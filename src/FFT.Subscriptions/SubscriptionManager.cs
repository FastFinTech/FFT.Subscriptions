// Copyright (c) True Goodwill. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace FFT.Subscriptions
{
  using System;
  using System.Collections.Generic;
  using System.Collections.Immutable;
  using System.Diagnostics.CodeAnalysis;
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

    /// <summary>
    /// Initializes a new instance of the <see cref="SubscriptionManager{TKey}"/> class.
    /// </summary>
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

      ImmutableQueue<Subscription>? Transform(ImmutableQueue<Subscription>? list, Subscription subscription)
      {
        // Check for disposal.
        if (list is null)
        {
          // Inform user code that no data will be provided for this subscription.
          subscription.Complete(DisposalReason);
          return null;
        }

        return list.Enqueue(subscription);
      }
    }

    /// <inheritdoc/>
    protected override ValueTask CustomDisposeAsync()
      => new(_workTask);

    /// <summary>
    /// Thread-safely deques an item from the given <paramref name="queue"/>.
    /// Returns true if an item was dequeued, false othewise.
    /// </summary>
    private static bool Pop<T>(ref ImmutableQueue<T>? queue, [NotNullWhen(true)] out T? item)
      where T : class
    {
      T? result = null;
      ImmutableInterlocked.Update(ref queue, q =>
      {
        if (q is null)
        {
          result = null;
          return null;
        }

        if (q.IsEmpty)
        {
          result = null;
          return q;
        }

        return q.Dequeue(out result);
      });

      item = result;
      return item is not null;
    }

    private async Task WorkAsync()
    {
      Exception? workFailure = null;
      var hubs = new Dictionary<TKey, IBroadcastHub>();

      try
      {
        var signalTask = _subscriptionChangeEvent.WaitAsync(DisposedToken);
        var readTask = _options.GetNextMessage(this, DisposedToken);

waitSignal:
        {
          if (readTask.IsCompleted)
            goto handleMessage;

          if (signalTask.IsCompleted)
            goto subscriptionsChanged;

          await Task.WhenAny(signalTask, readTask.AsTask());
          goto waitSignal;
        }

subscriptionsChanged:
        {
          await signalTask;

          while (Pop(ref _subscriptionsToStart, out var subscription))
          {
            if (subscription.IsDisposeStarted)
              continue;

            if (!hubs.TryGetValue(subscription.StreamId, out var hub))
            {
              try
              {
                hub = await _options.StartStream(this, subscription.StreamId);
                hubs[subscription.StreamId] = hub;
              }
              catch (Exception x)
              {
                subscription.Complete(x);
                throw;
              }
            }

            hub.AddSubscriber(subscription);
          }

          while (Pop(ref _subscriptionsToCancel, out var subscription))
          {
            if (hubs.TryGetValue(subscription.StreamId, out var hub))
            {
              hub.RemoveSubscriber(subscription);
              if (hub.SubscriberCount == 0)
              {
                try
                {
                  await _options.EndStream(this, subscription.StreamId);
                }
                catch (Exception x)
                {
                  subscription.Complete(x);
                  throw;
                }

                hub.Complete(null);
                hubs.Remove(subscription.StreamId);
              }
            }

            subscription.Complete(null);
          }

          signalTask = _subscriptionChangeEvent.WaitAsync(DisposedToken);
          goto waitSignal;
        }

handleMessage:
        {
          var (streamId, message) = await readTask;
          if (hubs.TryGetValue(streamId, out var hub))
          {
            hub.Handle(message);
          }

          readTask = _options.GetNextMessage(this, DisposedToken);
          goto waitSignal;
        }
      }
      catch (Exception x)
      {
        workFailure = x;
      }
      finally
      {
        var newSubscriptions = Interlocked.Exchange(ref _subscriptionsToStart, null);
        if (newSubscriptions is not null)
        {
          foreach (var subscription in newSubscriptions)
            subscription.Complete(workFailure ?? DisposalReason);
        }

        var cancelSubscriptions = Interlocked.Exchange(ref _subscriptionsToCancel, null);
        if (cancelSubscriptions is not null)
        {
          foreach (var subscription in cancelSubscriptions)
            subscription.Complete(workFailure ?? DisposalReason);
        }

        foreach (var (streamId, hub) in hubs)
        {
          try
          {
            await _options.EndStream(this, streamId);
          }
          catch { }
          hub.Complete(workFailure ?? DisposalReason);
        }

        _ = DisposeAsync(workFailure);
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

      ImmutableQueue<Subscription>? Transform(ImmutableQueue<Subscription>? list, Subscription subscription)
      {
        // Check for disposal.
        if (list is null)
        {
          // Inform user code that no data will be provided for this subscription.
          subscription.Complete(DisposalReason);
          return null;
        }

        return list.Enqueue(subscription);
      }
    }
  }
}
