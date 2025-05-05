#!/usr/bin/env python3
import argparse
import asyncio
import logging
import math
import os
import sys
from pykis import PyKis, KisAuth, KisAccount, KisStock


logger = logging.getLogger("buy")


def allocation(broker, ticker, allocation):
    price = broker.stock(ticker).quote().price
    price = math.floor(price * 100) / 100
    _request(broker, ticker, 1, price)


def _request(broker: PyKis, ticker, quantity, price):
    stock: KisStock = broker.stock(ticker)
    order = stock.buy(qty=quantity, price=price)
    logger.info(f"매수 요청: {ticker} {quantity}주")

    logger.info(repr(order))


if __name__ == "__main__":
    logging.basicConfig(
        level=logging.DEBUG,
        format="%(asctime)s - %(name)s - %(levelname)s - %(message)s",
        handlers=[logging.StreamHandler(sys.stdout)],
    )

    account = os.environ.get("ACCOUNT")
    if account is None:
        print("The ACCOUNT environment variable is not set.", file=sys.stderr)
        sys.exit(1)

    parser = argparse.ArgumentParser()
    parser.add_argument("ticker", type=str)
    parser.add_argument("quantity", type=float, nargs="?", default=None)
    parser.add_argument("price", type=float, nargs="?", default=None)

    args = parser.parse_args()

    kis = PyKis(f"accounts/{account}/auth.json", keep_token=True)
    _request(kis, ticker=args.ticker, quantity=args.quantity, price=args.price)
