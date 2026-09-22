using MassTransit;

namespace Fcg.Contracts.Events.V1;

[MessageUrn("fcg:order-placed:v1")]
[EntityName("fcg.order-placed.v1")]
public sealed record OrderPlacedEvent(
    Guid EventId,
    DateTime OccurredAt,
    Guid OrderId,
    Guid UserId,
    Guid GameId,
    decimal Price);
