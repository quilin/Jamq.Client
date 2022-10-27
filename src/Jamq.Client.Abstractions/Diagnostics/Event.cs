using System.Diagnostics;

namespace Jamq.Client.Abstractions.Diagnostics;

public static class Event
{
    public const string SourceName = "Jamq.Client";

    private static readonly DiagnosticSource DiagnosticSource = new DiagnosticListener(SourceName);
    internal static readonly ActivitySource ActivitySource = new(SourceName);

    internal static void WriteIfEnabled(string name, object? value)
    {
        if (DiagnosticSource.IsEnabled(SourceName))
        {
            DiagnosticSource.Write($"{SourceName}.{name}", value);
        }
    }

    // Activities
    internal const string Produce = nameof(Produce);
    internal const string Consume = nameof(Consume);
}