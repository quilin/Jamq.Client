﻿using Jamq.Client.Abstractions.Diagnostics;
using RabbitMQ.Client;

namespace Jamq.Client.Rabbit.Connection.Adapters;

internal class ChannelAdapter : IChannelAdapter
{
    private const ushort ForceTerminationCode = 320;
    private const string ForceTerminationText = "CONNECTION_FORCED - Closed via management plugin";

    public ChannelAdapter(IModel model)
    {
        Channel = model;
        Channel.ModelShutdown += ResolveShutdown!;
    }

    private void ResolveShutdown(object sender, ShutdownEventArgs e)
    {
        var channel = (IModel) sender;
        Event.WriteIfEnabled(RabbitDiagnostics.ChannelDisrupt, new { Channel = channel });
        if (e.Initiator != ShutdownInitiator.Peer ||
            e.ReplyCode == ForceTerminationCode && e.ReplyText == ForceTerminationText)
        {
            return;
        }

        OnDisrupted?.Invoke(this, new ChannelDisruptedEventArgs());
    }

    public IModel Channel { get; }

    public event EventHandler<ChannelDisruptedEventArgs>? OnDisrupted;

    public void Dispose()
    {
        Channel.ModelShutdown -= ResolveShutdown!;
        Event.WriteIfEnabled(RabbitDiagnostics.ChannelClose, new { Channel });
        Channel.Close();
    }
}