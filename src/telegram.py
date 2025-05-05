#!/usr/bin/env python3
import os
import logging
import threading
import requests

TELEGRAM_TOKEN = os.getenv("TELEGRAM_TOKEN")
TELEGRAM_CHAT_ID = os.getenv("TELEGRAM_CHAT_ID")

logging.getLogger("urllib3.connectionpool").setLevel(logging.WARNING)


class TelegramHandler(logging.Handler):
    def emit(self, record):
        log_entry = self.format(record)
        thread = threading.Thread(target=self.send, args=(log_entry,))
        thread.daemon = True  # Daemon thread will exit when the main program exits

        thread.start()

    def send(self, log_entry):
        url = f"https://api.telegram.org/bot{TELEGRAM_TOKEN}/sendMessage"
        data = {"chat_id": TELEGRAM_CHAT_ID, "text": log_entry}

        try:
            requests.post(url, data=data, timeout=10)
        except Exception as e:
            print(f"Failed to send log to Telegram: {e}")
