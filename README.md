# FixOrderBooking

Sistema de registro e gerenciamento de ordens baseado no protocolo **FIX 4.4**, composto por um servidor e um cliente, ambos comunicando-se via mensagens FIX e expostos também por APIs HTTP REST.

---

## Stack Tecnológica

- **.NET 10** — runtime e framework principal
- **ASP.NET Core (Minimal APIs)** — endpoints HTTP
- **QuickFIX/n** — engine FIX 4.4 (iniciador e aceitador)
- **NUnit 4** — testes unitários e de integração
- **Docker / Docker Compose** — container e orquestração

```
┌────────────────────────────────────────────────────────────┐
│  Cliente Externo (ex: algoritmo, terminal)                 │
│  FIX initiator  →  porta 5002                              │
└──────────────────────┬─────────────────────────────────────┘
                       │ FIX 4.4
┌──────────────────────▼─────────────────────────────────────┐
│  FixOrderBooking.Client  (porta HTTP 5100 / FIX 5002)      │
│                                                            │
│  FixAcceptorApplication  ←─ recebe ordens/cancels externos │
│         │                                                  │
│  FixClientApplication    ──────────────────────────┐       │
│         │                                          │       │
│  OrderEndpoints  GET /orders/snapshot              │       │
│         └── proxy HTTP → Server :5000              │       │
└──────────────────────────────────────────┬─────────┘       │
                                           │ FIX 4.4         │
┌──────────────────────────────────────────▼─────────────────┐
│  FixOrderBooking.Server  (porta HTTP 5000 / FIX 5001)      │
│                                                            │
│  FixServerApplication   ←── aceita conexão do Client       │
│         │                                                  │
│  OrderBookService       ←── validação e lógica de negócio  │
│         │                                                  │
│  InMemoryOrderRepository ← orders storage                  │
│         │                                                  │
│  OrderBookHttp  GET /orders/active                         │
└────────────────────────────────────────────────────────────┘
```

**Fluxo de uma nova Ordem:**
`Cliente Externo → [FIX] → Client App → [FIX] → Server App → OrderBook`
`Cliente Externo ← [FIX] ← Client App ← [FIX] ← ExecutionReport`

[**Registro de decisões** /docs/adr.md](./docs/adr.md)

---

## Setup

### Pré-requisitos

