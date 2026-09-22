using MassTransit;

namespace Fcg.Contracts.Events.V1;

[MessageUrn("fcg:user-created:v1")]
[EntityName("fcg.user-created.v1")]
public sealed record UserCreatedEvent(
    Guid EventId,
    DateTime OccurredAt,
    Guid UserId,
    string Name,
    string Email);
