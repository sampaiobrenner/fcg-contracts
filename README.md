# fcg-contracts

Contratos de integracao da plataforma **FIAP Cloud Games (FCG) - Fase 2**, distribuidos como o pacote NuGet `Fcg.Contracts`.

Fonte unica de verdade para: eventos trafegados no RabbitMQ, nomes de filas e claims do JWT. A especificacao completa (topologia, convencoes de runtime, contratos REST) esta em [docs/contratos.md](docs/contratos.md).

## Conteudo

| Namespace | Tipos |
|---|---|
| `Fcg.Contracts.Events.V1` | `UserCreatedEvent`, `OrderPlacedEvent`, `PaymentProcessedEvent`, `PaymentStatus` |
| `Fcg.Contracts.Messaging` | `QueueNames` |
| `Fcg.Contracts.Security` | `FcgClaimTypes`, `FcgRoles` |

Payloads de exemplo em [samples/](samples) - validados pelos testes.

## Por que um pacote compartilhado

O MassTransit roteia mensagens pelo tipo. Cada evento declara `[MessageUrn]` e `[EntityName]` fixos, entao o roteamento nao depende de namespace, e o pacote unico garante que publicador e consumidor usem exatamente o mesmo contrato.

| Evento | Publicador | Filas consumidoras |
|---|---|---|
| `UserCreatedEvent` | users-api | `notifications.user-created` |
| `OrderPlacedEvent` | catalog-api | `payments.order-placed` |
| `PaymentProcessedEvent` | payments-api | `catalog.payment-processed`, `notifications.payment-processed` |

## Versionamento

SemVer. Evolucao somente aditiva (campos opcionais). Mudanca incompativel = novo namespace `V2` publicado em paralelo ao `V1` (parallel change) ate que todos os consumidores migrem.

## Build

```bash
dotnet test
dotnet pack src/Fcg.Contracts -c Release -o artifacts
```
