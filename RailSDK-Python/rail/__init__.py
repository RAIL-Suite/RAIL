# ============================================================================
# Rail SDK FOR PYTHON
# ============================================================================
# Universal SDK enabling AI-driven control of Python applications.
# One line of code to enable LLM orchestration of your app.
#
# Usage:
#     import rail
#
#     class MyApp:
#         def process_order(self, order_id: int, quantity: int) -> str:
#             """Process a customer order."""
#             return f"Order {order_id} processed for {quantity} items"
#
#     app = MyApp()
#     Rail.ignite(app)  # That's it!
#
# ============================================================================

from .core import ignite, disconnect, is_connected
from .discovery import discover_methods
from .version import __version__

__all__ = [
    "ignite",
    "disconnect", 
    "is_connected",
    "discover_methods",
    "__version__"
]


