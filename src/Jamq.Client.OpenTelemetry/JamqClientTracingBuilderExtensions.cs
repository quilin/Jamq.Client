using Jamq.Client.Abstractions.Diagnostics;
using OpenTelemetry.Trace;

namespace Jamq.Client.OpenTelemetry;

public static class TracerProviderBuilderExtensions
{
    public static TracerProviderBuilder AddJamqClientInstrumentation(this TracerProviderBuilder builder) => 
        builder.AddSource(Event.SourceName);
}