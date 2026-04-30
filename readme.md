# DviZe.Win.Wnf

A .NET 10 class library for working with undocumented Windows Notification Facility (WNF) APIs.

```csharp
using Kwerty.DviZe.Win.Wnf;

const ulong WNF_SHEL_LOCKSCREEN_ACTIVE = 0xD83063EA3BC5835;

var subscription = wnfClient.SubscribeAsync(WNF_SHEL_LOCKSCREEN_ACTIVE, evt =>
{
    Console.WriteLine("Lockscreen active");
});

subscription.Dispose(); // Unsubscribe.
```

See [ExampleApp1](examples/ExampleApp1/) for other examples.
