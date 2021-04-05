// Copyright (c) True Goodwill. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace FFT.Subscriptions
{
  using System;
  using System.Threading;
  using System.Threading.Tasks;

  public sealed class SubscriptionManagerOptions<TKey>
    where TKey : notnull
  {
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

    /// <summary>
    /// Method called by the <see cref="SubscriptionManager{TKey}"/> when the
    /// first subcription is made for a given stream.
    /// </summary>
    public Func<TKey, ValueTask<IStream>> CreateStream { get; init; }

    /// <summary>
    /// Method called by the <see cref="SubscriptionManager{TKey}"/> when the
    /// last remaining subscription for a given stream is unsubscribed.
    /// </summary>
    public Func<TKey, ValueTask> EndStream { get; init; }

    /// <summary>
    /// Method called by the <see cref="SubscriptionManager{TKey}"/> to retrieve
    /// the next message. Throw an <see cref="OperationCanceledException"/> if
    /// you are no longer providing messages, or if the given cancellation token
    /// is canceled.
    /// </summary>
    public Func<CancellationToken, ValueTask<(TKey StreamId, object Message)>> GetNextMessage { get; init; }
  }
}
