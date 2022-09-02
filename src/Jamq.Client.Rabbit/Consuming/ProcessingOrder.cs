namespace Jamq.Client.Rabbit.Consuming;

/// <summary>
/// Queue messages processing order
/// </summary>
public static class ProcessingOrder
{
    /// <summary>
    /// Client doesn't declare QoS
    /// </summary>
    public static readonly PrefetchCount Unmanaged = new(0);

    /// <summary>
    /// Only one message can be processed at a time
    /// </summary>
    public static PrefetchCount Sequential => new(1);

    /// <summary>
    /// Parallel processing
    /// </summary>
    public static ParallelProcessing Parallel => new();
}