using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Fcg.Contracts.Events.V1;
using MassTransit;

namespace Fcg.Contracts.Tests.Events.V1;

public class EventSamplesTests
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private static T ReadSample<T>(string fileName)
        => JsonSerializer.Deserialize<T>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "samples", fileName)), SerializerOptions)!;

    [Fact]
    public void UserCreatedEvent_DeveDesserializarAmostra_QuandoPayloadDoContrato()
    {
        var evt = ReadSample<UserCreatedEvent>("user-created.json");

        evt.UserId.Should().NotBeEmpty();
        evt.Name.Should().Be("Ada Lovelace");
        evt.Email.Should().Be("ada@fcg.dev");
    }

    [Fact]
    public void OrderPlacedEvent_DeveDesserializarAmostra_QuandoPayloadDoContrato()
    {
        var evt = ReadSample<OrderPlacedEvent>("order-placed.json");

        evt.OrderId.Should().NotBeEmpty();
        evt.Price.Should().Be(199.90m);
    }

    [Fact]
    public void PaymentProcessedEvent_DeveDesserializarStatusComoTexto_QuandoPayloadDoContrato()
    {
        var evt = ReadSample<PaymentProcessedEvent>("payment-processed.json");

        evt.Status.Should().Be(PaymentStatus.Approved);
        evt.Reason.Should().BeNull();
    }

    [Theory]
    [InlineData(typeof(UserCreatedEvent), "urn:message:fcg:user-created:v1")]
    [InlineData(typeof(OrderPlacedEvent), "urn:message:fcg:order-placed:v1")]
    [InlineData(typeof(PaymentProcessedEvent), "urn:message:fcg:payment-processed:v1")]
    public void MessageUrn_DeveSerFixo_QuandoEventoV1(Type eventType, string expectedUrn)
    {
        eventType.GetCustomAttribute<MessageUrnAttribute>()!.Urn.ToString().Should().Be(expectedUrn);
    }
}
