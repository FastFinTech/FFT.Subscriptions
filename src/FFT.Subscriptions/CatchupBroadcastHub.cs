// Copyright (c) True Goodwill. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace FFT.Subscriptions
{
  using System;
  using System.Collections.Generic;

  /// <summary>
  /// This implementation of <see cref="IBroadcastHub"/> keeps a record of the last-sent message
  /// and immediately sends it to new subscribers, effectively catching them up on the latest
  /// information when they join.
  /// </summary>
  public sealed class CatchupBroadcastHub : IBroadcastHub
  {
    private readonly List<IWritable> _subscribers = new();

    private object? _lastMessage;

    /// <inheritdoc/>
    public int SubscriberCount => _subscribers.Count;

    /// <inheritdoc/>
    public void AddSubscriber(IWritable subscriber)
    {
      _subscribers.Add(subscriber);
      if (_lastMessage is not null)
        subscriber.Write(_lastMessage);
    } 

    /// <inheritdoc/>
    public void RemoveSubscriber(IWritable subscriber)
      => _subscribers.Remove(subscriber);

    /// <inheritdoc/>
    public void Handle(object message)
    {
      foreach (var subscriber in _subscribers)
        subscriber.Write(message);
      _lastMessage = message;
    }

    /// <inheritdoc/>
    public void Complete(Exception? error)
    {
      foreach (var subscriber in _subscribers)
        subscriber.Complete(error);
    }
  }
}
