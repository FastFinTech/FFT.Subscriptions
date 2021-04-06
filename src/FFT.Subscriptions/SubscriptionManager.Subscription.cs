// Copyright (c) True Goodwill. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace FFT.Subscriptions
{
  using System;
  using System.Diagnostics;
  using System.Threading.Channels;
  using FFT.Disposables;

  public partial class SubscriptionManager<TKey>
  {
    /// <summary>
    /// For internal use, this represents a single subscription to a given
    /// stream.
    /// <para>
    /// It implements <see cref="ISubscription"/> so it can be passed to user
    /// code, which can consume data from the channel reader, and dispose it
    /// when finished with the subscription.
    /// </para>
    /// <para>
    /// It implements <see cref="IWritable"/> so that the <see
    /// cref="IBroadcastHub"/> can write messages destined for the subscribers.
    /// </para>
    /// </summary>
    /// <remarks>
    /// The Dispose method is implemented for use by user code to indicate that
    /// it no longer requires the subscription, so this implemenation is a
    /// little different, in the sense that "Dispose" does not immediately
    /// completely shutdown the object ... it actually just enqueues it for
    /// shutdown.
    /// </remarks>
    internal sealed class Subscription : DisposeBase, ISubscription, IWritable
    {
      private readonly SubscriptionManager<TKey> _manager;

      private readonly Channel<object> _channel = Debugger.IsAttached
        ? Channel.CreateUnbounded<object>()
        : Channel.CreateBounded<object>(100);

      public Subscription(SubscriptionManager<TKey> manager, TKey streamId)
      {
        _manager = manager;
        StreamId = streamId;
      }

      public TKey StreamId { get; }

      /// <summary>
      /// Accessed by user code.
      /// </summary>
      ChannelReader<object> ISubscription.Reader
        => _channel.Reader;

      /// <summary>
      /// Called by the <see cref="IBroadcastHub"/>.
      /// </summary>
      void IWritable.Write(object message)
      {
        if (!_channel.Writer.TryWrite(message))
        {
          // Execution may reach here if the channel is completed, or if the
          // bounded channel has filled up due to slow reading. We handle the
          // second case by completing the reader with an appropriate exception.
          // Doing this makes no difference in the first case.
          _channel.Writer.TryComplete(new Exception("Bounded channel overflowed. (Reader is too slow)."));

          // Enqueue ourselves for subscription removal.
          Dispose();
        }
      }

      /// <summary>
      /// Called by the <see cref="IBroadcastHub"/> to inform user code that no
      /// more messages will be provided for this subscription.
      /// </summary>
      public void Complete()
        => _channel.Writer.TryComplete();

      /// <summary>
      /// Disposal is triggered by user code to inform us that that the
      /// subscription is no longer required, as part of the <see
      /// cref="ISubscription"/> interface.
      /// </summary>
      protected override void CustomDispose()
        => _manager.Unsubscribe(this);
    }
  }
}
