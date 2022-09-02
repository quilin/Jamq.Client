using System.Text.Json;

namespace Jamq.Client.Rabbit.Defaults;

internal static class DefaultBodyEncodingSettings
{
    public static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
}