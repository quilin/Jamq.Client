using FluentAssertions;
using Jamq.Client.Abstractions.Consuming;
using Jamq.Client.Kafka.Consuming;
using Jamq.Client.Kafka.Defaults;

namespace Jamq.Client.Tests.Kafka;

public class DefaultDiagnosticMiddlewareShould
{
    [Fact]
    public void MatchGenericInterface()
    {
        var middlewareType = typeof(DefaultDiagnosticMiddleware<,>);
        middlewareType.IsGenericType.Should().BeTrue();
        var (success, genericType) = MiddlewareCompiler
            .TryMatchGenericInterface<IConsumerMiddleware<string, string, KafkaConsumerProperties<string, string>>>(
                middlewareType);
        success.Should().BeTrue();
        genericType.Should().Be(typeof(DefaultDiagnosticMiddleware<string, string>));
    }
}