// Copyright (c) True Goodwill. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace FFT.Subscriptions
{
  using System;

  /// <summary>
  /// This message is used to indicate that the the subscription stream has been
  /// interrupted. The subscription itself is still active though, and the
  /// stream will be reestablished once the underlying issue has been resolved.
  /// </summary>
  public sealed class StreamInterruptionMessage
  {
    /// <summary>
    /// An exception containing the reason for the stream interruption.
    /// </summary>
    public Exception? Reason { get; init; }
  }
}
