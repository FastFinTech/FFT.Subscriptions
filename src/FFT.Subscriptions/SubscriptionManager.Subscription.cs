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
    internal sealed class Subscription : DisposeBase, ISubscriber, IWritable
    {
      private readonly SubscriptionManager<TKey> _manager;

      private readonly Channel<object> _channel = Debugger.IsAttached
        ? Channel.CreateUnbounded<object>()
        : Channel.CreateBounded<object>(100);

      public Subscription(TKey streamId, SubscriptionManager<TKey> subscriptionList)
      {
        _manager = subscriptionList;
        StreamId = streamId;
      }

      public TKey StreamId { get; }

      ChannelReader<object> ISubscriber.Reader
        => _channel.Reader;

      void IWritable.Write(object message)
      {
        if (!_channel.Writer.TryWrite(message))
        {
          // TODO: the reason may also be because the channel is completed.

          // If execution reaches here, the channel queue is full because the
          // user code is not consuming events. Rather than build up a useless
          // queue of events, we dispose ourselves to signal completion to the
          // user code have the subscription removed.
          Dispose(new Exception("Bounded channel overflowed. (Reader is too slow)."));
        }
      }

      /// <summary>
      /// This method is called internally to inform user code that no more
      /// messages will be provided for this subscription.
      /// </summary>
      internal void Complete()
        => _channel.Writer.TryComplete();

      /// <summary>
      /// Disposal is triggered by user code to inform us that that the
      /// subscription is no longer required, as part of the <see
      /// cref="ISubscriber"/> interface.
      /// </summary>
      protected override void CustomDispose()
        => _manager.EnqueueRemoval(this);
    }
  }
}
