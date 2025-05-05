import argparse
import asyncio
import logging
import math
import os
import sys
from pykis import PyKis, KisAuth, KisAccount, KisStock

logger = logging.getLogger("sell")


def close_all(broker: PyKis, ticker):
    quantity = broker.stock(ticker).orderable
    price = broker.stock(ticker).quote().price
    price = math.floor(price * 100) / 100
    _request(broker, ticker, quantity, price)


def _request(broker: PyKis, ticker, quantity, price):
    stock: KisStock = broker.stock(ticker)
    order = stock.sell(price=price, qty=quantity)

    logger.info(repr(order))


if __name__ == "__main__":
    logging.basicConfig(
        level=logging.DEBUG,
        format="%(asctime)s - %(name)s - %(levelname)s - %(message)s",
        handlers=[logging.StreamHandler(sys.stdout)],
    )

    # Read ACCOUNT from environment variable
    account = os.environ.get("ACCOUNT")
    if account is None:
        print("The ACCOUNT environment variable is not set.", file=sys.stderr)
        sys.exit(1)

    parser = argparse.ArgumentParser()
    parser.add_argument("ticker", type=str)
    parser.add_argument("quantity", type=float, nargs="?", default=None)

    args = parser.parse_args()

    kis = PyKis(f"accounts/{account}/auth.json", keep_token=True)
    request(kis, args.ticker, args.quantity)
