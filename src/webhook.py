#!/usr/bin/env python3
import os
import logging
import sys
import uvicorn
import threading
import requests
from contextlib import asynccontextmanager
from dotenv import load_dotenv

import redis.asyncio
from fastapi import FastAPI, Depends, Request, HTTPException
from pydantic import BaseModel, field_validator

load_dotenv()
WEBHOOK_SECRET = os.getenv("WEBHOOK_SECRET")
REDIS_URL = os.getenv("REDIS_URL", "redis://localhost:6379/0")
TELEGRAM_TOKEN = os.getenv("TELEGRAM_TOKEN")
TELEGRAM_CHAT_ID = os.getenv("TELEGRAM_CHAT_ID")


logging.basicConfig(
    level=logging.DEBUG,
    format="%(asctime)s - %(name)s - %(levelname)s - %(message)s",
    handlers=[logging.StreamHandler(sys.stdout)],
)


class TelegramHandler(logging.Handler):
    def emit(self, record):
        log_entry = self.format(record)
        thread = threading.Thread(target=self.send, args=(log_entry,))
        thread.daemon = True  # Daemon thread will exit when the main program exits
        thread.start()

    @staticmethod
    def send(log_entry):
        url = f"https://api.telegram.org/bot{TELEGRAM_TOKEN}/sendMessage"
        data = {"chat_id": TELEGRAM_CHAT_ID, "text": log_entry}
        try:
            requests.post(url, data=data, timeout=10)
        except Exception as e:
            print(f"Failed to send log to Telegram: {e}")


class Signal(BaseModel):
    action: str
    exchange: str
    ticker: str
    currency: str
    price: float
    strength: int
    comment: str
    secret: str

    @field_validator("secret")
    def secret_must_same(cls, v):
        if v != WEBHOOK_SECRET:
            logger.warning(f"Invalid secret: {v}")
            raise ValueError(f"Invalid secret: {v}")
        return v


@asynccontextmanager
async def lifespan(app: FastAPI):
    """Manage app lifecycle and load data at startup."""
    app.state.redis = redis.asyncio.from_url(REDIS_URL, decode_responses=True)
    yield  # Cleanup logic below
    await app.state.redis.close()


def get_redis(request: Request) -> redis.asyncio.Redis:
    """Return account data from app state."""
    return request.app.state.redis


logger = logging.getLogger("webhook")
# logger.addHandler(TelegramHandler())
# logging.getLogger("urllib3.connectionpool").setLevel(logging.WARNING)

app = FastAPI(lifespan=lifespan)


@app.post("/webhook")
async def handle_webhook(signal: Signal, redis=Depends(get_redis)):
    logger.info(f"Signal: {signal}")
    del signal.secret

    try:
        await redis.publish("signal", signal.model_dump_json())
        return {"status": "ok"}

    except Exception as e:
        logger.error(f"{e}")
        raise HTTPException(status_code=400, detail=str(e))


if __name__ == "__main__":
    uvicorn.run("webhook:app", host="0.0.0.0", port=8000, reload=True)
