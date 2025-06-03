#!/usr/bin/env python3
import argparse
from buy import publish_order


def main(account: str, ticker: str, quantity: int):
    publish_order(account, "sell", ticker, quantity)


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("account", type=str)
    parser.add_argument("ticker", type=str)
    parser.add_argument("quantity", type=int)
    args = parser.parse_args()

    main(args.account, args.ticker, args.quantity)
