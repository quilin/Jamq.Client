namespace RMQ.Client.Abstractions;

/// <summary>
/// Properties for connection to the broker
/// </summary>
public class RabbitConnectionParameters
{
    /// <summary>
    /// Create default connection parameters for local testing
    /// </summary>
    public RabbitConnectionParameters() : this("amqp://localhost:5672/", "guest", "guest")
    {
    }

    /// <summary>
    /// Create default connection parameters with specified host and basic credentials
    /// </summary>
    /// <param name="endpointUrl">Host and port of broker</param>
    /// <param name="userName">User name</param>
    /// <param name="password">Password</param>
    public RabbitConnectionParameters(string endpointUrl, string userName, string password)
    {
        EndpointUrl = endpointUrl;
        UserName = userName;
        Password = password;
    }

    /// <summary>
    /// Endpoint URL
    /// </summary>
    public string EndpointUrl { get; }

    /// <summary>
    /// User name
    /// </summary>
    public string UserName { get; }

    /// <summary>
    /// Password
    /// </summary>
    public string Password { get; }

    /// <summary>
    /// Channels per connection limit
    /// </summary>
    public int ChannelsLimit { get; init; } = 256;

    /// <summary>
    /// Connections per pool limit
    /// </summary>
    public int PoolSize { get; init; } = 16;
}