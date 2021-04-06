// Copyright (c) True Goodwill. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace FFT.Subscriptions
{
  /// <summary>
  /// Implement this interface to publish messages to multiple subscribers.
  /// <para>
  /// The <see cref="SubscriptionManager{TKey}"/> guarantees to call the methods
  /// sequentially without interleaving, so you can write your implementation
  /// assuming that it is threadsafe.
  /// </para>
  /// <para>
  /// Generally, for all simple cases, you can use the <see
  /// cref="DefaultBroadcastHub"/> that is already provided instead of
  /// implementing your own. For more complex cases, in which you need to
  /// processing incoming data and publish transformed results, or keep track of
  /// system state, you will need to create your own implementation.
  /// </para>
  /// </summary>
  public interface IBroadcastHub
  {
    /// <summary>
    /// Gets the number of subscribers currently attached to this stream.
    /// </summary>
    int SubscriberCount { get; }

    /// <summary>
    /// Adds a new subscriber to the subscriber list.
    /// </summary>
    void AddSubscriber(IWritable subscription);

    /// <summary>
    /// Removes the given subscriber from the subscriber list.
    /// </summary>
    void RemoveSubscriber(IWritable subscription);

    /// <summary>
    /// Publishes the given message to all subscribers. Optionally, some
    /// processing / transformations may be applied to the <paramref
    /// name="message"/> before publishing to the subscribers. For example,
    /// <paramref name="message"/> could be a change event, and you may publish
    /// the adjusted state instead of the change event.
    /// </summary>
    void Handle(object message);

    /// <summary>
    /// Signal to all subscribers that no more messages will be published for
    /// this stream.
    /// </summary>
    void Complete();
  }
}
