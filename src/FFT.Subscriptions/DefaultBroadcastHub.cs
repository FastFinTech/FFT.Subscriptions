// Copyright (c) True Goodwill. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace FFT.Subscriptions
{
  using System;
  using System.Collections.Generic;

  /// <summary>
  /// A very simple implementation of <see cref="IBroadcastHub"/> provided by
  /// the framework that you can reuse for most simple scenarios. This
  /// implementation performs no initialization to itself, or to suscribers.
  /// Messages that it handles are passed directly to the subscribers.
  /// </summary>
  public sealed class DefaultBroadcastHub : IBroadcastHub
  {
    private readonly List<IWritable> _subscribers = new();

    /// <inheritdoc/>
    public int SubscriberCount => _subscribers.Count;

    /// <inheritdoc/>
    public void AddSubscriber(IWritable subscriber)
      => _subscribers.Add(subscriber);

    /// <inheritdoc/>
    public void RemoveSubscriber(IWritable subscriber)
      => _subscribers.Remove(subscriber);

    /// <inheritdoc/>
    public void Handle(object message)
    {
      foreach (var subscriber in _subscribers)
        subscriber.Write(message);
    }

    /// <inheritdoc/>
    public void Complete(Exception? error)
    {
      foreach (var subscriber in _subscribers)
        subscriber.Complete(error);
    }
  }
}
