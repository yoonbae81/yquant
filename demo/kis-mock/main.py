import uvicorn
from fastapi import FastAPI, Request, Query, Header
from datetime import datetime
import yfinance as yf
import random
from typing import Optional, Dict

app = FastAPI(title="yQuant KIS Mock Server")

# In-memory storage for virtual demo accounts
# Alias -> { "balance": float, "positions": { ticker: { "qty": int, "avg_price": float } } }
demo_accounts: Dict[str, dict] = {}

def get_demo_account(account_no: str):
    if account_no not in demo_accounts:
        demo_accounts[account_no] = {
            "balance": 100_000_000.0,  # 100M KRW starting
            "positions": {}
        }
    return demo_accounts[account_no]

async def fetch_real_price(ticker: str, is_overseas: bool = False) -> float:
    try:
        if is_overseas:
            stock = yf.Ticker(ticker)
        else:
            stock = yf.Ticker(f"{ticker}.KS")
            info = stock.fast_info
            if info.last_price is None or info.last_price == 0:
                stock = yf.Ticker(f"{ticker}.KQ")
        
        info = stock.fast_info
        return info.last_price or 0.0
    except:
        return 50000.0 # Fallback

# 1. OAuth2 Token
@app.post("/oauth2/tokenP")
async def get_token():
    return {
        "access_token": "mock_access_token_yQuant_demo_123456789",
        "access_token_token_expired": datetime.now().strftime("%Y-%m-%d %H:%M:%S"),
        "token_type": "Bearer",
        "expires_in": 86400
    }

# 2. Hashkey
@app.post("/uapi/hashkey")
async def get_hashkey(request: Request):
    return {"HASH": "mock_hash_value_for_demo_purposes"}

# 3. Domestic Price
@app.get("/uapi/domestic-stock/v1/quotations/inquire-price")
async def get_domestic_price(FID_INPUT_ISCD: str):
    ticker = FID_INPUT_ISCD
    try:
        stock = yf.Ticker(f"{ticker}.KS")
        info = stock.fast_info
        if info.last_price is None or info.last_price == 0:
            stock = yf.Ticker(f"{ticker}.KQ")
            info = stock.fast_info

        price = info.last_price or 0
        prev_close = info.previous_close or price
        change = price - prev_close
        change_rate = (change / prev_close * 100) if prev_close else 0

        return {
            "rt_cd": "0", "msg_cd": "MCA00000", "msg1": "성공",
            "output": {
                "stck_prpr": str(int(price)),
                "prdy_vrss": str(int(change)),
                "prdy_ctrt": str(round(change_rate, 2)),
                "acml_vol": "1000000"
            }
        }
    except Exception as e:
        return {"rt_cd": "1", "msg1": f"Error: {str(e)}", "output": None}

# 4. Overseas Price
@app.get("/uapi/overseas-price/v1/quotations/price")
async def get_overseas_price(symb: str):
    try:
        stock = yf.Ticker(symb)
        info = stock.fast_info
        price = info.last_price or 0
        prev_close = info.previous_close or price
        change = price - prev_close
        change_rate = (change / prev_close * 100) if prev_close else 0

        return {
            "rt_cd": "0", "msg_cd": "MCA00000", "msg1": "성공",
            "output": {
                "last": str(round(price, 2)),
                "diff": str(round(change, 2)),
                "rate": str(round(change_rate, 2)),
                "base": str(round(prev_close, 2))
            }
        }
    except Exception as e:
        return {"rt_cd": "1", "msg1": f"Error: {str(e)}", "output": None}

# 5. Domestic Balance
@app.get("/uapi/domestic-stock/v1/trading/inquire-balance")
async def get_domestic_balance(CANO: str):
    acc = get_demo_account(CANO)
    
    output1 = []
    for ticker, pos in acc["positions"].items():
        if ticker.isdigit():
            output1.append({
                "pdno": ticker,
                "hldg_qty": str(pos["qty"]),
                "pchs_avg_pric": str(int(pos["avg_price"])),
                "prpr": str(int(pos["avg_price"]))
            })

    return {
        "rt_cd": "0", "msg_cd": "OPSP0000", "msg1": "성공",
        "output1": output1,
        "output2": [
            {
                "dnca_tot_amt": str(int(acc["balance"])),
                "nxdy_excc_amt": str(int(acc["balance"])),
                "scts_evlu_amt": "0",
                "tot_evlu_amt": str(int(acc["balance"]))
            }
        ]
    }

# 6. Domestic Order (Buy/Sell)
@app.post("/uapi/domestic-stock/v1/trading/order-cash")
async def place_domestic_order(request: Request, tr_id: str = Header(None)):
    body = await request.json()
    cano = body.get("CANO")
    ticker = body.get("pdno")
    qty = int(body.get("ord_qty", 0))
    acc = get_demo_account(cano)
    
    price = await fetch_real_price(ticker)
    total_cost = price * qty
    
    # Try to determine if Buy or Sell
    # In KIS, Buy TrId: TTTC08408U, Sell TrId: TTTC08409U
    tr_id_str = str(tr_id or body.get("tr_id", ""))
    is_buy = "TTC08408U" in tr_id_str or body.get("ord_dvsn") == "00" # Simple heuristic

    if is_buy: # Buy
        if acc["balance"] >= total_cost:
            acc["balance"] -= total_cost
            pos = acc["positions"].get(ticker, {"qty": 0, "avg_price": 0})
            new_qty = pos["qty"] + qty
            pos["avg_price"] = ((pos["avg_price"] * pos["qty"]) + total_cost) / new_qty
            pos["qty"] = new_qty
            acc["positions"][ticker] = pos
        else:
            return {"rt_cd": "1", "msg_cd": "MOCK0001", "msg1": "Insufficient virtual balance"}
    else: # Sell
        pos = acc["positions"].get(ticker)
        if pos and pos["qty"] >= qty:
            acc["balance"] += total_cost
            pos["qty"] -= qty
            if pos["qty"] <= 0: del acc["positions"][ticker]
        else:
            return {"rt_cd": "1", "msg_cd": "MOCK0002", "msg1": "Insufficient virtual quantity"}

    return {
        "rt_cd": "0", "msg_cd": "OPSP0000",
        "msg1": f"Virtual {'Buy' if is_buy else 'Sell'} Success ({ticker})",
        "output": {"odno": f"D{datetime.now().strftime('%H%M%S%f')}", "ord_tmd": datetime.now().strftime("%H%M%S")}
    }

# 7. Overseas Order (Dummy Success)
@app.post("/uapi/overseas-stock/v1/trading/order")
async def place_overseas_order(request: Request):
    return {
        "rt_cd": "0", "msg_cd": "OPSP0000",
        "msg1": "Overseas Order Accepted (Demo)",
        "output": {"odno": f"O{datetime.now().strftime('%H%M%S%f')}", "ord_tmd": datetime.now().strftime("%H%M%S")}
    }

if __name__ == "__main__":
    uvicorn.run(app, host="0.0.0.0", port=9443)
