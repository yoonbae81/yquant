# yQuant

This repository offers a modular Python framework for fully automated stock trading in the Korean market, triggered by TradingView webhook alerts. The system is designed to interface directly with Korea Investment & Securities (한국투자증권) via the robust [python-kis](https://github.com/Soju06/python-kis) library.

**Key Features**:

- FastAPI-based webhook server for secure, real-time reception of TradingView alerts

- Seamless integration with TradingView’s webhook functionality for automated trade execution based on Pine Script strategies or indicators

- Broker abstraction layer utilizing [python-kis](https://github.com/Soju06/python-kis) for reliable, type-safe access to Korea Investment & Securities’ Open API

- Flexible risk management module for dynamic position sizing based on account balance and stop-loss

- Independent, script-based modules for price checking, order execution, and account management

- Environment variable configuration for sensitive credentials

- Detailed logging and robust error handling
