namespace yQuant.App.BrokerGateway;

/// <summary>
/// Contains Lua scripts for atomic Valkey operations to prevent race conditions.
/// </summary>
public static class ValkeyLuaScripts
{
    /// <summary>
    /// Atomically updates a position in Valkey Hash.
    /// This script prevents Lost Update problems when multiple orders are processed concurrently.
    /// 
    /// KEYS[1]: Valkey hash key (e.g., "position:{alias}")
    /// ARGV[1]: Ticker symbol (hash field)
    /// ARGV[2]: Order action ("Buy" or "Sell")
    /// ARGV[3]: Order quantity (decimal)
    /// ARGV[4]: Execution price (decimal)
    /// ARGV[5]: Account alias
    /// ARGV[6]: Currency
    /// ARGV[7]: Fallback current price (from stock:{ticker})
    /// ARGV[8]: BuyReason (e.g., "Manual", "Schedule", "Webhook:StrategyName")
    /// 
    /// Returns: JSON object with updated position and buyReasonChanged flag
    /// </summary>
    public const string UpdatePositionScript = @"
local key = KEYS[1]
local ticker = ARGV[1]
local action = ARGV[2]
local orderQty = tonumber(ARGV[3])
local executionPrice = tonumber(ARGV[4])
local accountAlias = ARGV[5]
local currency = ARGV[6]
local fallbackPrice = tonumber(ARGV[7])
local newBuyReason = ARGV[8]

-- Get existing position JSON
local posJson = redis.call('HGET', key, ticker)
local position = {}
local buyReasonChanged = false

if posJson then
    position = cjson.decode(posJson)
    if action == 'Buy' and position.BuyReason and position.BuyReason ~= newBuyReason then
        buyReasonChanged = true
    end
else
    position = {
        AccountAlias = accountAlias,
        Ticker = ticker,
        Currency = currency,
        Qty = 0,
        AvgPrice = 0,
        CurrentPrice = executionPrice > 0 and executionPrice or fallbackPrice,
        ChangeRate = 0,
        BuyReason = newBuyReason
    }
end

if action == 'Buy' then
    local oldQty = position.Qty or 0
    local oldAvgPrice = position.AvgPrice or 0
    local newQty = oldQty + orderQty
    
    if newQty > 0 then
        position.AvgPrice = ((oldQty * oldAvgPrice) + (orderQty * executionPrice)) / newQty
    else
        position.AvgPrice = executionPrice
    end
    
    position.Qty = newQty
    if not position.BuyReason then
        position.BuyReason = newBuyReason
    end
elseif action == 'Sell' then
    position.Qty = (position.Qty or 0) - orderQty
end

if position.Qty <= 0 then
    redis.call('HDEL', key, ticker)
    return cjson.encode({ position = '', buyReasonChanged = buyReasonChanged, oldReason = position.BuyReason or '', newReason = newBuyReason })
end

local updatedJson = cjson.encode(position)
redis.call('HSET', key, ticker, updatedJson)

return cjson.encode({ position = updatedJson, buyReasonChanged = buyReasonChanged, oldReason = position.BuyReason or '', newReason = newBuyReason })
";

    /// <summary>
    /// Atomically updates deposit balance in Valkey Hash.
    /// This script prevents race conditions when multiple orders update the same currency balance.
    /// 
    /// KEYS[1]: Valkey hash key (e.g., "deposit:{alias}")
    /// ARGV[1]: Currency field (e.g., "USD", "KRW")
    /// ARGV[2]: Order action ("Buy" or "Sell")
    /// ARGV[3]: Amount change (quantity * price)
    /// 
    /// Returns: Updated balance
    /// </summary>
    public const string UpdateDepositScript = @"
local key = KEYS[1]
local currencyField = ARGV[1]
local action = ARGV[2]
local amountChange = tonumber(ARGV[3])

-- Get current balance
local currentVal = redis.call('HGET', key, currencyField)
local currentAmount = 0

if currentVal then
    currentAmount = tonumber(currentVal)
end

-- Update balance based on action
if action == 'Buy' then
    currentAmount = currentAmount - amountChange
elseif action == 'Sell' then
    currentAmount = currentAmount + amountChange
end

-- Save updated balance
redis.call('HSET', key, currencyField, tostring(currentAmount))

return currentAmount
";
}
