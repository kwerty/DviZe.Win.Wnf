using Kwerty.DviZe.Win.Wnf;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Threading;
using System.Threading.Tasks;

namespace ExampleApp1;

public class GeneralExample2(WnfClient wnfClient, ILogger<GeneralExample2> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Create new state.

        var stateName = wnfClient.Create<MyStateData>(WnfLifetime.Temporary, WnfScope.User);

        logger.LogInformation("Created state 0x{stateName:X}.", stateName);

        // Subscribe to events.

        var subscription = await wnfClient.SubscribeAsync<MyStateData>(stateName, evt =>
        {
            logger.LogInformation("[Event] Data changed: Val1={val1} Val2={val2}.", evt.Data.Val1, evt.Data.Val2);
        }, stoppingToken);

        // Update the state.

        var val = new MyStateData
        {
            Val1 = true,
            Val2 = 67,
        };
        wnfClient.Update(stateName, val);

        logger.LogInformation("Updated state data: Val1={val1} Val2={val2}.", val.Val1, val.Val2);

        // Query the state.

        var result = wnfClient.Query<MyStateData>(stateName);

        logger.LogInformation("Queried state data: Val1={val1} Val2={val2}.", result.Data.Val1, result.Data.Val2);

        // Unsubscribe from events.

        subscription.Dispose();

        logger.LogInformation("Unsubscribed from events.");

        // Delete the state.
        // Note: This this will cause subscribers to receive an event with a zero-length buffer,
        // which is why we unsubscribed first.

        wnfClient.Delete(stateName);

        logger.LogInformation("Deleted state.");
    }

    struct MyStateData
    {
        public bool Val1;
        public int Val2;
    }
}

