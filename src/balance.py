import argparse
import logging
import os
import pickle
import sys
import tempfile
from datetime import datetime, timedelta

from pykis import PyKis, KisAuth, KisAccount, KisBalance

logger = logging.getLogger("balance")


def retrieve(broker: PyKis, force_refresh=False):
    data = broker.account().balance()
    logger.debug(f"Retrieved")

    print(repr(data))

    return data


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

    kis = PyKis(f"accounts/{account}/auth.json", keep_token=True)
    retrieve(kis, True)