| Requisito                                                      | Versão mínima |
| -------------------------------------------------------------- | --------------- |
| [Docker Desktop](https://www.docker.com/products/docker-desktop/) | 24+             |
| [.NET SDK](https://dotnet.microsoft.com/download)                 | 10.0            |

> Os binários da aplicação são compilados **dentro dos containers Docker** via multi-stage build — não é necessário o SDK para simplesmente executar.

### Configurações

As variáveis de ambiente relevantes para cada container estão no `docker-compose.yml`. Os valores padrão funcionam para uso local sem alteração.

| Variável             | Container | Padrão                | Descrição                      |
| --------------------- | --------- | ---------------------- | -------------------------------- |
| `ServerBaseUrl`     | Client    | `http://server:5000` | URL interna do Server            |
| `FIX__ServerHost`   | Client    | `server`             | Host FIX do Server               |
| `FIX__ServerPort`   | Client    | `5001`               | Porta FIX do Server              |
| `FIX__AcceptorPort` | Client    | `5002`               | Porta FIX para clientes externos |

Os testes de integração lêem configuração de `tests/FixOrderBooking.IntegrationTests/appsettings.json`, podendo ser sobreposta por variáveis de ambiente com prefixo `INTEGRATION_`.

---

## Execução e Testes

### 1. Restaurar dependências

```bash
dotnet restore
```

### 2. Executar testes unitários (sem Docker)

```bash
# Servidor
dotnet test tests/FixOrderBooking.Server.Tests/

# Cliente
dotnet test tests/FixOrderBooking.Client.Tests/
```

### 3. Subir o ambiente Docker

```bash
docker compose up --build -d
```

Aguarde os healthchecks ficarem `healthy` antes de prosseguir:

```bash
docker compose ps
```

### 4. Executar testes de aceitação (e2e)

```bash
dotnet test tests/FixOrderBooking.IntegrationTests/ --filter "Category=Acceptance"
```

Esses testes verificam o fluxo completo: nova ordem, duplicata, cancelamento, snapshot HTTP, ordenação por preço e FIFO.

### 5. Executar testes de carga (latência e2e)

```bash
dotnet test tests/FixOrderBooking.IntegrationTests/ --filter "Category=Load"
```

### 6. Derrubar o ambiente

```bash
docker compose down --remove-orphans --volumes
```

> **Nota:** o fixture de integração (`DockerComposeFixture`) executa `down` + `up --build` automaticamente ao rodar os testes, garantindo sempre a imagem mais recente.

---

### Resultados do Teste de Carga

O teste envia **100.000 ciclos sequenciais** `NewOrderSingle → ExecutionReport` através da cadeia completa (cliente externo → Client App → Server App → Client App → cliente externo), descartando os primeiros 1.000 como WARMUP (aquecimento).

**SLA exigido:** média < **1,0 ms** por round-trip.

| Métrica | Resultado típico |
| -------- | ----------------- |
| Média   | ~0,15 ms          |
| P50      | ~0,12 ms          |
| P95      | ~0,30 ms          |
| P99      | ~0,70 ms          |
| Máximo  | ~5,00 ms          |
| Falhas   | 0                 |

> Resultados medidos em hardware local (WSL2 / Docker Desktop). Em produção com rede real, adicionar a latência de rede ao valor medido.

---

## O que falta / Próximos passos

| # | Área                                          | Melhoria                                                                                                                                                                                                                                                                                                                                                     | Prioridade |
| - | ---------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ | ---------- |
| 1 | **Persistência**                        | `InMemoryOrderRepository` perde todos os dados ao reiniciar. Em produção pode ser substituido por Redis (estruturas sorted set para o order book) ou um banco relacional como foco em readonly. <br />O estado FIX também é em memória (`MemoryStoreFactory`) — em produção usar `FileStoreFactory` com volume persistente ou store em banco. | 🔴 Alta    |
| 2 | **Autenticação**                       | Nenhuma autenticação nas APIs HTTP nem no FIX (sem `SenderCompID` ACL). Adicionar validação de credenciais FIX e JWT/API Key nos endpoints HTTP.                                                                                                                                                                                                       | 🔴 Alta    |
| 3 | **Múltiplos símbolos com isolamento**  | O order book atual é um único `InMemoryOrderRepository` global. Considerar particionamento por símbolo para reduzir contenção de lock.                                                                                                                                                                                                                | 🟠 Média  |
| 4 | **Observabilidade**                      | Sem métricas exportadas (Prometheus/OpenTelemetry). Adicionar contadores de ordens, latência de processamento FIX, tamanho do order book por símbolo.                                                                                                                                                                                                     | 🟠 Média  |
| 5 | **Cancelamento parcial / modificação** | `OrderCancelReplaceRequest` (FIX tag 35=G) não é tratado. Implementar suporte a modificação de preço/quantidade.                                                                                                                                                                                                                                      | 🟡 Baixa   |
| 6 | **Alta disponibilidade**                 | Ambos os serviços são instância única. Adicionar suporte a múltiplas réplicas do Client com load balancing FIX (FIX session affinity).                                                                                                                                                                                                                 | 🟡 Baixa   |
| 7 | **Interface de visualização**          | Criar um dashboard web simples consumindo `GET /orders/snapshot` via polling ou WebSocket.                                                                                                                                                                                                                                                                | 🟡 Baixa   |
| 8 | **Pipeline CI/CD**                       | Sem pipeline automatizado. Adicionar GitHub Actions: build → unit tests → Docker build → integration tests.                                                                                                                                                                                                                                               | 🟡 Baixa   |
| 9 | **Testes de contrato**                   | Sem validação do contrato do schema JSON entre Client e Server. Adicionar testes de contrato (ex: Pact) para garantir compatibilidade entre as duas APIs.                                                                                                                                                                                                  | 🟡 Baixa   |
