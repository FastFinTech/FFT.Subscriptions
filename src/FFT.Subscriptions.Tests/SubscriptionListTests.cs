// Copyright (c) True Goodwill. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace FFT.Subscriptions.Tests
{
  using System.Threading.Tasks;
  using Microsoft.VisualStudio.TestTools.UnitTesting;

  [TestClass]
  public class SubscriptionListTests
  {
    [TestMethod]
    public async Task TestMethod1()
    {
      await using var list = new SubscriptionList<object>();
    }
  }
}
