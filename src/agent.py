import argparse
import asyncio
from decimal import Decimal
import json
import logging
import os
from typing import Optional
from pydantic import BaseModel
import redis

from dotenv import load_dotenv
import redis.asyncio
from pykis import MARKET_TYPE, PyKis
from pykis.api.account.order import order, ensure_price

logger = logging.getLogger("agent")

load_dotenv()
ACCOUNTS_DIR = os.getenv("ACCOUNTS_DIR", "accounts")
REDIS_URL = os.getenv("REDIS_URL", "redis://localhost:6379/0")


class Signal(BaseModel):
    action: str
    exchange: MARKET_TYPE
    ticker: str
    currency: str
    price: float
    strength: int
    comment: str


class Order(BaseModel):
    account: str
    action: str
    exchange: MARKET_TYPE
    ticker: str
    currency: Optional[str] = None
    price: Optional[float] = None
    quantity: int
    comment: Optional[str] = None


class Broker(PyKis):
    def __init__(self, *args, account_alias, **kwargs):
        super().__init__(*args, **kwargs)
        self.account_alias = account_alias


def get_quantity(ticker: str, price: float, strenght: int) -> int:
    return 1


async def handle_signal(broker: Broker, data: dict):
    logger.debug("Received signal:\n%s", json.dumps(data, indent=2, ensure_ascii=False))
    signal = Signal(**data)

    quantity = get_quantity(signal.ticker, signal.price, signal.strength)
    order = Order(**data, account=broker.account_alias, quantity=quantity)

    await execute_order(broker, order)


async def handle_order(broker: Broker, data: dict):
    if data["account"] != broker.account_alias:
        return

    logger.debug("Received order:\n%s", json.dumps(data, indent=2, ensure_ascii=False))
    order = Order(**data)

    await execute_order(broker, order)


def format_price(price: float, exchange: str) -> str:
    if exchange == "KRX":
        return f"{price:,.0f}"
    else:
        return f"{price:,.2f}"


async def execute_order(broker: Broker, order: Order) -> None:
    # price = format_price(order.price, order.exchange)
    logger.info(
        f"Executing {order.action.upper()}: {order.exchange}:{order.ticker} x{order.quantity}"
    )


def buy(self, exchange: MARKET_TYPE, ticker: str, quantity: Decimal, price: Decimal):
    price = ensure_price(price, 2)
    logger.debug(f"Buying {quantity} shares of {ticker} at {price:,.2f}")
    return order(
        self._kis,
        self._kis.account().account_number,
        exchange,
        ticker,
        "buy",
        None,  # price,
        quantity,
    )


def sell(self, exchange: MARKET_TYPE, ticker: str, quantity: Decimal, price: Decimal):
    price = ensure_price(price, 2)
    logger.debug(f"Selling {quantity} shares of {ticker} at {price:,.2f}")
    return order(
        self._kis,
        self._kis.account().account_number,
        exchange,
        ticker,
        "sell",
        None,  # price,
        quantity,
    )


HANDLERS = {
    "signal": handle_signal,
    "order": handle_order,
}


async def main(broker: Broker, tickers: list[str]):

    r = await redis.asyncio.from_url(REDIS_URL, decode_responses=True)
    pubsub = r.pubsub()

    channels = ["signal", f"order"]
    await pubsub.subscribe(*channels)

    logger.info(f"Subscribing channels: {', '.join(channels)}")

    try:
        async for msg in pubsub.listen():
            if msg["type"] != "message":
                continue

            try:
                data = json.loads(msg["data"])

            except json.JSONDecodeError as e:
                logger.error(f"JSON Decode Error: {e}")
                continue

            if "*" not in tickers and data["ticker"] not in tickers:
                continue

            handler = HANDLERS.get(msg["channel"])
            if handler is not None:
                await handler(broker, data)
            else:
                logger.error(f"No handler found for channel: {msg['channel']}")

    finally:
        await pubsub.aclose()


def get_tickers(account) -> list[str]:
    account_path = os.path.join(ACCOUNTS_DIR, account)
    if not os.path.isdir(account_path):
        raise FileNotFoundError(f"Account directory {account_path} does not exist")

    tickers = []
    tickers_path = os.path.join(account_path, "tickers.txt")
    if os.path.exists(tickers_path):
        with open(tickers_path, "r") as f:
            tickers = [line.strip() for line in f if line.strip()]

    return tickers


def get_broker(account) -> Broker:
    account_path = os.path.join(ACCOUNTS_DIR, account)
    if not os.path.isdir(account_path):
        raise FileNotFoundError(f"Account directory {account_path} does not exist")

    auth_path = os.path.join(account_path, "auth.json")
    if not os.path.exists(auth_path):
        raise FileNotFoundError(f"{auth_path} not found")

    return Broker(auth_path, account_alias=account, keep_token=True)


if __name__ == "__main__":

    logging.basicConfig(
        level=logging.INFO,
        format="%(asctime)s - %(levelname)s - %(message)s",
        handlers=[logging.StreamHandler()],
    )

    parser = argparse.ArgumentParser(description="Agent for trading signals")
    parser.add_argument("account", type=str)
    args = parser.parse_args()

    logger.debug(f"Loading Account information")
    tickers = get_tickers(args.account)
    broker = get_broker(args.account)
    logger.debug(f"Account [{args.account}] loaded")

    asyncio.run(main(broker, tickers))
