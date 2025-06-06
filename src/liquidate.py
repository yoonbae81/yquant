import argparse
from decimal import Decimal
import os
from dotenv import load_dotenv

from pykis import PyKis
from pykis.api.account.balance import KisDeposit, KisBalanceStock
from pykis.api.stock.market import CURRENCY_TYPE, MARKET_CURRENCY_MAP
from pykis import PyKis, KisBalance, KisAccount, KisStock

from balance import Balance
from buy import publish_order
import time

load_dotenv()
ACCOUNTS_DIR = os.getenv("ACCOUNTS_DIR", "accounts")


def main():
    parser = argparse.ArgumentParser(description="특정 시장의 모든 주식 매도")
    parser.add_argument("account", type=str)
    parser.add_argument(
        "exchange", type=str, choices=["KRX", "AMEX"], help="시장 코드 (KRX 또는 AMEX)"
    )
    args = parser.parse_args()

    account_path = os.path.join(ACCOUNTS_DIR, args.account)
    auth_path = os.path.join(account_path, "auth.json")

    kis = PyKis(auth_path, keep_token=True)
    balance = Balance(kis)

    exchange = args.exchange.upper()

    stocks_to_sell = [
        stock
        for stock in balance.stocks
        if hasattr(stock, "market") and stock.market == exchange
    ]

    if not stocks_to_sell:
        print(f"Nothing to sell in {exchange}")
        return

    for stock in stocks_to_sell:
        if stock.quantity == 0:
            continue

        print(f"Selling: {exchange}:{stock.symbol} {stock.quantity} @{stock.price:.2f}")
        publish_order(
            args.account,
            "sell",
            stock.symbol,
            int(stock.quantity),
            float(stock.price),
        )


if __name__ == "__main__":
    main()
