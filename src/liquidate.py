import argparse
from decimal import Decimal
import os
from dotenv import load_dotenv

from pykis import PyKis
from pykis.api.account.balance import KisDeposit, KisBalanceStock
from pykis.api.stock.market import CURRENCY_TYPE, MARKET_CURRENCY_MAP
from pykis import PyKis, KisBalance, KisAccount, KisStock
from pykis.api.account.order import order, ensure_price

from broker import Broker
from balance import Balance
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
    # Check if stock has 'market' property before filtering
    stocks_to_sell = [
        stock
        for stock in balance.stocks
        if hasattr(stock, "market") and stock.market == exchange
    ]

    if not stocks_to_sell:
        print(f"{exchange} 시장에 매도할 주식이 없습니다.")
        return

    for stock in stocks_to_sell:
        ticker = stock.symbol
        price = stock.price
        quantity = stock.quantity

        if quantity > 0:
            print(f"{exchange} 시장 {ticker} {quantity}주를 {price}에 매도합니다.")
            order(
                kis,
                kis.account().account_number,
                exchange,
                ticker,
                "sell",
                ensure_price(price, 0 if exchange == "KRX" else 2),
                quantity,
            )

            time.sleep(1)


if __name__ == "__main__":

    main()
