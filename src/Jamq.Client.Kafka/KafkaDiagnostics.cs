namespace Jamq.Client.Kafka;

internal static class KafkaDiagnostics
{
    // Entities
    private const string Topic = nameof(Topic);

    // Status
    private const string Consumed = nameof(Consumed);

    private static string Combine(params string[] parts) => string.Join(".", parts);

    public static readonly string TopicConsumed = Combine(Topic, Consumed);
}