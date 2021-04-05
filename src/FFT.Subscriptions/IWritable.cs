// Copyright (c) True Goodwill. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace FFT.Subscriptions
{
  using System;

  public interface IWritable : IDisposable
  {
    void Write(object message);

    void Complete();
  }
}
