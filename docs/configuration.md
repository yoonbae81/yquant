1. **JSON Files** (solution root): Application settings, secrets, and market rules. Redis connection strings are now managed in `appsecrets.json`.

## Configuration Sources

### 1. JSON Files (Solution Root)

| File | Purpose | Version Control |
|------|---------|-----------------|
| `appsettings.json` | Application settings, market configurations, catalog URLs | ✅ Committed to Git |
| `appsecrets.json` | Sensitive credentials (broker API keys, accounts, webhooks, notifications, **Redis connections**) | ❌ Gitignored |
| `appsecrets.example.json` | Template for secrets file | ✅ Committed to Git |

## Setup Guide

### Step 1: Configure JSON Files

```bash
# Navigate to solution root
cd /path/to/yQuant

# Create secrets file from template
cp appsecrets.example.json appsecrets.json

# Edit secrets file with your credentials (accounts, API keys, notifications)
nano appsecrets.json  # or use your preferred editor

# Review settings file
cat appsettings.json
```

### Step One: Configure JSON Files

```bash
# Navigate to solution root
cd /path/to/yQuant

# Create secrets file from template
cp appsecrets.example.json appsecrets.json

# Edit secrets file with your credentials (accounts, API keys, notifications, and Redis)
nano appsecrets.json  # or use your preferred editor
```

Review both `appsettings.json` and `appsecrets.json` to ensure all necessary fields are present.

**Note:** JSON files are automatically copied to the build output directory during build.

## Detailed Configuration

### 1. Settings (`appsettings.json`)

Contains non-sensitive application settings, market configurations, catalog URLs, and policy definitions.

#### Market Rules Configuration
시장별 운영 규칙을 설정합니다.
```json
"MarketRules": {
  "KR": {
    "IsActive": true,
    "OpenTime": "09:00:00",
    "CloseTime": "15:30:00",
    "Currency": "KRW"
  },
  "US": {
    "IsActive": true,
    "OpenTime": "23:30:00",
    "CloseTime": "06:00:00",
    "Currency": "USD"
  }
}
```

#### Strategy Sizing Mapping
전략별로 사용할 포지션 사이징 정책을 매핑합니다.
```json
"StrategySizingMapping": {
  "Default": "OnlyOne",
  "Swing": "Basic",
  "DayTrading": "Kelly"
}
```
`OnlyOne`은 단일 포지션만 유지, `Basic`은 설정된 비율에 따른 사이징을 의미합니다.

### 2. Secrets (`appsecrets.json`)

Contains sensitive credentials, API keys, and notification webhook URLs.

## Security Best Practices

-   **Never commit real credentials to Git.**
-   The `.gitignore` file is configured to exclude sensitive files:
    -   `appsecrets.json`
-   Always use `appsecrets.example.json` files to share the configuration structure without exposing secrets.
-   `appsettings.json` contains no secrets and can be committed to Git.

## How it Works

Configuration files are automatically copied to the build output directory:

```xml
<ItemGroup>
  <None Include="$(MSBuildThisFileDirectory)*.json">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    <Link>%(Filename)%(Extension)</Link>
    <Visible>false</Visible>
  </None>
</ItemGroup>
```

This logic copies all JSON configuration files from the solution root to the output folder of **every** project that is built, adhering to the DRY (Don't Repeat Yourself) principle.
```
