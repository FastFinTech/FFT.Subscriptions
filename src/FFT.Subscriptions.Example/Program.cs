// Copyright (c) True Goodwill. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable SA1200 // Using directives should be placed correctly

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FFT.Subscriptions;
using FFT.Subscriptions.Example;

// We'll use this to signal completion of the program.
using var cts = new CancellationTokenSource();

// Setup a simulation input feed. In real life, it might be a websocket or any
// other kind of facility that provides data.
await using var fakeFeed = new FakeFeed();

// Create a subscription manager that consumes data from the input feed and
// redistributes it to various program components that want to consume it. The
// subscription manager will also provide a way for the various program
// components to subscribe and unsubscribe from various data feeds.
await using var subscriptionManager = new SubscriptionManager<string>(
  new SubscriptionManagerOptions<string>
  {
    StartStream = (requester, streamId, cancellationToken) =>
    {
      Console.WriteLine($"Starting stream '{streamId}'.");
      // The given stream is being subscribed to for the first time, so we
      // simulate requesting data from the input feed.
      fakeFeed.Subscribe(streamId);
      // Return an IBroadcastHub that will redistribute the feed's data to the
      // various subscribers.
      return new(new DefaultBroadcastHub());
    },
    EndStream = (requester, streamId) =>
    {
      Console.WriteLine($"Ending stream '{streamId}'.");
      // No more subscribers are using the given feed, so we simulate
      // unsubscribing this stream from the input feed.
      fakeFeed.Unsubscribe(streamId);
      return default;
    },
    GetNextMessage = async (requester, cancellationToken) =>
    {
      using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, cts.Token);
      var message = await fakeFeed.Reader.ReadAsync(linked.Token);
      var streamId = message.Substring(0, message.IndexOf('_'));
      return (streamId, message);
    },
  });

// Now we create a bunch of components that want to consume the data. Start off
// by creating two subscribers to the same stream.
var bitcoin1 = subscriptionManager.Subscribe("Bitcoin");
var bitcoin2 = subscriptionManager.Subscribe("Bitcoin");

// And one subscriber to another stream.
var etherium1 = subscriptionManager.Subscribe("Etherium");

// ... the fake feed will be providing stream data, which is redistributed to
// the various subscribers that we created above.
await Task.Delay(1100);

// Unsubscribe all but one of the subscribers. The shirts stream will completely
// stop, because there are no subscribers left for it. The shoes stream will
// continue supplying data, because there is still one subscriber attached to
// it.
bitcoin2.Dispose();
etherium1.Dispose();

await Task.Delay(1100);
bitcoin1.Dispose();

Console.WriteLine($"bitcoin1 count = {await bitcoin1.Reader.ReadAllAsync().CountAsync()}");
Console.WriteLine($"bitcoin2 count = {await bitcoin2.Reader.ReadAllAsync().CountAsync()}");
Console.WriteLine($"etherium1 count = {await etherium1.Reader.ReadAllAsync().CountAsync()}");

cts.Cancel();
