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
    private readonly AsyncAutoResetEvent _subscriptionChangeEvent = new(false);

    /// <summary>
    /// Used to marshal new subscription requests into a thread-safe context.
    /// Null when disposal has begun.
    /// </summary>
    private ImmutableQueue<Subscription>? _subscriptionsToStart = ImmutableQueue<Subscription>.Empty;

    /// <summary>
    /// Used to marshal subscription cancellations into a thread-safe context.
    /// Null when disposal has begun.
    /// </summary>
    private ImmutableQueue<Subscription>? _subscriptionsToCancel = ImmutableQueue<Subscription>.Empty;

    public SubscriptionManager(SubscriptionManagerOptions<TKey> options)
    {
      _options = options;
      _workTask = Task.Run(WorkAsync);
    }

    /// <summary>
    /// Call this method to create a new subscription to the given <paramref
    /// name="streamId"/>. Internally, new data connections may be created if
    /// this is the first subscription for a particular <paramref
    /// name="streamId"/>.
    /// </summary>
    public ISubscription Subscribe(TKey streamId)
    {
      var subscription = new Subscription(this, streamId);
      ImmutableInterlocked.Update(ref _subscriptionsToStart, Transform, subscription);
      _subscriptionChangeEvent.Set();
      return subscription;

      static ImmutableQueue<Subscription>? Transform(ImmutableQueue<Subscription>? list, Subscription subscription)
      {
        // Check for disposal.
        if (list is null)
        {
          // Inform user code that no data will be provided for this subscription.
          subscription.Complete();
          return null;
        }

        return list.Enqueue(subscription);
      }
    }

    /// <summary>
    /// This method is called by the <paramref name="subscription"/> when it is
    /// disposed. Typically, this happens when user code no longer wants to
    /// receive subscription updates.
    /// </summary>
    private void Unsubscribe(Subscription subscription)
    {
      ImmutableInterlocked.Update(ref _subscriptionsToCancel, Transform, subscription);
      _subscriptionChangeEvent.Set();

      static ImmutableQueue<Subscription>? Transform(ImmutableQueue<Subscription>? list, Subscription subscription)
      {
        // Check for disposal.
        if (list is null)
        {
          // Inform user code that no data will be provided for this subscription.
          subscription.Complete();
          return null;
        }

        return list.Enqueue(subscription);
      }
    }

    protected override ValueTask CustomDisposeAsync()
      => new(_workTask);

    private async Task WorkAsync()
    {
      var streams = new Dictionary<TKey, IBroadcastHub>();

      try
      {
        var signalTask = _subscriptionChangeEvent.WaitAsync(DisposedToken);
        var readTask = _options.GetNextMessage(this, DisposedToken);

waitSignal:

        if (readTask.IsCompleted)
          goto handleMessage;

        if (signalTask.IsCompleted)
          goto subscriptionsChanged;

        await Task.WhenAny(signalTask, readTask.AsTask());
        goto waitSignal;

subscriptionsChanged:

        await signalTask;

        foreach (var subscription in PopQueue(ref _subscriptionsToStart))
        {
          if (!streams.TryGetValue(subscription.StreamId, out var stream))
          {
            stream = await _options.StartStream(this, subscription.StreamId);
            streams[subscription.StreamId] = stream;
          }

          stream.AddSubscriber(subscription);
        }

        foreach (var subscription in PopQueue(ref _subscriptionsToCancel))
        {
          if (streams.TryGetValue(subscription.StreamId, out var stream))
          {
            stream.RemoveSubscriber(subscription);
            if (stream.SubscriberCount == 0)
            {
              await _options.EndStream(this, subscription.StreamId);
              stream.Complete();
              streams.Remove(subscription.StreamId);
            }
          }

          subscription.Complete();
        }

        signalTask = _subscriptionChangeEvent.WaitAsync(DisposedToken);
        goto waitSignal;

handleMessage:
        {
          var (streamId, message) = await readTask;

          if (streams.TryGetValue(streamId, out var stream))
          {
            stream.Handle(message);
          }

          readTask = _options.GetNextMessage(this, DisposedToken);
          goto waitSignal;
        }
      }
      catch (Exception x)
      {
        _ = DisposeAsync(x);
      }
      finally
      {
        var newSubscriptions = Interlocked.Exchange(ref _subscriptionsToStart, null);
        if (newSubscriptions is not null)
        {
          foreach (var subscription in newSubscriptions)
            subscription.Complete();
        }

        var cancelSubscriptions = Interlocked.Exchange(ref _subscriptionsToCancel, null);
        if (cancelSubscriptions is not null)
        {
          foreach (var subscription in cancelSubscriptions)
            subscription.Complete();
        }

        foreach (var (streamId, stream) in streams)
        {
          try
          {
            await _options.EndStream(this, streamId);
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
      return result ?? ImmutableQueue<Subscription>.Empty;

      ImmutableQueue<Subscription>? Transform(ImmutableQueue<Subscription>? queue)
      {
        result = queue;
        return result is null ? null : ImmutableQueue<Subscription>.Empty;
      }
    }
  }
}
