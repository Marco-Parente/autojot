using AutoJot.ServiceDefaults;
using Core.Shared;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Trace;

namespace UnitTest;

public class ServiceDefaultsTest
{
    private static IHost BuildHost()
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["OTEL_EXPORTER_OTLP_ENDPOINT"] = "http://localhost:4317",
            }
        );
        builder.AddServiceDefaults();

        return builder.Build();
    }

    [Fact]
    public void Tracing_ListensToTheAutoJotActivitySource()
    {
        // An ActivitySource nobody subscribed to silently produces no spans, which looks exactly
        // like a working setup until you go looking for traces. StartActivity returning non-null
        // is the proof that something is listening.
        using var host = BuildHost();
        _ = host.Services.GetRequiredService<TracerProvider>();

        using var activity = AutoJotDiagnostics.ActivitySource.StartActivity("test");

        Assert.NotNull(activity);
        Assert.Equal(AutoJotDiagnostics.ActivitySourceName, activity.Source.Name);
    }

    [Fact]
    public void Exporter_IsSkippedWhenNoCollectorIsConfigured()
    {
        // Running the worker without the Aspire host should not fail for want of a collector.
        var builder = Host.CreateEmptyApplicationBuilder(null);
        builder.AddServiceDefaults();

        using var host = builder.Build();

        Assert.NotNull(host.Services.GetRequiredService<TracerProvider>());
    }
}
