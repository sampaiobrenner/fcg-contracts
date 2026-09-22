# Contratos de integracao - FCG Fase 2

> **Status: proposta (v0.1.0).** Aprovacao pendente na historia FD-01 (sampaiobrenner/PosTech.Fiap.CloudGames#7).
> Apos aprovacao pelos 5 membros o pacote e versionado como `1.0.0`. Mudancas somente via PR neste repositorio.

## 1. Eventos v1 (`Fcg.Contracts.Events.V1`)
```csharp
public sealed record UserCreatedEvent(Guid EventId, DateTime OccurredAt, Guid UserId, string Name, string Email);

public sealed record OrderPlacedEvent(Guid EventId, DateTime OccurredAt, Guid OrderId, Guid UserId, Guid GameId, decimal Price);

public enum PaymentStatus { Approved = 1, Rejected = 2 }

public sealed record PaymentProcessedEvent(Guid EventId, DateTime OccurredAt, Guid OrderId, Guid UserId, Guid GameId, decimal Price, PaymentStatus Status, string? Reason);
```
- Cada record com `[MessageUrn("fcg:<nome>:v1")]` e `[EntityName("fcg.<nome>.v1")]` (roteamento independente de namespace).
- `OrderId` e o `CorrelationId` do fluxo de compra.
- Datas em UTC; `decimal` com 2 casas; enums serializados como string.
- Evolucao: apenas campos opcionais adicionados. Quebra = novo `V2` em paralelo (parallel change).

## 2. Topologia RabbitMQ (MassTransit 8.x - a v9 e comercial)
| Evento | Publicador | Fila consumidora |
|---|---|---|
| `UserCreatedEvent` | Users | `notifications.user-created` |
| `OrderPlacedEvent` | Catalog | `payments.order-placed` |
| `PaymentProcessedEvent` | Payments | `catalog.payment-processed`, `notifications.payment-processed` |

Padrao de consumo: retry imediato 3x + redelivery 5s/15s/30s, DLQ `_error` do MassTransit, consumidores idempotentes por `OrderId`/`EventId`, publicacao via EF Core Outbox do MassTransit.

## 3. Autenticacao
- UsersAPI **emite** o JWT; demais APIs **apenas validam** (chave simetrica compartilhada).
- Claims: `sub` (UserId), `email`, `name`, `role` (`User` | `Administrator`).
- `Jwt__Issuer=fcg-users-api`, `Jwt__Audience=fcg`, `Jwt__Key` em Secret.

## 4. Convencoes de runtime
| Item | Valor |
|---|---|
| Porta do container | `8080` (`ASPNETCORE_HTTP_PORTS=8080`, imagem non-root) |
| Services K8s | `users-api:80`, `catalog-api:80`, `payments-api:80`, `notifications-api:80`, `rabbitmq:5672`, `postgres:5432` |
| Health | `/health/live`, `/health/ready` (ready checa DB + RabbitMQ) |
| Banco | 1 Postgres, 1 database por servico: `fcg_users`, `fcg_catalog`, `fcg_payments`, `fcg_notifications` |
| ConfigMap | `RabbitMq__Host`, `RabbitMq__VirtualHost`, `Messaging__Queues__*`, `Jwt__Issuer`, `Jwt__Audience` |
| Secret | `ConnectionStrings__Default`, `RabbitMq__Username`, `RabbitMq__Password`, `Jwt__Key` |
| Erros HTTP | `ProblemDetails` (RFC 7807) |

## 5. Contratos REST novos
- Catalog: `POST /api/v1/orders {gameId}` -> `202 {orderId, status: "Pending", price}`; `GET /api/v1/orders/{id}`; `GET /api/v1/library`.
- Payments: `GET /api/v1/payments/{orderId}`.
- Regra de simulacao: `Price <= Payments__ApprovalLimit` (default 300) -> Approved; senao Rejected.
