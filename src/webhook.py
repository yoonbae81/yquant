#!/usr/bin/env python3
from decimal import Decimal
import os
import logging
import importlib.util
import json
import sys
from pykis import MARKET_TYPE, PyKis
import uvicorn
import threading
import requests
from contextlib import asynccontextmanager
from dotenv import load_dotenv

from fastapi import FastAPI, Depends, Request, HTTPException, BackgroundTasks
from fastapi.responses import JSONResponse
from pydantic import BaseModel, field_validator

from broker import Broker
from balance import Balance

load_dotenv()
ACCOUNTS_DIR = os.getenv("ACCOUNTS_DIR", "accounts")
WEBHOOK_SECRET = os.getenv("WEBHOOK_SECRET")
TELEGRAM_TOKEN = os.getenv("TELEGRAM_TOKEN")
TELEGRAM_CHAT_ID = os.getenv("TELEGRAM_CHAT_ID")

logging.basicConfig(
    level=logging.DEBUG,
    format="%(asctime)s - %(name)s - %(levelname)s - %(message)s",
    handlers=[logging.StreamHandler(sys.stdout)],
)


class TelegramHandler(logging.Handler):
    def emit(self, record):
        log_entry = self.format(record)
        thread = threading.Thread(target=self.send, args=(log_entry,))
        thread.daemon = True  # Daemon thread will exit when the main program exits
        thread.start()

    @staticmethod
    def send(log_entry):
        url = f"https://api.telegram.org/bot{TELEGRAM_TOKEN}/sendMessage"
        data = {"chat_id": TELEGRAM_CHAT_ID, "text": log_entry}
        try:
            requests.post(url, data=data, timeout=10)
        except Exception as e:
            print(f"Failed to send log to Telegram: {e}")


class Message(BaseModel):
    action: str
    exchange: MARKET_TYPE
    ticker: str
    price: float
    strength: int
    comment: str
    secret: str

    @field_validator("secret")
    def secret_must_same(cls, v):
        if v != WEBHOOK_SECRET:
            raise ValueError(f"Invalid secret: {v}")
        return v


def load_accounts_data() -> dict:
    """Traverse account directories and load tickers and broker for each account."""
    result = {}
    for account_name in os.listdir(ACCOUNTS_DIR):
        logger.debug(f"Loading account information: {account_name}")
        account_path = os.path.join(ACCOUNTS_DIR, account_name)
        if not os.path.isdir(account_path):
            continue

        tickers = []
        tickers_path = os.path.join(account_path, "tickers.txt")
        if os.path.exists(tickers_path):
            with open(tickers_path, "r") as f:
                tickers = [line.strip() for line in f if line.strip()]

        auth_path = os.path.join(account_path, "auth.json")
        if not os.path.exists(auth_path):
            raise FileNotFoundError(f"{auth_path} not found")

        kis = PyKis(auth_path, keep_token=True)
        data = {
            "tickers": tickers,
            "balance": Balance(kis),
            "broker": Broker(kis),
        }
        logger.debug(f"Loaded: {data}")
        result[account_name] = data

    return result


@asynccontextmanager
async def lifespan(app: FastAPI):
    """Manage app lifecycle and load data at startup."""
    app.state.accounts = load_accounts_data()
    yield
    # Cleanup logic can be added here if needed


def get_accounts(request: Request) -> dict:
    """Return account data from app state."""
    return request.app.state.accounts


def execute_order(msg: Message, balance: Balance, broker: Broker):
    logger.info(f"Executing {msg.action.upper()}: {msg.ticker}, {msg.comment}")

    try:

        if msg.action == "sell":
            quantity = balance.sell_quantity(msg.ticker, msg.price, msg.strength)
            order = broker.sell(msg.exchange, msg.ticker, quantity, msg.price)

        if msg.action == "buy":
            quantity = balance.buy_quantity(msg.ticker, msg.price, msg.strength)
            order = broker.buy(msg.exchange, msg.ticker, quantity, msg.price)

        balance.update()

    except Exception as e:
        logger.error(f"{e}")
        # raise e


logger = logging.getLogger("webhook")
# logger.addHandler(TelegramHandler())
# logging.getLogger("urllib3.connectionpool").setLevel(logging.WARNING)

app = FastAPI(lifespan=lifespan)


@app.post("/webhook")
async def handle_webhook(
    msg: Message,
    background_tasks: BackgroundTasks,
    accounts: dict = Depends(get_accounts),
):
    logger.info(f"Message: {msg}")
    del msg.secret

    try:
        for account in accounts.values():
            if "*" not in account["tickers"] and msg.ticker not in account["tickers"]:
                continue

            background_tasks.add_task(
                execute_order, msg, account["balance"], account["broker"]
            )

        return 200

    except Exception as e:
        logger.error(f"{e}")
        raise HTTPException(status_code=400, detail=str(e))


if __name__ == "__main__":
    uvicorn.run("webhook:app", host="0.0.0.0", port=8000, reload=True)
