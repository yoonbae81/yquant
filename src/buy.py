#!/usr/bin/env python3
import argparse
import os
import json
import redis
import re
from dotenv import load_dotenv

load_dotenv()
REDIS_URL = os.getenv("REDIS_URL", "redis://localhost:6379/0")
ACCOUNTS_DIR = os.getenv("ACCOUNTS_DIR", "accounts")


def get_exchange(ticker: str) -> str:
    return "KRX" if re.fullmatch(r"\d{6}", ticker) else "AMEX"


def publish_order(account: str, action: str, ticker: str, quantity: int, price: float):
    order_data = {
        "account": account,
        "action": action,
        "exchange": get_exchange(ticker),
        "ticker": ticker,
        "quantity": quantity,
        "price": price,
    }

    r = redis.from_url(REDIS_URL, decode_responses=True)
    r.publish("order", json.dumps(order_data))
    print(f"Requested: {order_data}")


def main(args):
    publish_order(args.account, "buy", args.ticker, args.quantity, args.price)


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("account", type=str)
    parser.add_argument("ticker", type=str)
    parser.add_argument("quantity", type=int)
    parser.add_argument("price", type=float)
    args = parser.parse_args()

    main(args)
