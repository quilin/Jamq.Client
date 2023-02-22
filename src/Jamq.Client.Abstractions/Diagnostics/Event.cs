using System.Diagnostics;

namespace Jamq.Client.Abstractions.Diagnostics;

public static class Event
{
    public const string SourceName = "Jamq.Client";
    public const string VersionName = "0.8.0";

    private static readonly DiagnosticSource DiagnosticSource = new DiagnosticListener(SourceName);
    internal static readonly ActivitySource ActivitySource = new(SourceName, VersionName);

    internal static void WriteIfEnabled(string name, object? value)
    {
        if (DiagnosticSource.IsEnabled(SourceName))
        {
            DiagnosticSource.Write($"{SourceName}.{name}", value);
        }
    }

    // Activities
    internal static string Produce(string exchange) => Combine(nameof(Produce), exchange);
    internal static string Consume(string queue, string consumerTag) => Combine(nameof(Consume), queue, consumerTag);

    private static string Combine(params string[] parts) => $"{SourceName}.{string.Join(".", parts)}";
}