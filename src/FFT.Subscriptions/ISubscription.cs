// Copyright (c) True Goodwill. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace FFT.Subscriptions
{
  using System;
  using System.Threading.Channels;

  /// <summary>
  /// An <see cref="ISubscription"/> is returned by the <see
  /// cref="SubscriptionManager{TKey}.Subscribe(TKey)"/> method. It contains a
  /// <see cref="Reader"/> that you can use to access messages from the
  /// subscription.
  /// </summary>
  /// <remarks>
  /// IMPORTANT!! Dispose it when finished to signal that the subscription is no
  /// longer required. This allows the graceful termination of internal data
  /// connections.
  /// </remarks>
  public interface ISubscription : IDisposable
  {
    /// <summary>
    /// Use this reader to read messages from the subscription. This reader will
    /// be completed after you dispose the subscription or if the underlying
    /// data supply completes or fails.
    /// </summary>
    ChannelReader<object> Reader { get; }
  }
}
