// Copyright (c) True Goodwill. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace FFT.Subscriptions.Example
{
  using System;
  using System.Collections.Concurrent;
  using System.Collections.Generic;
  using System.Diagnostics;
  using System.Net.WebSockets;
  using System.Threading;
  using System.Threading.Tasks;
  using FFT.Disposables;
  using Nito.AsyncEx;

  internal sealed partial class Program : AsyncDisposeBase
  {
    private static Task Main(string[] args)
      => new Program().DisposedTask;
  }

  internal partial class Program
  {
    private readonly List<string> _newSubscriptions = new();
    private readonly List<string> _removedSubscriptions = new();
    private readonly AutoResetEvent _
    private readonly ConcurrentDictionary<string, SubscriptionManager<object>> _streams = new();

    public Program()
    {
      Task.Run(WorkAsync).Ignore();
      Task.Run(async () =>
      {
        var rand = new Random();
        var availableSubscriptions = new[] { "Shoes", "Hats", "Vests", "Ties" };
        var subscribers = new List<Subscriber>();

        // ninety seconds worth of starting and ending subscriptions.
        for (var i = 0; i < 90; i++)
        {
          var subscriptionName = availableSubscriptions[rand.Next(0, 3)];
          subscribers.Add(new Subscriber(subscriptionName, Subscribe(subscriptionName)));
          if (subscribers.Count > 4)
          {
            var indexToRemove = rand.Next(0, subscribers.Count - 1);
            subscribers[indexToRemove].EndSubscription();
            subscribers.RemoveAt(indexToRemove);
          }

          await Task.Delay(1000);
        }

        await DisposeAsync();
      }).Ignore();
    }

    public ISubscriber<object> Subscribe(string subscriptionName)
    {
      var stream = _streams.GetOrAdd(subscriptionName, static _ => new SubscriptionList<object>());
      return stream.CreateSubscriber();
    }

    private async Task WorkAsync()
    {
      using var ws = new ClientWebSocket();
      await ws.ConnectAsync(new("wss://somefeed"), default);
      var buffer = new byte[1024];
      while (true)
      {
        await ws.ReceiveAsync(buffer, default);
      }
    }
  }

  internal abstract class Stream
  {
    public abstract Task InitializeAsync();
  }

  internal sealed class Subscriber
  {
    private readonly ISubscriber<object> _reader;

    public Subscriber(string subscriptionName, ISubscriber<object> reader)
    {
      _reader = reader;
      SubscriptionName = subscriptionName;
      Task.Run(ReadMessagesAsync).Ignore();
    }

    public string SubscriptionName { get; }

    public void EndSubscription()
    {
      _reader.Dispose();
    }

    async Task ReadMessagesAsync()
    {
      try
      {
        while (true)
        {
          await _reader.Reader.ReadAsync(default);
        }
      }
      catch (Exception x)
      {
        // just wanna check what exception is thrown when the reader is completed.
        Debugger.Break();
      }
    }
  }
}
