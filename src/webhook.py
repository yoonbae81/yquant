import os
import logging
import json
import sys
from contextlib import asynccontextmanager

from fastapi import FastAPI, Depends, Request, HTTPException, BackgroundTasks
from fastapi.responses import JSONResponse
from pydantic import BaseModel, validator
from pykis import PyKis

from telegram import TelegramHandler
import buy
import sell

# https://www.avast.com/random-password-generator
WEBHOOK_SECRET = os.getenv("WEBHOOK_SECRET")

ACCOUNTS_DIR = os.getenv("ACCOUNTS_DIR")
ALLOWED_IPS = {
    "52.89.214.238",
    "34.212.75.30",
    "54.218.53.128",
    "52.32.178.7",
    "127.0.0.1",
    "14.47.199.14",  # The EST
}

logging.basicConfig(
    level=logging.DEBUG,
    format="%(asctime)s - %(name)s - %(levelname)s - %(message)s",
    handlers=[logging.StreamHandler(sys.stdout)],
)

logger = logging.getLogger("webhook")
# logger.addHandler(TelegramHandler())


def load_accounts_data() -> dict:
    """Traverse account directories and load auth.json and tickers.txt for each account."""
    data = {}

    for account_name in os.listdir(ACCOUNTS_DIR):
        account_path = os.path.join(ACCOUNTS_DIR, account_name)

        # Skip if not a directory
        if not os.path.isdir(account_path):
            continue

        # Load auth.json
        auth_info = None
        auth_file = os.path.join(account_path, "auth.json")
        if os.path.exists(auth_file):
            broker = PyKis(auth_file, keep_token=True)

        # Load tickers.txt
        tickers = []
        tickers_file = os.path.join(account_path, "tickers.txt")
        if os.path.exists(tickers_file):
            with open(tickers_file, "r") as f:
                tickers = [line.strip() for line in f if line.strip()]

        # Store account data
        data[account_name] = {"broker": broker, "tickers": tickers}

    return data


@asynccontextmanager
async def lifespan(app: FastAPI):
    """Manage app lifecycle and load data at startup."""
    app.state.accounts = load_accounts_data()
    yield
    # Cleanup logic can be added here if needed


# Dependency injection function
def get_accounts(request: Request) -> dict:
    """Return account data from app state."""
    return request.app.state.accounts


class Alert(BaseModel):
    action: str
    exchange: str
    ticker: str
    currency: str
    price: str
    allocation: str
    secret: str

    @validator("secret")
    def secret_must_same(cls, v):
        if v != WEBHOOK_SECRET:
            raise ValueError(f"Invalid secret: {v}")
        return v


def execute_order(alert: Alert, broker: PyKis):
    try:
        if alert.action == "sell":
            # if alert.allocation == 100:
            logger.info(f"Sell: {alert.ticker}")
            order = sell.close_all(broker, alert.ticker)

        if alert.action == "buy":
            logger.info(f"Buy: {alert.ticker}")
            order = buy.allocation(broker, alert.ticker, alert.allocation)

        logger.debug(f"Order: {order}")

    except Exception as e:
        logger.error(f"{e}")


app = FastAPI(lifespan=lifespan)


@app.post("/webhook")
async def handle_webhook(
    alert: Alert,
    background_tasks: BackgroundTasks,
    accounts: dict = Depends(get_accounts),
):
    logger.info(f"Alert: {alert}")
    del alert.secret

    try:
        for name, account in accounts.items():
            if alert.ticker not in account["tickers"]:
                continue

            logger.debug(f"Account: {name}")
            background_tasks.add_task(execute_order, alert, account["broker"])

        return 200

    except Exception as e:
        logger.error(f"{e}")
        raise HTTPException(status_code=400, detail=str(e))


@app.middleware("http")
async def ip_filter_middleware(request: Request, call_next):
    if request.client.host not in ALLOWED_IPS:
        logger.warn(f"Not allowed IP address: {request.client.host}")
        return JSONResponse(status_code=403, content=f"Not allowed IP address")

    return await call_next(request)


if __name__ == "__main__":
    uvicorn.run("main:app", host="0.0.0.0", port=8000, reload=True)
