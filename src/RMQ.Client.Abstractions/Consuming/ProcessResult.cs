namespace RMQ.Client.Abstractions.Consuming;

/// <summary>
/// Result of message processing that suggests the reply strategy to broker
/// </summary>
public enum ProcessResult
{
    /// <summary>
    /// Successfully processed, broker should receive ack/commit
    /// </summary>
    Success = 0,

    /// <summary>
    /// Did not process, broker should return message back to the queue
    /// </summary>
    RetryNeeded = 1,

    /// <summary>
    /// Did not process, broker should reject message
    /// </summary>
    Failure = 2
}