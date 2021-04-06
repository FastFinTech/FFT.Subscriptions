# FFT.Subscriptions

[![Source code](https://img.shields.io/static/v1?style=flat&label=&message=Source%20Code&logo=read-the-docs&color=informational)](https://github.com/FastFinTech/FFT.Subscriptions)
[![NuGet
package](https://img.shields.io/nuget/v/FFT.Subscriptions.svg)](https://nuget.org/packages/FFT.Subscriptions)

Use this package within your application when messages from one data source need to be redistributed amongst multiple consuming subscriber components. 

Hooks allow you to start and stop consuming data from the original data source when the first component subscribes and when the last component unsubscribes.

Implementing your own `IBroadcastHub` allows you to customize the messages sent to subscribers, and allows you to initialize the data and process / transform incoming messages before sending data to the various subscriber components.
