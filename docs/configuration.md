# yQuant Configuration Guide

## Centralized Configuration via Environment Variables

All yQuant applications now use a **single `.env.local` file** for configuration. No more per-project appsettings.json files!

## Quick Start

### 1. Create `.env.local`

```bash
Copy-Item .env.example .env.local
```

### 2. Fill in Your Values

Edit `.env.local` with your actual credentials:

```bash
# Redis
Redis=localhost:6379

# Account Configuration
Accounts__0__Alias=MainAccount
Accounts__0__UserId=your_user_id
Accounts__0__AccountNumber=12345678-01
Accounts__0__Credentials__AppKey=your_app_key
Accounts__0__Credentials__AppSecret=your_app_secret

# Discord (optional)
Discord__Enabled=true
Discord__System__Status=https://discord.com/api/webhooks/...
Discord__System__Error=https://discord.com/api/webhooks/...

# Telegram (optional)
Telegram__BotToken=your_token
Telegram__ChatId=your_chat_id
```

### 3. Load and Run

```powershell
# Load environment variables
. .\load-env.ps1

# Run any application
dotnet run --project src/03.Applications/yQuant.App.Console
dotnet run --project src/03.Applications/yQuant.App.Dashboard
dotnet run --project src/03.Applications/yQuant.App.BrokerGateway
```

### Optional Settings

**Discord Notifications:**
- `Discord__Enabled` - Enable/disable Discord (default: true)
- `Discord__System__Status` - System status webhook
- `Discord__System__Error` - System error webhook
- `Discord__Signal__Mappings__[Strategy]` - Strategy-specific webhooks
- `Discord__Accounts__[Alias]__Channels__[Type]` - Account-specific webhooks

**Telegram Notifications:**
- `Telegram__BotToken` - Telegram bot token
- `Telegram__ChatId` - Telegram chat ID

**Webhook Security (for yQuant.App.Webhook):**
- `Security__WebhookSecret` - Webhook authentication secret
- `Security__AllowedIps__[N]` - Allowed IP addresses

## Multi-Account Setup

Add multiple accounts by incrementing the index:

```bash
# Account 0
Accounts__0__Alias=MainAccount
Accounts__0__UserId=user1
Accounts__0__AccountNumber=12345678-01
Accounts__0__Credentials__AppKey=key1
Accounts__0__Credentials__AppSecret=secret1

# Account 1
Accounts__1__Alias=SubAccount
Accounts__1__UserId=user2
Accounts__1__AccountNumber=87654321-01
Accounts__1__Credentials__AppKey=key2
Accounts__1__Credentials__AppSecret=secret2
```

## Application-Specific Notes

### Console App
- **Usage**: `yquant <AccountAlias> <Command> [Args]`
- **Examples**:
  - `yquant MainAccount assets`
  - `yquant MainAccount price 005930`
  - `yquant MainAccount buy 005930 10`
- Uses the configuration for the specified `AccountAlias`
- Supports all commands: `assets`, `price`, `buy`, `sell`, `report`

### BrokerGateway & Dashboard
- Support multiple accounts simultaneously
- Automatically load all configured accounts from the Accounts array

### StockMaster, OrderComposer, Webhook
- Use the centralized configuration
- Discord and Telegram notifications configured globally

## Security Best Practices

✅ **DO:**
- Keep `.env.local` in your `.gitignore` (already configured)
- Use strong, unique secrets for each environment
- Rotate credentials regularly

❌ **DON'T:**
- Commit `.env.local` to version control
- Share credentials in plain text
- Use production credentials in development

## Troubleshooting

**Q: My app can't find the configuration**
A: Make sure you've run `. .\load-env.ps1` before running the app

**Q: Do I need to load .env.local for each terminal session?**
A: Yes, environment variables are session-specific. Run `load-env.ps1` in each new terminal

**Q: Can I use User Secrets instead?**
A: While supported, we recommend using `.env.local` for consistency across all apps
