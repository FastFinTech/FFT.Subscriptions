// Copyright (c) True Goodwill. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace FFT.Subscriptions
{
  using System;
  using System.Threading;
  using System.Threading.Tasks;

  /// <summary>
  /// Contains methods the that <see cref="SubscriptionManager{TKey}"/> can call
  /// to complete its operations.
  /// </summary>
  /// <typeparam name="TKey">The type of the stream id.</typeparam>
  /// <remarks>
  /// All methods are guaranteed to be called sequentially and without
  /// interleaving by a given <see cref="SubscriptionManager{TKey}"/>
  /// "requester". If you have more than one <see
  /// cref="SubscriptionManager{TKey}"/> "requester" accessing the same method
  /// instances, you will need to make your own threadsafety guarantees within
  /// the implementation of each method. The "requester" parameter is added to
  /// each method's signature to help you facilitate this if necessary.
  /// </remarks>
  public sealed class SubscriptionManagerOptions<TKey>
    where TKey : notnull
  {
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

    /// <summary>
    /// Called only once, when the subscription manager has just started up.
    /// </summary>
    /// <param name="requester">The <see cref="SubscriptionManager{TKey}"/> that
    /// is requesting the initialization.</param>
    public delegate ValueTask InitialiseDelegate(SubscriptionManager<TKey> requester);

    /// <summary>
    /// Called when the first subscription is made for a given stream. Starts
    /// the underlying data connection.
    /// </summary>
    /// <param name="requester">The <see cref="SubscriptionManager{TKey}"/> that
    /// is requesting the subscription start.</param>
    /// <param name="streamId">The id of the stream to be started.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>
    /// Returns a <see cref="IBroadcastHub"/> that allows customized handling of
    /// message publishing and subscriber adding / removing.
    /// </returns>
    /// <exception cref="OperationCanceledException">Thrown when the operation
    /// is cancelled, not only because of the cancellation token, but also
    /// possibly because of internal data connections shutting down.</exception>
    public delegate ValueTask<IBroadcastHub> StartStreamDelegate(SubscriptionManager<TKey> requester, TKey streamId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Called when the last subscription is cancelled for a given stream. Ends
    /// the underlying data connection.
    /// </summary>
    /// <param name="requester">The <see cref="SubscriptionManager{TKey}"/> that
    /// is requesting the subscription start.</param>
    /// <param name="streamId">The id of the stream to be ended.</param>
    public delegate ValueTask EndStreamDelegate(SubscriptionManager<TKey> requester, TKey streamId);

    /// <summary>
    /// Called when the <see cref="SubscriptionManager{TKey}"/> is ready to
    /// receive the next message for any of the subscribed streams.
    /// </summary>
    /// <param name="requester">The <see cref="SubscriptionManager{TKey}"/> that
    /// is requesting the subscription start.</param>
    /// <param name="cancellationToken">Cancels the read operation.</param>
    /// <exception cref="OperationCanceledException">Thrown when the operation
    /// is cancelled, not only because of the cancellation token, but also
    /// possibly because of internal data connections shutting down.</exception>
    public delegate ValueTask<(TKey StreamId, object Message)> GetNextMessageDelegate(SubscriptionManager<TKey> requester, CancellationToken cancellationToken);

    /// <summary>
    /// <see cref="InitialiseDelegate"/> for more information.
    /// </summary>
    public InitialiseDelegate? Initialize { get; init; }

    /// <summary>
    /// See <see cref="StartStreamDelegate"/> for more information.
    /// </summary>
    public StartStreamDelegate StartStream { get; init; }

    /// <summary>
    /// See <see cref="EndStreamDelegate"/> for more information.
    /// </summary>
    public EndStreamDelegate EndStream { get; init; }

    /// <summary>
    /// <see cref="GetNextMessageDelegate"/> for more information.
    /// </summary>
    public GetNextMessageDelegate GetNextMessage { get; init; }
  }
}
