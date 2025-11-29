# Configuration Settings

The yQuant system now uses Redis to store and manage configuration settings. This allows for centralized management and dynamic updates without redeploying applications.

## Redis Configuration
The applications connect to Redis using the connection string specified in the `YQUANT_REDIS` environment variable.

- **Environment Variable**: `YQUANT_REDIS`
- **Default**: `localhost:6379`
- **Key Prefix**: `Settings:`

## Managing Settings
You can manage settings via the **Dashboard** under the **Settings** page. This page allows you to view, edit, and add configuration keys.

## Migration Script
To migrate existing settings from `appsettings.json` to Redis, you can use the following PowerShell script. This script reads the JSON file and populates Redis with the corresponding keys.

### Prerequisites
# Configuration Settings

The yQuant system now uses Redis to store and manage configuration settings. This allows for centralized management and dynamic updates without redeploying applications.

## Redis Configuration
The applications connect to Redis using the connection string specified in the `YQUANT_REDIS` environment variable.

- **Environment Variable**: `YQUANT_REDIS`
- **Default**: `localhost:6379`
- **Key Prefix**: `Settings:`

## Managing Settings
You can manage settings via the **Dashboard** under the **Settings** page. This page allows you to view, edit, and add configuration keys.

- `Policies:Sizing:Class`: `yQuant.Policies.Sizing.Basic.BasicPositionSizer`
- `Policies:Sizing:Settings:MaxPositionRiskPct`: `0.02`

## Application Updates
All applications (`Console`, `Dashboard`, `BrokerGateway`, `StockMaster`, `OrderComposer`) have been updated to read these values from Redis. Ensure `REDIS_CONNECTION_STRING` is set before running them.
