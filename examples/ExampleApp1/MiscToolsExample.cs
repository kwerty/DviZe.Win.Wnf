using Kwerty.DviZe.Linq;
using Kwerty.DviZe.Win.Wnf;
using Kwerty.DviZe.Win.Wnf.Misc;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ExampleApp1;

public class MiscToolsExample(ILogger<MiscToolsExample> logger, IHostEnvironment env) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var csvPath = Path.Combine(env.ContentRootPath, "wnf.csv");
        using var writer = new StreamWriter(csvPath);
        writer.WriteLine("StateName,MaxStateSize,InternalName,InternalDescription,Version,Lifetime,Scope,IsDataPersistent");

        var registrations = WnfStateRegistrationReader.GetAll(WnfLifetime.WellKnown);
        var symbols = WnfStateSymbolDumper.GetAll();
        foreach (var pair in registrations.PairWith(symbols, r => r.StateName, s => s.StateName))
        {
            if (!pair.IsFullPair)
            {
                continue;
            }

            var (registration, symbol) = (pair.Left, pair.Right);
            var stateNameInfo = WnfStateNameInfo.Parse(registration.StateName);

            writer.WriteLine(string.Join(',',
                $"0x{registration.StateName:X}",
                registration.MaxStateSize,
                "\"" + symbol.InternalName.Replace("\"", "\"\"") + "\"",
                "\"" + symbol.InternalDescription.Replace("\"", "\"\"") + "\"",
                stateNameInfo.Version,
                stateNameInfo.Lifetime,
                stateNameInfo.Scope,
                stateNameInfo.IsDataPersistent));
        }

        logger.LogInformation("Saved WNF state info to {CsvPath}", csvPath);
    }
}