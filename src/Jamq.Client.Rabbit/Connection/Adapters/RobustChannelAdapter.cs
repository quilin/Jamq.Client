using Jamq.Client.Abstractions.Diagnostics;
using RabbitMQ.Client;

namespace Jamq.Client.Rabbit.Connection.Adapters;

internal class RobustChannelAdapter : IChannelAdapter
{
    private readonly IModel channel;

    public RobustChannelAdapter(
        IModel channel)
    {
        this.channel = channel;
        channel.ModelShutdown += ResolveShutdown;
    }

    private const ushort ForceTerminationCode = 320;
    private const string ForceTerminationText = "CONNECTION_FORCED - Closed via management plugin";

    public IModel Channel => channel;
    public event EventHandler<ChannelDisruptedEventArgs>? OnDisrupted;

    private void ResolveShutdown(object sender, ShutdownEventArgs args)
    {
        Event.WriteIfEnabled(RabbitDiagnostics.ChannelDisrupt, new { Channel });
        if (args.Initiator is not ShutdownInitiator.Peer ||
            args is { ReplyCode: ForceTerminationCode, ReplyText: ForceTerminationText })
        {
            return;
        }
        
        OnDisrupted?.Invoke(this, new ChannelDisruptedEventArgs());
    }

    public void Dispose()
    {
        Channel.ModelShutdown -= ResolveShutdown;
        Event.WriteIfEnabled(RabbitDiagnostics.ChannelClose, new { Channel });
        Channel.Dispose();
    }
}