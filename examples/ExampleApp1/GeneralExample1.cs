using Kwerty.DviZe.Win.Wnf;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Threading;
using System.Threading.Tasks;

namespace ExampleApp1;

public class GeneralExample1(WnfClient wnfClient, ILogger<GeneralExample1> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Create new state.

        var stateName = wnfClient.Create(WnfLifetime.Temporary, WnfScope.User, maxDataSize: 1);

        logger.LogInformation("Created state 0x{stateName:X}.", stateName);

        // Subscribe to events.

        var subscription = await wnfClient.SubscribeAsync(stateName, evt =>
        {
            logger.LogInformation("[Event] Data changed: 0x{val:X2}.", evt.Data[0]);
        }, stoppingToken);

        logger.LogInformation("Subscribed to events.");

        // Update the state.

        var val = new byte[] { 0x67 };
        wnfClient.Update(stateName, val);

        logger.LogInformation("Updated state data: 0x{val:X2}.", val[0]);

        // Query the state.

        var buffer = new byte[1];
        wnfClient.Query(stateName, buffer);

        logger.LogInformation("Queried state data: 0x{val:X2}.", buffer[0]);

        // Unsubscribe from events.

        subscription.Dispose();

        logger.LogInformation("Unsubscribed from events.");

        // Delete the state.
        // Note: This this will cause subscribers to receive an event with a zero-length buffer,
        // which is why we unsubscribed first.
        
        wnfClient.Delete(stateName);

        logger.LogInformation("Deleted state.");
    }
}