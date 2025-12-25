# Redis Implementation Guide

This document details the Redis implementation for the `yQuant` system, serving as a reference for future development and maintenance.

## 1. Overview
Redis is used in `yQuant` in two distinct logical roles, which are configured via `appsecrets.json`:

1.  **Messaging Redis (`Redis:Message`)**: 
    - Facilitates real-time, event-driven communication (Pub/Sub: `signal`, `order`, etc.).
    - Stores environment-specific state (Heartbeats, Trade logs, Market data cache).
2.  **Token Redis (`Redis:Token`)**:
    - Shared across all environments (Production, Staging, Local Dev).
    - Specifically caches **KIS Access Tokens** to comply with daily issuance limits.

## 2. Connection & Configuration
### 2.1. Messaging & Ops Redis
- **Configuration Path**: `Redis:Message` in `appsecrets.json`
- **Library**: `StackExchange.Redis` via `IRedisService`
- **Scope**: Specific to each deployment environment.

### 2.2. Global Token Redis
- **Configuration Path**: `Redis:Token` in `appsecrets.json`
- **Library**: `StackExchange.Redis` via `ITokenRedisService`
- **Scope**: Shared across ALL environments (Production, Staging, Local).

> **Note**: These two can point to the same Redis instance if desired. The key namespacing is designed to avoid conflicts.

## 3. Schema Reference

### 3.1. Naming Convention
- **Entity-First**: Keys start with the domain entity name (e.g., `account`, `stock`).
- **No Prefixes**: Project prefixes like `yQuant:` are omitted.
- **Separator**: Colon (`:`) is used as a separator.

### 3.2. Channels (Pub/Sub)
| Channel | Publisher | Subscriber | Payload | Description |
| :--- | :--- | :--- | :--- | :--- |
| `signal` | `App.Webhook` | `App.OrderManager` | `Signal` | Raw trading signals from TradingView. |
| `order` | `App.OrderManager`, `App.Web`, `App.Console` | `App.BrokerGateway` | `Order` | Executable orders. |
| `execution` | `App.BrokerGateway` | `App.Web`, `Notification` | `OrderResult` | Execution results (fill/reject). |
| `query` | `App.Web`, `App.Console` | `App.BrokerGateway` | `Query` | Information requests (price, account data, etc.). BrokerGateway fetches from Broker and updates Redis. |

### 3.3. Keys (State)
| Key Pattern | Type | Writer | Reader | Description |
| :--- | :--- | :--- | :--- | :--- |
| `account:{Alias}` | Hash | `App.BrokerGateway` | `App.Web`, `App.OrderManager` | Static account metadata (Number, Broker, Active status). Secrets are **NOT** stored. |
| `account:index` | Set | `App.BrokerGateway` | `App.Web` | Set of all active account aliases. Used for discovery. |
| `deposit:{Alias}` | Hash | `App.BrokerGateway` | `App.Web`, `App.OrderManager` | Real-time balance per currency (Field: `USD`, Value: `1000.00`). |
| `position:{Alias}` | Hash | `App.BrokerGateway` | `App.Web`, `App.OrderManager` | Real-time positions (Field: `AAPL`, Value: `Position` JSON). |
| `stock:{Ticker}` | Hash | `App.Console` (catalog), `App.BrokerGateway` | `App.Web`, `App.Console` | Merged static info (Name, Exchange) and dynamic market data (Price, Change). |
| `scheduled:{Alias}` | String | `App.Web` | `App.OrderManager` | List of scheduled orders (JSON Array). Stores schedule config (DaysOfWeek, TimeMode). |
| `Token:KIS:{Alias}` | JSON | `App.BrokerGateway` | All Environments | **(Token Redis)** Shared KIS access token and expiration. |

## 4. Data Flows

### 4.1. Order Execution Flow
1.  **Signal Source**: `App.Webhook` receives a webhook → publishes to `signal`.
2.  **Composition**: `App.OrderManager` receives `signal` → validates & composes `Order` → publishes to `order`.
3.  **Execution**: `App.BrokerGateway` receives `order` → sends to Broker API → publishes result to `execution`.
4.  **Feedback**: `App.Web` listens to `execution` for real-time updates.

### 4.2. Market Data Flow
1.  **Master Data**: `App.Console` (catalog command) runs daily → writes static data (Name, Exchange) to `stock:{Ticker}` with TTL.
2.  **Real-time Price**: `App.BrokerGateway` fetches prices periodically → updates `price`, `changeRate` fields in `stock:{Ticker}` and refreshes TTL.
3.  **Consumption**: `App.Web` reads `stock:{Ticker}` to display portfolio values.

### 4.3. Account Sync Flow
1.  **Startup**: `App.BrokerGateway` connects to Broker → writes `account:{Alias}` and adds to `account:index`.
2.  **Hybrid Sync**:
    -   **Full Sync**: Polls Broker for Assets/Positions if `AccountUpdateIntervalMinutes` has passed since last sync.
    -   **Local Update**: On order execution, immediately updates `deposit:{Alias}` and `position:{Alias}` locally using estimated values (reducing Broker API reliance).
3.  **Aggregation**: `App.OrderManager` reads all three keys (`account`, `deposit`, `position`) to reconstruct the full `Account` object for risk checks.

## 5. Maintenance Guidelines

### Adding New Entities
- Follow the **Entity-First** convention.
- Use **Hashes** for objects to allow partial updates.
- Use **Sets** for indices if discovery is needed.

### TTL Strategy
- **Stocks**: Use TTL (e.g., 25h) to automatically clean up delisted or inactive stocks.
- **Accounts**: Generally persistent, but consider TTL if dynamic account provisioning is introduced.

### Security
- **Never store API Secrets** (AppKey, AppSecret) in Redis.
- Use Redis ACLs in production if shared.
