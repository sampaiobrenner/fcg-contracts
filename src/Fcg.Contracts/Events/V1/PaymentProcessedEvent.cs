using MassTransit;

namespace Fcg.Contracts.Events.V1;

[MessageUrn("fcg:payment-processed:v1")]
[EntityName("fcg.payment-processed.v1")]
public sealed record PaymentProcessedEvent(
    Guid EventId,
    DateTime OccurredAt,
    Guid OrderId,
    Guid UserId,
    Guid GameId,
    decimal Price,
    PaymentStatus Status,
    string? Reason);
