// Copyright (c) True Goodwill. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace FFT.Subscriptions
{
  public interface IStream
  {
    int SubscriptionCount { get; }

    void AddSubscription(IWritable subscription);

    void RemoveSubscription(IWritable subscription);

    void Handle(object message);

    void Complete();
  }
}
