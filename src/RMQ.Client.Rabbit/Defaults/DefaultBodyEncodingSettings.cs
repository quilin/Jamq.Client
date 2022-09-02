using System.Text.Json;

namespace RMQ.Client.Rabbit.Defaults;

internal static class DefaultBodyEncodingSettings
{
    public static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
}