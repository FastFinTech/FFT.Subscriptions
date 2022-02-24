// Copyright (c) True Goodwill. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace FFT.Subscriptions
{
  using System;
  using System.Threading.Channels;
  using System.Threading.Tasks;
  using FFT.Disposables;
  using FFT.IgnoreTasks;

  public sealed class RetryingSubscription : DisposeBase, ISubscription
  {
    private readonly Channel<object> _channel = Channel.CreateBounded<object>(1);
    private readonly Func<ISubscription> _getSubscription;

    public RetryingSubscription(Func<ISubscription> getSubscription)
    {
      _getSubscription = getSubscription;
      Task.Run(Work).Ignore();
    }

    public ChannelReader<object> Reader => _channel.Reader;

    private async Task Work()
    {
      while (true)
      {
        try
        {
          using var subscription = _getSubscription();
          await foreach (var message in subscription.Reader.ReadAllAsync(DisposedToken))
          {
            await _channel.Writer.WriteAsync(message, DisposedToken);
          }

          await _channel.Writer.WriteAsync(new SubscriptionInterruptionMessage(null), DisposedToken);
          await Task.Delay(1000, DisposedToken);
        }
        catch (Exception x)
        {
          // The inner subscription reader can throw
          // OperationCanceledException when it is disposed due to feed
          // issues. Therefore we can't use a special
          // OperationCanceledException check here and expect to use that to
          // know when we are being disposed. Must use this property instead.
          if (IsDisposeStarted)
          {
            _channel.Writer.Complete();
            return;
          }
          else
          {
            await _channel.Writer.WriteAsync(new SubscriptionInterruptionMessage(x), DisposedToken);
            await Task.Delay(1000, DisposedToken);
          }
        }
      }
    }
  }
}
