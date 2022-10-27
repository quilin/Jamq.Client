namespace Jamq.Client;

internal static class CommonDiagnostics
{
    private static string Combine(params string[] parts) => string.Join(".", parts);

    // Entities
    private const string Consumer = nameof(Consumer);
    private const string Message = nameof(Message);
    private static readonly string Process = Combine(Message, nameof(Process));

    // Status
    private const string Subscribed = nameof(Subscribed);
    private const string Unsubscribed = nameof(Unsubscribed);
    private const string Published = nameof(Published);
    private const string Received = nameof(Received);
    private const string Complete = nameof(Complete);
    private const string Success = nameof(Success);
    private const string Failure = nameof(Failure);

    public static readonly string ConsumerSubscribed = Combine(Consumer, Subscribed);
    public static readonly string ConsumerUnsubscribed = Combine(Consumer, Unsubscribed);

    public static readonly string MessagePublished = Combine(Message, Published);
    public static readonly string MessageReceived = Combine(Message, Received);

    public static readonly string MessageProcessComplete = Combine(Process, Complete);
    public static readonly string MessageProcessSuccess = Combine(Process, Success);
    public static readonly string MessageProcessFailure = Combine(Process, Failure);
}