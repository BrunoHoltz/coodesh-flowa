# Arquitetura

## Overview

O sistema é composto de duas aplicações:

- Client: Envia comandos FIX (NewOrder, Cancel) e consulta via Http (OrdersSnapshot)
- Server: Processa comandos FIX (NewOrder, Cancel) e Http (OrdersSnapshot) e gerencia o acesso ao dataset de ordens.Ambiente

## Stack

* C# .NET 10 - Minimal APIs
* FIX 4.4 - https://quickfixengine.org/n/
* Docker
* Data storage em memória

## Diagrama

```plaintext
+----------------------+
|   Client Application |  -> App tipo proxy, pequeno e sem estado
+----------------------+
| FIX Layer            |  -> Parse/Serialize FIX
| Application Layer    |  -> Casos de uso (Create, Cancel, Snapshot)
+----------------------+
        |
        v
+----------------------+
|   Server Application |
+----------------------+
| FIX Layer            |  -> Host + Parse/Serialize FIX
| Application Layer    |  -> Casos de uso (Create, Cancel, Snapshot)
| Domain               |  -> Entidades do sistema (Order, OrderStatus, OrderSide e OrderBook)
| Repository (In-Mem)  |  -> Repositório em memória
+----------------------+
        |
        v
Response (ExecutionReport / OrdersSnapshot)
```

## Características

* Processamento apenas em memória
* Otimizado para alto throughput e baixa latência (~< 1ms)
* Datastores com input multiplo para garantir O(1) na leitura do (OrdersSnapshot) considerando sua característica de agrupamento e ordenação
* Otimizado para evitar pressão de memória:
  * Poucas transformações de dados
  * Validações na camada de negócio
  * Expiraçao e remoção automática de dados antigos (ex Ordens canceladas)
