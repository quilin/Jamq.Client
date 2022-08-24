using System.Text.Json;

namespace RMQ.Client.Defaults;

internal static class DefaultBodyEncodingSettings
{
    public static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
}