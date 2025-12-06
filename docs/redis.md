# Redis Implementation Guide

This document details the Redis implementation for the `yQuant` system, serving as a reference for future development and maintenance.

## 1. Overview
Redis is used in `yQuant` for two primary purposes:
1.  **Pub/Sub Messaging**: Facilitating real-time, event-driven communication between microservices (e.g., Webhook -> OrderComposer -> BrokerGateway).
2.  **State Caching**: Storing shared state (Accounts, Positions, Prices) to decouple read-heavy services (Dashboard) from the Broker Gateway.

## 2. Connection & Configuration
All applications use the `yQuant.Infra.Redis` library for standardized connection management.

- **Configuration Key**: `ConnectionStrings:Redis`
- **Library**: `StackExchange.Redis` via `yQuant.Infra.Redis` wrapper.
- **DI Registration**: `services.AddRedisMiddleware(configuration);`

## 3. Schema Reference

### 3.1. Naming Convention
- **Entity-First**: Keys start with the domain entity name (e.g., `account`, `stock`).
- **No Prefixes**: Project prefixes like `yQuant:` are omitted.
- **Separator**: Colon (`:`) is used as a separator.

### 3.2. Channels (Pub/Sub)
| Channel | Publisher | Subscriber | Payload | Description |
| :--- | :--- | :--- | :--- | :--- |
| `signal` | `App.Webhook` | `App.OrderComposer` | `Signal` | Raw trading signals from TradingView. |
| `order` | `App.OrderComposer`, `App.Dashboard`, `App.Console` | `App.BrokerGateway` | `Order` | Executable orders. |
| `execution` | `App.BrokerGateway` | `App.Dashboard`, `Notification` | `OrderResult` | Execution results (fill/reject). |

### 3.3. Keys (State)
| Key Pattern | Type | Writer | Reader | Description |
| :--- | :--- | :--- | :--- | :--- |
| `account:{Alias}` | Hash | `App.BrokerGateway` | `App.Dashboard`, `App.OrderComposer` | Static account metadata (Number, Broker, Active status). Secrets are **NOT** stored. |
| `account:index` | Set | `App.BrokerGateway` | `App.Dashboard` | Set of all active account aliases. Used for discovery. |
| `deposit:{Alias}` | Hash | `App.BrokerGateway` | `App.Dashboard`, `App.OrderComposer` | Real-time balance per currency (Field: `USD`, Value: `1000.00`). |
| `position:{Alias}` | Hash | `App.BrokerGateway` | `App.Dashboard`, `App.OrderComposer` | Real-time positions (Field: `AAPL`, Value: `Position` JSON). |
| `stock:{Ticker}` | Hash | `App.StockMaster`, `App.BrokerGateway` | `App.Dashboard`, `App.Console` | Merged static info (Name, Exchange) and dynamic market data (Price, Change). |
| `scheduled:{Alias}` | String | `App.Dashboard` | `App.Dashboard` | List of scheduled orders (JSON Array). Stores schedule config (DaysOfWeek, TimeMode). |

## 4. Data Flows

### 4.1. Order Execution Flow
1.  **Signal Source**: `App.Webhook` receives a webhook -> publishes to `signal`.
2.  **Composition**: `App.OrderComposer` receives `signal` -> validates & composes `Order` -> publishes to `order`.
3.  **Execution**: `App.BrokerGateway` receives `order` -> sends to Broker API -> publishes result to `execution`.
4.  **Feedback**: `App.Dashboard` listens to `execution` for real-time updates.

### 4.2. Market Data Flow
1.  **Master Data**: `App.StockMaster` runs daily -> writes static data (Name, Exchange) to `stock:{Ticker}` with TTL.
2.  **Real-time Price**: `App.BrokerGateway` fetches prices periodically -> updates `price`, `changeRate` fields in `stock:{Ticker}` and refreshes TTL.
3.  **Consumption**: `App.Dashboard` reads `stock:{Ticker}` to display portfolio values.

### 4.3. Account Sync Flow
1.  **Startup**: `App.BrokerGateway` connects to Broker -> writes `account:{Alias}` and adds to `account:index`.
2.  **Hybrid Sync**:
    -   **Full Sync**: Polls Broker for Assets/Positions if `AccountUpdateIntervalMinutes` has passed since last sync.
    -   **Local Update**: On order execution, immediately updates `deposit:{Alias}` and `position:{Alias}` locally using estimated values (reducing Broker API reliance).
3.  **Aggregation**: `App.OrderComposer` reads all three keys (`account`, `deposit`, `position`) to reconstruct the full `Account` object for risk checks.

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
