from decimal import Decimal
import logging
import re
import time
from pykis import PyKis
from pykis.api.account.balance import KisDeposit, KisBalanceStock
from pykis.api.stock.market import CURRENCY_TYPE, MARKET_CURRENCY_MAP
from pykis import PyKis, KisBalance, KisAccount, KisStock

BALANCE_CACHE_TIMEOUT = 300  # 5 minutes

logger = logging.getLogger("balance")


class Balance:
    def __init__(self, kis: PyKis):
        self._kis = kis
        self._deposits_cache: dict[CURRENCY_TYPE, KisDeposit]
        self._stocks_cache: list[KisBalanceStock]
        self._cache_timestamp: float

    def update(self):
        logger.debug("Updating balance from KIS API")
        _balance = self._kis.account().balance()

        self._deposits_cache = _balance.deposits
        self._stocks_cache = _balance.stocks
        self._cache_timestamp = time.time()

    def _ensure_cache(self):
        if (time.time() - getattr(self, "_cache_timestamp", 0)) > BALANCE_CACHE_TIMEOUT:
            self.update()
            logger.debug("Balance cache updated")

    @property
    def deposits(self) -> dict[CURRENCY_TYPE, KisDeposit]:
        self._ensure_cache()
        return self._deposits_cache

    @property
    def stocks(self) -> list[KisBalanceStock]:
        self._ensure_cache()
        return self._stocks_cache

    @staticmethod
    def determine_currency(ticker: str) -> CURRENCY_TYPE:
        """
        Function to determine the currency of a given stock ticker.
        """
        ticker = ticker.strip().upper()

        if re.fullmatch(r"[A-Z]{1,5}", ticker):
            return "USD"
        elif re.fullmatch(r"\d{6}", ticker):
            return "KRW"
        elif ticker.endswith(".T"):
            return "JPY"
        elif ticker.endswith(".HK"):
            return "HKD"
        else:
            raise ValueError(f"Unknown currency for ticker: {ticker}")

    def get_cash(self, currency: CURRENCY_TYPE) -> Decimal:
        deposit = self.deposits.get(currency)
        if deposit:
            return deposit.amount
        else:
            return Decimal(0)

    def get_exposed(self, currency: CURRENCY_TYPE) -> Decimal:
        total = Decimal(0)

        markets = {
            market for market, cur in MARKET_CURRENCY_MAP.items() if cur == currency
        }
        for stock in self.stocks:
            if stock.market in markets:
                total += stock.price * stock.quantity

        return total

    def get_total_assets(self, currency: CURRENCY_TYPE) -> Decimal:
        return self.get_cash(currency) + self.get_exposed(currency)

    def get_amount(self, ticker) -> Decimal:
        total = Decimal(0)
        for stock in self.stocks:
            if stock.symbol == ticker:
                total += stock.amount
        return total

    def get_quantity(self, ticker) -> Decimal:
        for stock in self.stocks:
            if stock.symbol == ticker:
                return stock.quantity
        return Decimal(0)

    def buy_quantity(self, ticker: str, price: Decimal, strength: int) -> int:
        """
        Calculate the quantity of a stock to buy based on allocation and strength.

        Args:
            ticker (str): Stock ticker.
            price (Decimal): Price per share.
            strength (int): Strength factor (0-10).

        Returns:
            int: Quantity to buy.
        """
        MAX_ALLOCATION_RATIO = Decimal("0.1")
        logger.debug(f"MAX_ALLOCATION_RATIO set to {MAX_ALLOCATION_RATIO}")

        currency = self.determine_currency(ticker)
        logger.debug(f"Determined currency for {ticker}: {currency}")

        cash = self.get_cash(currency)
        logger.debug(f"Available cash in {currency}: {cash}")

        total_assets = self.get_total_assets(currency)
        logger.debug(f"Total assets in {currency}: {total_assets}")

        current_amount = self.get_amount(ticker)
        logger.debug(f"Current amount for {ticker}: {current_amount}")

        max_allocatable = total_assets * MAX_ALLOCATION_RATIO
        logger.debug(f"Max allocatable for {ticker}: {max_allocatable}")

        remaining_alloc = max(Decimal(0), max_allocatable - current_amount)
        logger.debug(f"Remaining allocation for {ticker}: {remaining_alloc}")

        usable_cash = min(cash, remaining_alloc)
        logger.debug(f"Usable cash for {ticker}: {usable_cash}")

        allocation_factor = Decimal(strength / 10)
        logger.debug(f"Allocation factor for strength {strength}: {allocation_factor}")

        allocated_cash = usable_cash * allocation_factor
        logger.debug(f"Allocated cash for {ticker}: {allocated_cash}")

        quantity = int(allocated_cash // price)
        logger.debug(
            f"Calculated quantity to buy for {ticker} at price {price}: {quantity}"
        )

        # return max(0, quantity)
        return 1

    def sell_quantity(self, ticker: str, price: Decimal, strength: int) -> int:
        if self.get_quantity(ticker) == 0:
            return 0

        allocation_factor = Decimal(strength / 10)
        allocated_cash = self.get_amount(ticker) * allocation_factor

        quantity = int(allocated_cash // price)
        return min(1, quantity)

    def __repr__(self) -> str:
        deposits_str = ", ".join(
            f"{currency}: {deposit.amount:,.2f}"
            for currency, deposit in (self.deposits or {}).items()
        )
        stocks_str = "\n".join(
            f"{stock.symbol}: qty={stock.quantity}, price={stock.price}, amount={stock.amount}"
            for stock in (self.stocks or [])
        )
        return f"<Balance>\nDeposits: {deposits_str}\nStocks:\n{stocks_str}"
