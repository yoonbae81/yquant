#!/usr/bin/env python3
import argparse
from buy import publish_order


def main(args):
    publish_order(args.account, "sell", args.ticker, args.quantity, args.price)


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("account", type=str)
    parser.add_argument("ticker", type=str)
    parser.add_argument("quantity", type=int)
    parser.add_argument("price", type=float)
    args = parser.parse_args()

    main(args)
