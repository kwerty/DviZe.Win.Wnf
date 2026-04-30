using Kwerty.DviZe.Win.Wnf;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ExampleApp1;

public class WellKnownStatesExample(WnfClient wnfClient, ILogger<WellKnownStatesExample> logger) : IHostedService
{
    const ulong WNF_SHEL_LOCKSCREEN_ACTIVE = 0xD83063EA3BC5835;
    const ulong WNF_SHEL_START_VISIBILITY_CHANGED = 0xD83063EA3BCB035;

    IDisposable subscription1;
    IDisposable subscription2;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        subscription1 = wnfClient.SubscribeAsync(WNF_SHEL_LOCKSCREEN_ACTIVE, evt =>
        {
            logger.LogInformation("{event} event. Changestamp = {changeStamp}.", nameof(WNF_SHEL_LOCKSCREEN_ACTIVE), evt.ChangeStamp);
        });

        subscription2 = wnfClient.SubscribeAsync(WNF_SHEL_START_VISIBILITY_CHANGED, evt =>
        {
            logger.LogInformation("{event} event. Changestamp = {changeStamp}.", nameof(WNF_SHEL_START_VISIBILITY_CHANGED), evt.ChangeStamp);
        });

        logger.LogInformation("Subscribed to {name}.", nameof(WNF_SHEL_LOCKSCREEN_ACTIVE));
        logger.LogInformation("Subscribed to {name}.", nameof(WNF_SHEL_START_VISIBILITY_CHANGED));
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        subscription1.Dispose();
        subscription2.Dispose();
        return Task.CompletedTask;
    }
}
