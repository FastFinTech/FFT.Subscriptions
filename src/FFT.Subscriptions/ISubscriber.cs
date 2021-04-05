// Copyright (c) True Goodwill. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace FFT.Subscriptions
{
  using System;
  using System.Threading.Channels;

  public interface ISubscriber : IDisposable
  {
    /// <summary>
    /// Read this reader to get messages from the subscription. This reader will be
    /// completed after you dispose the subscription or if the underlying
    /// subscription had an issue.
    /// </summary>
    ChannelReader<object> Reader { get; }
  }
}
