/**
 * ============================================================================
 * NATIVE BRIDGE INTERFACE
 * ============================================================================
 * ffi-napi wrapper for RailBridge native library.
 * Handles loading the native library and managing callbacks.
 *
 * IMPORTANT: Node.js is single-threaded. Callbacks from the native bridge
 * thread are handled via ffi-napi's built-in thread safety mechanisms.
 *
 * ============================================================================
 */

import * as path from 'path';
import * as ffi from 'ffi-napi';
import * as ref from 'ref-napi';

// ============================================================================
// TYPE DEFINITIONS
// ============================================================================

// const char* (*RailCommandCallback)(const char* commandJson)
const CallbackType = ffi.Function('string', ['string']);

// ============================================================================
// LIBRARY LOADING
// ============================================================================

function getLibraryPath(): string {
    const platform = process.platform;
    let libName: string;

    switch (platform) {
        case 'win32':
            libName = 'RailBridge.dll';
            break;
        case 'linux':
            libName = 'libRailBridge.so';
            break;
        case 'darwin':
            libName = 'libRailBridge.dylib';
            break;
        default:
            throw new Error(`Unsupported platform: ${platform}`);
    }

    // Try multiple locations
    const candidates = [
        path.join(__dirname, '..', 'prebuilds', `${platform}-${process.arch}`, libName),
        path.join(__dirname, '..', libName),
        path.join(__dirname, libName),
    ];

    // For now, return the first candidate (in production, check existence)
    return candidates[0];
}

// ============================================================================
// BRIDGE CLASS
// ============================================================================

/**
 * Wrapper for the native RailBridge library.
 */
class NativeBridge {
    private static instance: NativeBridge | null = null;
    private lib: any;
    private callback: any = null;
    private callbackFn: ((commandJson: string) => string) | null = null;

    private constructor() {
        const libPath = getLibraryPath();

        this.lib = ffi.Library(libPath, {
            'Rail_Ignite': ['int', ['string', 'string', 'pointer']],
            'Rail_Disconnect': ['void', []],
            'Rail_Heartbeat': ['int', []],
            'Rail_GetVersion': ['string', []],
            'Rail_IsConnected': ['int', []]
        });
    }

    static getInstance(): NativeBridge {
        if (!NativeBridge.instance) {
            NativeBridge.instance = new NativeBridge();
        }
        return NativeBridge.instance;
    }

    /**
     * Initialize connection to Host and start listening for commands.
     */
    ignite(instanceId: string, manifest: string, callback: (commandJson: string) => string): number {
        // Store callback to prevent garbage collection
        this.callbackFn = callback;

        // Create native callback
        this.callback = ffi.Callback('string', ['string'], (commandJson: string): string => {
            try {
                return this.callbackFn!(commandJson);
            } catch (e) {
                return JSON.stringify({ status: 'error', message: String(e) });
            }
        });

        // Keep callback alive
        process.on('exit', () => {
            // Reference to prevent GC
            this.callback;
        });

        return this.lib.Rail_Ignite(instanceId, manifest, this.callback);
    }

    /**
     * Disconnect from Host and cleanup.
     */
    disconnect(): void {
        this.lib.Rail_Disconnect();
        this.callback = null;
        this.callbackFn = null;
    }

    /**
     * Send heartbeat to Host.
     */
    heartbeat(): number {
        return this.lib.Rail_Heartbeat();
    }

    /**
     * Get native bridge version.
     */
    getVersion(): string {
        return this.lib.Rail_GetVersion() || 'unknown';
    }

    /**
     * Check if connected to Host.
     */
    isConnected(): boolean {
        return this.lib.Rail_IsConnected() === 1;
    }
}

/**
 * Get the singleton bridge instance.
 */
export function getBridge(): NativeBridge {
    return NativeBridge.getInstance();
}


