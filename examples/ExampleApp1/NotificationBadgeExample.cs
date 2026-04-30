using Kwerty.DviZe.Win.Wnf;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ExampleApp1;

public class NotificationBadgeExample(WnfClient wnfClient, ILogger<NotificationBadgeExample> logger) : IHostedService
{
    const ulong WNF_SHEL_NOTIFICATIONS = 0xD83063EA3BC1035;
    IDisposable subscription;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        // Subscribe to notification badge updates.
        // Pass the current changestamp, since we're only interested in future events.

        var currentChangeStamp = wnfClient.Query(WNF_SHEL_NOTIFICATIONS).ChangeStamp;
        subscription = await wnfClient.SubscribeAsync<ShellNotificationBadge>(WNF_SHEL_NOTIFICATIONS, currentChangeStamp, evt =>
        {
            logger.LogInformation("[Event] State data updated: {data}.", evt.Data.UnreadCount);

        }, cancellationToken);

        logger.LogInformation("Subscribed to events.");

        // Update notification badge.

        var stateData = new ShellNotificationBadge { UnreadCount = 67 };

        wnfClient.Update(WNF_SHEL_NOTIFICATIONS, stateData);

        logger.LogInformation("Updated state data: {stateData}.", stateData.UnreadCount);

        logger.LogInformation("Click notification badge (bottom right) to trigger more events.");
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        subscription.Dispose();
        return Task.CompletedTask;
    }

    struct ShellNotificationBadge
    {
        public uint UnreadCount;
    }
}