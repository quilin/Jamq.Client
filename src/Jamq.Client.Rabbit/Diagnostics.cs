namespace Jamq.Client.Rabbit;

internal static class Diagnostics
{
    // Entities
    private const string Connection = nameof(Connection);
    private const string Channel = nameof(Channel);

    // Status
    private const string Open = nameof(Open);
    private const string Disrupt = nameof(Disrupt);
    private const string Restore = nameof(Restore);
    private const string Close = nameof(Close);

    private static string Combine(params string[] parts) => string.Join(".", parts);

    public static readonly string ConnectionOpen = Combine(Connection, Open);
    public static readonly string ConnectionDisrupt = Combine(Connection, Disrupt);
    public static readonly string ConnectionClose = Combine(Connection, Close);

    public static readonly string ChannelOpen = Combine(Channel, Open);
    public static readonly string ChannelDisrupt = Combine(Channel, Disrupt);
    public static readonly string ChannelRestore = Combine(Channel, Restore);
    public static readonly string ChannelClose = Combine(Channel, Close);
}