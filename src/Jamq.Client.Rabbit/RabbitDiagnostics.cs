namespace Jamq.Client.Rabbit;

internal static class RabbitDiagnostics
{
    // Entities
    private const string Connection = nameof(Connection);
    private const string Channel = nameof(Channel);
    private const string Consumer = nameof(Consumer);
    private const string Message = nameof(Message);

    // Status
    private const string Open = nameof(Open);
    private const string Disrupt = nameof(Disrupt);
    private const string Restore = nameof(Restore);
    private const string Close = nameof(Close);
    private const string Cancel = nameof(Cancel);
    private const string Receive = nameof(Receive);
    private const string Timeout = nameof(Timeout);

    private static string Combine(params string[] parts) => string.Join(".", parts);

    public static readonly string ConnectionOpen = Combine(Connection, Open);
    public static readonly string ConnectionDisrupt = Combine(Connection, Disrupt);
    public static readonly string ConnectionClose = Combine(Connection, Close);

    public static readonly string ChannelOpen = Combine(Channel, Open);
    public static readonly string ChannelDisrupt = Combine(Channel, Disrupt);
    public static readonly string ChannelRestore = Combine(Channel, Restore);
    public static readonly string ChannelClose = Combine(Channel, Close);

    public static readonly string ConsumerCancelDisrupt = Combine(Consumer, Cancel, Disrupt);
    public static readonly string ConsumerCancelTimeout = Combine(Consumer, Cancel, Timeout);
    public static readonly string MessageReceiveDisrupt = Combine(Message, Receive, Disrupt);
}