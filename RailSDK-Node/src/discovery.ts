/**
 * ============================================================================
 * METHOD DISCOVERY
 * ============================================================================
 * Auto-discovery of methods using JavaScript/TypeScript introspection.
 * Generates manifest JSON for LLM consumption.
 *
 * ============================================================================
 */

import { FunctionInfo, ParameterInfo, Manifest } from './types';
import { VERSION } from './version';

// ============================================================================
// TYPE MAPPING
// ============================================================================

/**
 * Map common JavaScript types to JSON Schema types.
 */
function inferType(value: unknown): string {
    if (value === null) return 'null';
    if (typeof value === 'undefined') return 'any';
    if (typeof value === 'string') return 'string';
    if (typeof value === 'number') return 'number';
    if (typeof value === 'boolean') return 'boolean';
    if (Array.isArray(value)) return 'array';
    if (typeof value === 'object') return 'object';
    return 'any';
}

// ============================================================================
// PARAMETER EXTRACTION
// ============================================================================

/**
 * Extract parameter names from a function.
 * Uses function.toString() parsing.
 */
function extractParams(fn: Function): ParameterInfo[] {
    const fnStr = fn.toString();

    // Match function parameters: function(a, b, c) or (a, b, c) =>
    const match = fnStr.match(/(?:function\s*\w*\s*)?\(([^)]*)\)/);
    if (!match || !match[1]) return [];

    const paramStr = match[1].trim();
    if (!paramStr) return [];

    const params: ParameterInfo[] = [];

    for (const part of paramStr.split(',')) {
        const trimmed = part.trim();
        if (!trimmed) continue;

        // Handle destructuring, defaults, etc.
        let name = trimmed
            .split(':')[0]  // Remove type annotation (TypeScript)
            .split('=')[0]  // Remove default value
            .trim();

        // Skip rest parameters marker
        if (name.startsWith('...')) {
            name = name.slice(3);
        }

        params.push({
            name,
            type: 'any',
            description: '',
            required: !trimmed.includes('=')  // Has default = optional
        });
    }

    return params;
}

// ============================================================================
// DISCOVERY FUNCTIONS
// ============================================================================

/**
 * Discover all callable methods on an object instance.
 */
export function discoverMethods(instance: object, includePrivate: boolean = false): FunctionInfo[] {
    const functions: FunctionInfo[] = [];

    // Get prototype methods
    const proto = Object.getPrototypeOf(instance);
    const methodNames = Object.getOwnPropertyNames(proto);

    for (const name of methodNames) {
        // Skip constructor and special methods
        if (name === 'constructor') continue;
        if (name.startsWith('__')) continue;
        if (name.startsWith('_') && !includePrivate) continue;

        const method = (instance as any)[name];
        if (typeof method !== 'function') continue;

        const funcInfo: FunctionInfo = {
            name,
            description: '',  // Could extract from JSDoc if available
            parameters: extractParams(method)
        };

        functions.push(funcInfo);
    }

    // Also get own properties that are functions
    for (const name of Object.keys(instance)) {
        if (name.startsWith('_') && !includePrivate) continue;

        const method = (instance as any)[name];
        if (typeof method !== 'function') continue;

        // Skip if already added from prototype
        if (functions.some(f => f.name === name)) continue;

        const funcInfo: FunctionInfo = {
            name,
            description: '',
            parameters: extractParams(method)
        };

        functions.push(funcInfo);
    }

    return functions;
}

/**
 * Generate a complete manifest for an object instance.
 */
export function generateManifest(
    instance: object,
    context?: string,
    includePrivate: boolean = false
): Manifest {
    const functions = discoverMethods(instance, includePrivate);

    return {
        processId: process.pid,
        language: 'node',
        sdkVersion: VERSION,
        context: context || instance.constructor.name,
        functions
    };
}


