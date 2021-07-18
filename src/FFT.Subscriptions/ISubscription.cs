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
    /// Use this reader to read messages from the subscription. This reader's
    /// ReadAllAsync method will complete without exception if you dispose the
    /// subscription to stop it. If the subscription is stopped due to some
    /// other issue, including disposal of the subscription manager, the
    /// ReadAllAsync method will throw an exception.
    /// </summary>
    ChannelReader<object> Reader { get; }
  }
}
