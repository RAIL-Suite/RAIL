# Liquid SDK for Python

Universal SDK enabling AI-driven control of Python applications.

## Installation

```bash
pip install liquid-sdk
```

## Usage

```python
import liquid

class MyApp:
    def process_order(self, order_id: int, quantity: int) -> str:
        """Process a customer order."""
        return f"Order {order_id} processed for {quantity} items"
    
    def get_inventory(self, product_id: str) -> dict:
        """Get inventory status for a product."""
        return {"product_id": product_id, "stock": 150}

app = MyApp()
liquid.ignite(app)  # That's it!
```

## How It Works

1. `ignite()` discovers all public methods on your instance
2. Generates a manifest with method signatures and docstrings
3. Connects to Liquid Host via named pipe
4. When an LLM calls a function, it's executed on your instance

## Requirements

- Python 3.8+
- Liquid Host service running
- RailBridge.dll (included in package)

## License

MIT

