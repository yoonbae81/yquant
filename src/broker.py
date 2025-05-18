#!/usr/bin/env python3
import argparse
import asyncio
import logging
import math
import os
import sys
from decimal import Decimal
from dotenv import load_dotenv

# Adjust the import to match the actual available symbols in pykis
from pykis import MARKET_TYPE, PyKis, KisBalance, KisAccount, KisStock
from pykis.api.account.order import order, ensure_price

logger = logging.getLogger("broker")

load_dotenv()
ACCOUNTS_DIR = os.getenv("ACCOUNTS_DIR", "accounts")


class Broker:
    def __init__(self, kis: PyKis):
        self._kis: PyKis = kis

    def buy(self, exchange: MARKET_TYPE, ticker: str, quantity: int, price: Decimal):
        logger.debug(f"Buying {quantity} shares of {ticker} at {price:,.2f}")
        return order(
            self._kis,
            self._kis.account().account_number,
            exchange,
            ticker,
            "buy",
            price,
            quantity,
        )

    def sell(self, exchange: MARKET_TYPE, ticker: str, quantity: int, price: Decimal):
        logger.debug(f"Selling {quantity} shares of {ticker} at {price:,.2f}")
        return order(
            self._kis,
            self._kis.account().account_number,
            exchange,
            ticker,
            "sell",
            price,
            quantity,
        )


if __name__ == "__main__":

    logging.basicConfig(
        level=logging.DEBUG,
        format="%(levelname)s - %(message)s",
        handlers=[logging.StreamHandler(sys.stdout)],
    )

    parser = argparse.ArgumentParser()
    parser.add_argument("account", type=str)
    parser.add_argument(
        "action",
        type=str,
        choices=["sell", "buy"],
        help="Action to perform: 'sell' or 'buy'",
    )
    parser.add_argument("ticker", type=str, nargs="?", default=None)
    parser.add_argument("quantity", type=float, nargs="?", default=None)
    parser.add_argument("price", type=float, nargs="?", default=None)
    args = parser.parse_args()

    auth_path = os.path.join(ACCOUNTS_DIR, args.account, "auth.json")
    kis = PyKis(auth_path, keep_token=True)
    broker = Broker(kis)

    method = getattr(broker, args.action, None)
    if not method or not callable(method):
        raise AttributeError(f"No '{args.action}' method in Broker class")

    result = method(args.ticker, args.quantity, args.price)

    print(repr(result))
