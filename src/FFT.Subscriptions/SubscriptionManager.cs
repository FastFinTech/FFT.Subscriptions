// Copyright (c) True Goodwill. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace FFT.Subscriptions
{
  using System;
  using System.Collections.Generic;
  using System.Collections.Immutable;
  using System.Threading;
  using System.Threading.Tasks;
  using FFT.Disposables;
  using Nito.AsyncEx;

  public sealed partial class SubscriptionManager<TKey> : AsyncDisposeBase
    where TKey : notnull
  {
    private static readonly ImmutableQueue<Subscription> _emptyQueue = ImmutableQueue<Subscription>.Empty;

    private readonly SubscriptionManagerOptions<TKey> _options;

    /// <summary>
    /// This task completes when the <see cref="WorkAsync"/> method returns.
    /// </summary>
    /// <remarks>
    /// The <see cref="CustomDisposeAsync"/> method awaits this task in order to
    /// make the disposal wait for the <see cref="WorkAsync"/> method to return.
    /// It is a requirement that <see cref="CustomDisposeAsync"/> does not throw
    /// an exception, so it is a requirement that awaiting this task never
    /// throws an exception. To achieve this we must ensure that the <see
    /// cref="WorkAsync"/> method completes without throwing an exception.
    /// </remarks>
    private readonly Task _workTask;

    /// <summary>
    /// Signals when a subscription is added or removed.
    /// </summary>
    private readonly AsyncAutoResetEvent _signalEvent = new(false);

    /// <summary>
    /// Used to marshal new subscription requests into a thread-safe context.
    /// Null when disposal has begun.
    /// </summary>
    private ImmutableQueue<Subscription>? _newSubscriptions = _emptyQueue;

    /// <summary>
    /// Used to marshal subscription cancellations into a thread-safe context.
    /// Null when disposal has begun.
    /// </summary>
    private ImmutableQueue<Subscription>? _cancelSubscriptions = _emptyQueue;

    public SubscriptionManager(SubscriptionManagerOptions<TKey> options)
    {
      _options = options;
      _workTask = Task.Run(WorkAsync);
    }

    public ISubscriber CreateSubscriber(TKey streamId)
    {
      var subscription = new Subscription(streamId, this);
      ImmutableInterlocked.Update(ref _newSubscriptions, Transform, subscription);
      _signalEvent.Set();
      return subscription;

      static ImmutableQueue<Subscription>? Transform(ImmutableQueue<Subscription>? newSubscriptions, Subscription subscription)
      {
        // Check for disposal.
        if (newSubscriptions is null)
        {
          // Inform user code that no data will be provided for this subscription.
          subscription.Complete();
          return null;
        }

        return newSubscriptions.Enqueue(subscription);
      }
    }

    /// <summary>
    /// This method is called by the <paramref name="subscription"/> when it is
    /// disposed. Typically, this happens when user code no longer wants to
    /// receive subscription updates.
    /// </summary>
    private void EnqueueRemoval(Subscription subscription)
    {
      ImmutableInterlocked.Update(ref _cancelSubscriptions, Transform, subscription);
      _signalEvent.Set();

      static ImmutableQueue<Subscription>? Transform(ImmutableQueue<Subscription>? cancelSubscriptions, Subscription subscription)
      {
        // Check for disposal.
        if (cancelSubscriptions is null)
        {
          // Inform user code that no data will be provided for this subscription.
          subscription.Complete();
          return null;
        }

        return cancelSubscriptions.Enqueue(subscription);
      }
    }

    protected override ValueTask CustomDisposeAsync()
      => new(_workTask);

    private async Task WorkAsync()
    {
      var streams = new Dictionary<TKey, IStream>();

      try
      {
        var signalTask = _signalEvent.WaitAsync(DisposedToken);
        var readTask = _options.GetNextMessage(DisposedToken);

waitSignal:

        if (signalTask.IsCompleted)
          goto subscriptionsChanged;

        if (readTask.IsCompleted)
          goto handleMessage;

        await Task.WhenAny(signalTask, readTask.AsTask());
        goto waitSignal;

subscriptionsChanged:

        await signalTask;
        signalTask = _signalEvent.WaitAsync(DisposedToken);

        foreach (var subscription in PopQueue(ref _newSubscriptions))
        {
          if (!streams.TryGetValue(subscription.StreamId, out var stream))
          {
            stream = await _options.CreateStream(subscription.StreamId);
            streams[subscription.StreamId] = stream;
          }

          stream.AddSubscription(subscription);
        }

        foreach (var subscription in PopQueue(ref _cancelSubscriptions))
        {
          if (streams.TryGetValue(subscription.StreamId, out var stream))
          {
            stream.RemoveSubscription(subscription);
            if (stream.SubscriptionCount == 0)
            {
              await _options.EndStream(subscription.StreamId);
              stream.Complete();
              streams.Remove(subscription.StreamId);
            }
          }

          subscription.Complete();
        }

        goto waitSignal;

handleMessage:

        var (streamId, message) = await readTask;
        readTask = _options.GetNextMessage(DisposedToken);

        if (streams.TryGetValue(streamId, out var stream))
        {
          stream.Handle(message);
        }

        goto waitSignal;
      }
      catch (Exception x)
      {
        _ = DisposeAsync(x);
      }
      finally
      {
        var newSubscriptions = Interlocked.Exchange(ref _newSubscriptions, null);
        if (newSubscriptions is not null)
        {
          foreach (var subscription in newSubscriptions)
            subscription.Complete();
        }

        var cancelSubscriptions = Interlocked.Exchange(ref _cancelSubscriptions, null);
        if (cancelSubscriptions is not null)
        {
          foreach (var subscription in cancelSubscriptions)
            subscription.Complete();
        }

        foreach (var (streamId, stream) in streams)
        {
          try
          {
            await _options.EndStream(streamId);
          }
          catch { }
          stream.Complete();
        }

        _ = DisposeAsync();
      }
    }

    /// <summary>
    /// Thread-safely gets the entire queue, replacing it with an empty queue.
    /// Returns an empty queue if we have been disposed.
    /// </summary>
    private ImmutableQueue<Subscription> PopQueue(ref ImmutableQueue<Subscription>? queue)
    {
      ImmutableQueue<Subscription>? result = null;
      ImmutableInterlocked.Update(ref queue, Transform);
      return result ?? _emptyQueue;

      ImmutableQueue<Subscription>? Transform(ImmutableQueue<Subscription>? queue)
      {
        result = queue;
        return result is null ? null : _emptyQueue;
      }
    }
  }
}
