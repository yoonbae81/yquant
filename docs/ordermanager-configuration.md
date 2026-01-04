# OrderManager Configuration

## Overview
The OrderManager application uses dynamic policy loading and market rules configuration to manage order execution, position sizing, and trading hours validation.

## Configuration Sources

OrderManager loads configuration from JSON files:

1. **`appsettings.json`**: Application settings, market configurations, and policy definitions
2. **`appsecrets.json`**: Sensitive notification settings, account credentials, and **Valkey connection strings**

## Market Configuration

Market rules are configured in `appsettings.json` under the top-level `Markets` section:

**Location:** `/appsettings.json`

**Structure:**
```json
{
  "Markets": {
    "KR": {
      "Exchanges": ["KRX", "KOSPI", "KOSDAQ"],
      "TimeZone": "Korea Standard Time",
      "Currency": "KRW",
      "TradingHours": {
        "Open": "09:00:00",
        "Close": "15:30:00"
      }
    },
    "US": {
      "Exchanges": ["NYSE", "NASDAQ", "AMEX"],
      "TimeZone": "Eastern Standard Time",
      "Currency": "USD",
      "TradingHours": {
        "RegularOpen": "09:30:00",
        "RegularClose": "16:00:00",
        "PreMarketOpen": "04:00:00",
        "AllowPreMarket": false
      }
    },
    "CN": {
      "Exchanges": ["SSE", "SZSE"],
      "TimeZone": "China Standard Time",
      "Currency": "CNY",
      "TradingHours": {
        "MorningOpen": "09:30:00",
        "MorningClose": "11:30:00",
        "AfternoonOpen": "13:00:00",
        "AfternoonClose": "15:00:00"
      }
    }
  }
}
```

### Market Configuration Parameters

- **Country Code**: 2-letter ISO country code (KR, US, CN, JP, HK, VN)
- **Exchanges**: List of exchange codes for this market
- **TimeZone**: Windows timezone identifier for the market
- **Currency**: ISO currency code
- **TradingHours**: Market-specific trading hours
  - Simple markets (KR): `Open`, `Close`
  - US markets: `RegularOpen`, `RegularClose`, `PreMarketOpen`, `AllowPreMarket`
  - Asian markets with lunch break (CN, JP, HK, VN): `MorningOpen`, `MorningClose`, `AfternoonOpen`, `AfternoonClose`

Position sizing policies are configured in `appsettings.json` and loaded dynamically.

### Policy Structure (`appsettings.json`)

**Format:** JSON
```json
{
  "OrderManager": {
    "Policies": {
      "Sizing": {
        "Path": "yQuant.Policies.Sizing.dll",
        "Class": "yQuant.Policies.Sizing.BasicPositionSizer",
        "Settings": {
          "Basic": {
            "MaxPositionRiskPct": 0.02,
            "MaxPortfolioAllocPct": 0.20,
            "StopLossPct": 0.05,
            "MinOrderAmt": 10000
          }
        }
      }
    }
  }
}
```

### Policy Parameters

#### Position Sizing Policy

- **Path**: The DLL filename (the DLL is copied to the output directory during build)
- **Class**: The fully qualified class name implementing `IPositionSizer`
- **Settings**:
  - `MaxPositionRiskPct`: Maximum percentage of portfolio equity to risk per position (default: 0.02 = 2%)
  - `MaxPortfolioAllocPct`: Maximum percentage of portfolio to allocate to a single position (default: 0.20 = 20%)
  - `StopLossPct`: Expected stop loss percentage used in risk calculations (default: 0.05 = 5%)
  - `MinOrderAmt`: Minimum order amount in KRW (default: 10000)

## Setup Instructions

1. **Configure Markets & Policies:**
   - Review and customize `appsettings.json`
   - Ensure timezone and trading hours are correct for your target markets
   - Verify policy paths and settings

2. **Configure Secrets:**
   - Ensure notifications and account credentials are set in `appsecrets.json`

3. **Build the application:**
   ```bash
   dotnet build
   ```
   The build process will automatically copy the policy DLLs to the output directory.

## Troubleshooting

### Error: "Position Sizer policy is not configured correctly"
This error occurs when:
- `appsettings.json` is missing the `OrderManager:Policies:Sizing` section
- The `Path` or `Class` values are empty or incorrect
- The specified DLL file is not found in the application directory

**Solution:**
1. Ensure `appsettings.json` contains the policy configuration
2. Verify the DLL path and class name are correct
3. Rebuild the application to ensure policy DLLs are copied to the output directory

### Error: "No market rules were loaded"
This error occurs when:
- `appsettings.json` is missing the `Markets` section
- Market configurations are malformed

**Solution:**
1. Ensure `appsettings.json` contains the `Markets` section
2. Verify JSON syntax is correct
3. Check that each market has required fields (Exchanges, TimeZone, Currency, TradingHours)

## Adding Custom Policies

To add a custom position sizing policy:

1. Create a new project in `src/04.Policies/`
2. Implement the `IPositionSizer` interface
3. Add a project reference in `yQuant.App.OrderManager.csproj`
4. Update `appsettings.json` to point to your custom policy DLL and class
5. Add your policy settings under `Settings:{PolicyName}`
6. Rebuild the application

## Notes

- Market and Policy configurations are stored in `appsettings.json`
- Sensitive information (including Valkey connections) is stored in `appsecrets.json`
