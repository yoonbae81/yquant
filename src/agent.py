import argparse
import asyncio
import json
import logging
import os
from decimal import Decimal
from typing import Literal, Optional

from dotenv import load_dotenv
from pydantic import BaseModel
import pykis.api.account.order
from pykis import PyKis
import redis
import redis.asyncio

logger = logging.getLogger("yquant")

load_dotenv()
ACCOUNTS_DIR = os.getenv("ACCOUNTS_DIR", "accounts")
REDIS_URL = os.getenv("REDIS_URL", "redis://localhost:6379/0")

ORDER_TYPE = Literal["buy", "sell"]
ORDER_METHOD = Literal["MARKET", "LIMIT"]
MARKET_TYPE = Literal["KRX", "NASDAQ", "NYSE", "AMEX"]
CURRENCY_TYPE = Literal["KRW", "USD"]

class Signal(BaseModel):
    action: ORDER_TYPE
    exchange: MARKET_TYPE
    ticker: str
    currency: CURRENCY_TYPE
    price: float
    strength: int
    comment: str


class Order(BaseModel):
    action: ORDER_TYPE
    exchange: MARKET_TYPE
    ticker: str
    quantity: int
    currency: Optional[CURRENCY_TYPE] = None
    price: float
    method: ORDER_METHOD = "LIMIT"
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
    order = Order(**data, quantity=quantity, method="LIMIT")

    await execute_order(broker, order)


async def handle_order(broker: Broker, data: dict):
    logger.debug("Received order:\n%s", json.dumps(data, indent=2, ensure_ascii=False))
    order = Order(**data)

    await execute_order(broker, order)


def format_price(price: float, exchange: str) -> str:
    if exchange == "KRX":
        return f"{price:,.0f}"
    else:
        return f"{price:,.2f}"


def _get_tick(price: float) -> int:
    if price < 1000:
        return 1
    elif price < 5000:
        return 5
    elif price < 10000:
        return 10
    elif price < 50000:
        return 50
    elif price < 100000:
        return 100
    elif price < 500000:
        return 500
    else:
        return 1000


def adjust_price(exchange: MARKET_TYPE, action: ORDER_TYPE, price: float, method: ORDER_METHOD) -> Decimal:
    if method == "MARKET":
        adjusted = price * (1.01 if action == "buy" else 0.99)
        if exchange == "KRX":
            tick = _get_tick(price)
            adjusted = (adjusted // tick) * tick
            
    elif method == "LIMIT":
        tick = _get_tick(price) if exchange == "KRX" else 0.01
        adjusted = price + tick if action == "buy" else price - tick

    return pykis.api.account.order.ensure_price(adjusted, 0 if exchange == "KRX" else 2)


async def execute_order(broker: Broker, order: Order) -> None:
    logger.info(
        f"Executing {order.action.upper()}: {order.exchange}:{order.ticker} x{order.quantity}"
    )
    logger.info(f"Order details: {order}")
    price = adjust_price(order.exchange, order.action, order.price, order.method)
    logger.info(f"Adjusted price: {price}")

    result = pykis.api.account.order.order(
        broker,
        broker.account().account_number,
        order.exchange,
        order.ticker,
        order.action,
        price,
        order.quantity,
    )

    logger.info(f"Order result: {result}")



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


HANDLERS = {
    "signal": handle_signal,
    "order": handle_order,
}


async def main(broker: Broker, tickers: list[str]):
    r = await redis.asyncio.from_url(REDIS_URL, decode_responses=True)
    pubsub = r.pubsub()

    channels = ["signal", f"order:{broker.account_alias}"]
    await pubsub.subscribe(*channels)

    logger.info(f"Subscribing channels: {', '.join(channels)}")

    async with pubsub:
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

            handler = HANDLERS.get(msg["channel"].split(":")[0])

            try:
                if handler:
                    await handler(broker, data)
                else:
                    logger.error(f"No handler found for channel: {msg['channel']}")
            except Exception as e:
                logger.error(f"Error handling message: {e}")
                continue


if __name__ == "__main__":

    logging.basicConfig(
        level=logging.DEBUG,
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
