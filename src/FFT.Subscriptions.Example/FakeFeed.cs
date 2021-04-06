// Copyright (c) True Goodwill. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace FFT.Subscriptions.Example
{
  using System;
  using System.Collections.Immutable;
  using System.Diagnostics;
  using System.Threading;
  using System.Threading.Channels;
  using System.Threading.Tasks;
  using FFT.Disposables;

  internal sealed class FakeFeed : AsyncDisposeBase
  {
    private readonly Channel<string> _channel = Debugger.IsAttached
      ? Channel.CreateUnbounded<string>()
      : Channel.CreateBounded<string>(100);

    private readonly Task _workTask;

    private ImmutableList<string> _streamIds = ImmutableList<string>.Empty;

    public FakeFeed()
    {
      _workTask = Task.Run(WorkAsync);
    }

    public ChannelReader<string> Reader => _channel.Reader;

    public void Subscribe(string streamId)
    {
      ImmutableInterlocked.Update(ref _streamIds, Transform, streamId);

      static ImmutableList<string> Transform(ImmutableList<string> list, string streamId)
      {
        if (list.Contains(streamId))
          return list;
        return list.Add(streamId);
      }
    }

    public void Unsubscribe(string streamId)
    {
      ImmutableInterlocked.Update(ref _streamIds, Transform, streamId);

      static ImmutableList<string> Transform(ImmutableList<string> list, string streamId)
      {
        if (list.Contains(streamId))
          return list.Remove(streamId);
        return list;
      }
    }

    protected override async ValueTask CustomDisposeAsync()
    {
      await _workTask;
    }

    private async Task WorkAsync()
    {
      try
      {
        int count = 0;
        while (true)
        {
          var streamIds = Interlocked.CompareExchange(ref _streamIds!, null!, null!);
          foreach (var streamId in streamIds)
            await _channel.Writer.WriteAsync($"{streamId}_{count}");
          count++;
          await Task.Delay(250, DisposedToken);
        }
      }
      catch (OperationCanceledException) { }
      finally
      {
        _channel.Writer.Complete();
      }
    }
  }
}
