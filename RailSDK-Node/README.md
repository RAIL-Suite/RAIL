# @liquid-os/sdk

Universal SDK for AI-driven control of Node.js applications.

## Installation

```bash
npm install @liquid-os/sdk
```

## Usage

```typescript
import { ignite } from '@liquid-os/sdk';

class MyApp {
    processOrder(orderId: number, quantity: number): string {
        return `Order ${orderId} processed for ${quantity} items`;
    }
    
    getInventory(productId: string): object {
        return { productId, stock: 150 };
    }
}

const app = new MyApp();
ignite(app);  // That's it!
```

## How It Works

1. `ignite()` discovers all public methods on your instance
2. Generates a manifest with method signatures
3. Connects to Liquid Host via named pipe
4. When an LLM calls a function, it's executed on your instance

## Requirements

- Node.js 16+
- Liquid Host service running
- RailBridge native library (included in package)


