/**
 * ============================================================================
 * Rail SDK FOR NODE.JS
 * ============================================================================
 * Universal SDK enabling AI-driven control of Node.js applications.
 * One line of code to enable LLM orchestration of your app.
 *
 * Usage:
 *     import { ignite } from '@Rail-os/sdk';
 *
 *     class MyApp {
 *         processOrder(orderId: number, quantity: number): string {
 *             return `Order ${orderId} processed for ${quantity} items`;
 *         }
 *     }
 *
 *     const app = new MyApp();
 *     ignite(app);  // That's it!
 *
 * ============================================================================
 */

export { ignite, disconnect, isConnected } from './core';
export { discoverMethods, generateManifest } from './discovery';
export { VERSION } from './version';

// Type exports
export type { FunctionInfo, ParameterInfo, Manifest } from './types';


