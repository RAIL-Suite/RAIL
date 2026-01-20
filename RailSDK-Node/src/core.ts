/**
 * ============================================================================
 * Rail SDK CORE
 * ============================================================================
 * Main entry point for the Rail SDK. Provides the ignite() function
 * which is the only thing users need to call.
 *
 * Usage:
 *     import { ignite } from '@Rail-os/sdk';
 *     ignite(myAppInstance);
 *
 * ============================================================================
 */

import { v4 as uuidv4 } from 'uuid';
import { getBridge } from './bridge';
import { generateManifest } from './discovery';
import { CommandPayload, ResultPayload } from './types';

// ============================================================================
// GLOBAL STATE
// ============================================================================

let _instance: object | null = null;

// ============================================================================
// CALLBACK EXECUTOR
// ============================================================================

/**
 * Execute a command received from the Host.
 */
function executeCallback(commandJson: string): string {
    try {
        const command: CommandPayload = JSON.parse(commandJson);
        const methodName = command.method;
        const args = command.args || {};

        if (!_instance) {
            const result: ResultPayload = {
                status: 'error',
                message: 'No instance registered'
            };
            return JSON.stringify(result);
        }

        // Get the method
        const method = (_instance as any)[methodName];
        if (typeof method !== 'function') {
            const result: ResultPayload = {
                status: 'error',
                message: `Method not found: ${methodName}`
            };
            return JSON.stringify(result);
        }

        // Execute the method
        let returnValue: unknown;
        if (typeof args === 'object' && !Array.isArray(args)) {
            // Named parameters - call with object
            returnValue = method.call(_instance, ...Object.values(args));
        } else if (Array.isArray(args)) {
            // Positional parameters
            returnValue = method.apply(_instance, args);
        } else {
            returnValue = method.call(_instance);
        }

        // Serialize result
        const result: ResultPayload = {
            status: 'success',
            result: returnValue
        };
        return JSON.stringify(result);

    } catch (e) {
        const result: ResultPayload = {
            status: 'error',
            message: String(e)
        };
        return JSON.stringify(result);
    }
}

// ============================================================================
// PUBLIC API
// ============================================================================

/**
 * Error codes from the native bridge.
 */
const ErrorMessages: Record<number, string> = {
    [-1]: 'Invalid argument',
    [-2]: 'Null callback',
    [-3]: 'Already initialized',
    [-4]: 'Not initialized',
    [-5]: 'Connection failed - Is Rail Host running?',
    [-6]: 'Pipe broken',
    [-7]: 'Connection timeout',
    [-99]: 'Unknown error'
};

/**
 * Register an object with the Rail Host for AI-driven control.
 *
 * This is the main entry point for the Rail SDK. Call this once
 * with your application instance, and all its public methods become
 * available to LLMs.
 *
 * @param instance - Object instance whose methods will be exposed
 * @param options - Optional configuration
 * @returns Promise that resolves when connected
 *
 * @example
 * ```typescript
 * class MyApp {
 *     processOrder(orderId: number): string {
 *         return `Processed ${orderId}`;
 *     }
 * }
 *
 * const app = new MyApp();
 * await ignite(app);  // That's it!
 * ```
 */
export function ignite(
    instance: object,
    options?: {
        context?: string;
        includePrivate?: boolean;
    }
): void {
    if (_instance !== null) {
        throw new Error('Already ignited. Call disconnect() first.');
    }

    _instance = instance;

    try {
        // Generate manifest
        const manifest = generateManifest(
            instance,
            options?.context,
            options?.includePrivate ?? false
        );
        const manifestJson = JSON.stringify(manifest);

        // Generate unique instance ID
        const instanceId = uuidv4();

        // Connect to Host
        const bridge = getBridge();
        const result = bridge.ignite(instanceId, manifestJson, executeCallback);

        if (result !== 0) {
            _instance = null;
            const errorMsg = ErrorMessages[result] || `Error code: ${result}`;
            throw new Error(`Failed to ignite: ${errorMsg}`);
        }

    } catch (e) {
        _instance = null;
        throw e;
    }
}

/**
 * Disconnect from the Rail Host and cleanup.
 */
export function disconnect(): void {
    _instance = null;
    const bridge = getBridge();
    bridge.disconnect();
}

/**
 * Check if currently connected to the Rail Host.
 */
export function isConnected(): boolean {
    const bridge = getBridge();
    return bridge.isConnected();
}


