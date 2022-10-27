using System.Text.Json;
using Confluent.Kafka;

namespace Jamq.Client.Kafka.Defaults;

public abstract class DefaultKafkaSerializer
{
    protected static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
}

public class DefaultKafkaSerializer<TValue> : DefaultKafkaSerializer,
    ISerializer<TValue>,
    IDeserializer<TValue>
{
    public byte[] Serialize(TValue data, SerializationContext context) => 
        JsonSerializer.SerializeToUtf8Bytes(data, typeof(TValue), SerializerOptions);

    public TValue Deserialize(ReadOnlySpan<byte> data, bool isNull, SerializationContext context) => 
        JsonSerializer.Deserialize<TValue>(data, SerializerOptions)!;
}