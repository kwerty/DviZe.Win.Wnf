using Kwerty.DviZe.Win.Wnf;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace ExampleApp1;

public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);

        builder.Services.AddLogging(loggingBuilder =>
        {
            loggingBuilder
                .AddFilter("Microsoft.Hosting.Lifetime", LogLevel.None)
                .AddSimpleConsole(opts => opts.SingleLine = true);
        });

        builder.Services.AddSingleton<WnfClient>();

        builder.Services.AddHostedService<GeneralExample1>();
        //builder.Services.AddHostedService<GeneralExample2>();
        //builder.Services.AddHostedService<ShellNotificationCountExample>();
        //builder.Services.AddHostedService<WellKnownStatesExample>();
        //builder.Services.AddHostedService<MiscToolsExample>();

        var host = builder.Build();

        // Restores legacy CTRL_CLOSE_EVENT handling on Windows, which was removed with .NET 10.
        // Without it, closing the console kills the process immediately, bypassing graceful shutdown.
        // https://learn.microsoft.com/en-us/dotnet/core/compatibility/core-libraries/10.0/sigterm-signal-handler
        using var closeHandler = PosixSignalRegistration.Create(PosixSignal.SIGHUP, _ =>
        {
            var lifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();
            lifetime.StopApplication();
            lifetime.ApplicationStopped.WaitHandle.WaitOne();
        });

        await host.RunAsync();
    }
}
