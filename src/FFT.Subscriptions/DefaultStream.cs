// Copyright (c) True Goodwill. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace FFT.Subscriptions
{
  using System.Collections.Generic;

  public sealed class DefaultStream : IStream
  {
    private readonly List<IWritable> _subscribers = new();

    public int SubscriptionCount => _subscribers.Count;

    public void AddSubscription(IWritable subscription)
      => _subscribers.Add(subscription);

    public void RemoveSubscription(IWritable subscription)
      => _subscribers.Remove(subscription);

    public void Handle(object message)
    {
      foreach (var subscriber in _subscribers)
        subscriber.Write(message);
    }

    public void Complete()
    {
      foreach (var subscriber in _subscribers)
        subscriber.Complete();
    }
  }
}
