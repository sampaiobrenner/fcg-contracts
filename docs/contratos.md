# Contratos de integracao - FCG Fase 2

> **Status: aprovado - v1.0.0 (22/09/2026).** Historia FD-01 no [Azure Boards](https://dev.azure.com/PosTech-Fiap-CloudGames/PosTech-Fiap-CloudGames/_workitems/edit/25). Pacote `PosTech.Fiap.CloudGames.Contracts` **1.0.0**.
> Mudancas somente via PR neste repositorio. Evolucao so aditiva (campos opcionais, nova minor); quebra = novo namespace `V2` em paralelo.

## 1. Eventos v1 (`Fcg.Contracts.Events.V1`)
```csharp
public sealed record UserCreatedEvent(Guid EventId, DateTime OccurredAt, Guid UserId, string Name, string Email);

public sealed record OrderPlacedEvent(Guid EventId, DateTime OccurredAt, Guid OrderId, Guid UserId, Guid GameId, decimal Price);

public enum PaymentStatus { Approved = 1, Rejected = 2 }

public sealed record PaymentProcessedEvent(Guid EventId, DateTime OccurredAt, Guid OrderId, Guid UserId, Guid GameId, decimal Price, PaymentStatus Status, string? Reason);
```
- Cada record com `[MessageUrn("fcg:<nome>:v1")]` e `[EntityName("fcg.<nome>.v1")]` (roteamento independente de namespace).
- `EventId` novo (`Guid.CreateVersion7()`) a cada publicacao. `OrderId` e o `CorrelationId` do fluxo de compra.
- Datas em UTC; `decimal` com 2 casas; enums serializados como string.
- Evolucao: apenas campos opcionais adicionados. Quebra = novo `V2` em paralelo (parallel change).

## 2. Topologia RabbitMQ (MassTransit 8.x - a v9 e comercial)
| Evento | Publicador | Fila consumidora |
|---|---|---|
| `UserCreatedEvent` | Users | `notifications.user-created` |
| `OrderPlacedEvent` | Catalog | `payments.order-placed` |
| `PaymentProcessedEvent` | Payments | `catalog.payment-processed`, `notifications.payment-processed` |

Exchanges: uma por evento, com o nome do `[EntityName]` (`fcg.user-created.v1`, `fcg.order-placed.v1`, `fcg.payment-processed.v1`), criadas pelo MassTransit.

Filas: o enunciado da Fase 2 pede ConfigMap para configuracoes nao sensiveis e cita nomes de filas como exemplo. Cada consumidor le o nome da fila da configuracao `Messaging:Queues:<Fila>` (variavel `Messaging__Queues__<Fila>`, vinda do ConfigMap) e usa a constante de `Fcg.Contracts.Messaging.QueueNames` como default, aplicando o valor em `ConsumerDefinition.EndpointName` (nunca o nome gerado pelo formatter).

| Fila | Variavel (ConfigMap) | Default (`QueueNames`) | Servico |
|---|---|---|---|
| `notifications.user-created` | `Messaging__Queues__NotificationsUserCreated` | `NotificationsUserCreated` | Notifications |
| `payments.order-placed` | `Messaging__Queues__PaymentsOrderPlaced` | `PaymentsOrderPlaced` | Payments |
| `catalog.payment-processed` | `Messaging__Queues__CatalogPaymentProcessed` | `CatalogPaymentProcessed` | Catalog |
| `notifications.payment-processed` | `Messaging__Queues__NotificationsPaymentProcessed` | `NotificationsPaymentProcessed` | Notifications |

Exemplo:
```csharp
public sealed class OrderPlacedConsumerDefinition : ConsumerDefinition<OrderPlacedConsumer>
{
    public OrderPlacedConsumerDefinition(IConfiguration configuration)
        => EndpointName = configuration[$"Messaging:Queues:{nameof(QueueNames.PaymentsOrderPlaced)}"] ?? QueueNames.PaymentsOrderPlaced;
}
```

Especificacao AsyncAPI 3.0 dos eventos: [asyncapi.yaml](asyncapi.yaml).

Padrao de consumo:
- Retry em memoria `UseMessageRetry` com intervalos 1s/5s/15s/30s, ignorando `BusinessException` e `ValidationException`. Esgotado, a mensagem vai para a fila `<fila>_error` do MassTransit.
- Nao usamos redelivery atrasado: ele exige o plugin `rabbitmq_delayed_message_exchange`, ausente na imagem `rabbitmq:4-management-alpine`.
- Publicacao via EF Core Outbox do MassTransit (evento gravado na mesma transacao do dado de negocio).
- Consumidores idempotentes: Inbox do MassTransit (por `MessageId`) + regra de negocio por `OrderId`/`EventId` descrita em cada servico.

## 3. Autenticacao
- UsersAPI **emite** o JWT; demais APIs **apenas validam** (chave simetrica compartilhada).
- Assinatura HS256. Claims: `sub` (UserId), `email`, `name`, `role` (`User` | `Administrator`), `jti`. Usar as claims curtas (constantes `FcgClaimTypes`), nunca as URIs de `System.Security.Claims.ClaimTypes`.
- `Jwt__Issuer=fcg-users-api`, `Jwt__Audience=fcg`, `Jwt__Key` em Secret (>= 32 caracteres, igual em todos os servicos).
- Expiracao: `Jwt__ExpirationMinutes` (apenas UsersAPI, ConfigMap, default `60`).

## 4. Convencoes de runtime
| Item | Valor |
|---|---|
| Porta do container | `8080` (`ASPNETCORE_HTTP_PORTS=8080`, imagem non-root) |
| Services K8s | `users-api:80`, `catalog-api:80`, `payments-api:80`, `notifications-api:80`, `rabbitmq:5672`, `postgres:5432` |
| Health | `/health/live`, `/health/ready` (ready checa DB + RabbitMQ) |
| Banco | 1 Postgres, 1 database por servico: `fcg_users`, `fcg_catalog`, `fcg_payments`, `fcg_notifications` |
| ConfigMap | `ASPNETCORE_ENVIRONMENT`, `Database__ApplyMigrationsOnStartup`, `RabbitMq__Host`, `RabbitMq__VirtualHost`, `Jwt__Issuer`, `Jwt__Audience`; `Messaging__Queues__<Fila>` dos consumidores; UsersAPI: `Jwt__ExpirationMinutes`; PaymentsAPI: `Payments__ApprovalLimit` |
| Secret | `ConnectionStrings__Default`, `RabbitMq__Username`, `RabbitMq__Password`, `Jwt__Key` |
| Erros HTTP | `ProblemDetails` (RFC 7807): 400 validacao, 401 nao autenticado, 403 sem permissao, 404 nao encontrado, 409 conflito, 422 regra de negocio |
| JSON REST | camelCase, enums como string, datas ISO 8601 UTC |

## 5. Contratos REST novos

Todos exigem token (`Authorization: Bearer <jwt>`).

### Catalog: `POST /api/v1/orders`
Request `{ "gameId": "uuid" }`.

| Status | Quando | Corpo |
|---|---|---|
| `202 Accepted` + `Location: /api/v1/orders/{orderId}` | pedido criado | `{ "orderId": "uuid", "status": "Pending", "price": 199.90 }` |
| `404` | jogo inexistente ou inativo | ProblemDetails |
| `409` | jogo ja na biblioteca ou pedido `Pending` para o mesmo usuario/jogo | ProblemDetails |

`price` e o preco efetivo (promocao ativa de maior desconto aplicada).

### Catalog: `GET /api/v1/orders/{id}`
`200` `{ "orderId", "userId", "gameId", "price", "status": "Pending|Completed|Rejected", "reason": null, "createdAt", "updatedAt" }`. Somente o dono ou `Administrator`; qualquer outro recebe `404`.

### Catalog: `GET /api/v1/library`
`200` `[ { "gameId", "title", "genre", "pricePaid", "acquiredAt" } ]` do usuario do token.

### Payments: `GET /api/v1/payments/{orderId}`
`200` `{ "paymentId", "orderId", "userId", "gameId", "amount", "status": "Approved|Rejected", "reason", "processedAt" }`. Somente o dono ou `Administrator`; senao `404`.

### Regra de simulacao
`Price <= Payments__ApprovalLimit` (default `300`) -> `Approved` com `Reason = null`; senao `Rejected` com `Reason` preenchido.

## 6. Mudancas neste contrato
Somente via PR neste repositorio, com aprovacao do time. Depois do merge, publicar nova versao do pacote (tag `v*`).
