/**
 * ============================================================================
 * TYPE DEFINITIONS FOR Rail SDK
 * ============================================================================
 * TypeScript interfaces and types for type-safe usage.
 *
 * ============================================================================
 */

/**
 * Parameter descriptor for a function.
 */
export interface ParameterInfo {
    name: string;
    type: string;
    description: string;
    required: boolean;
}

/**
 * Function descriptor for manifest.
 */
export interface FunctionInfo {
    name: string;
    description: string;
    parameters: ParameterInfo[];
}

/**
 * Complete manifest sent to Host.
 */
export interface Manifest {
    processId: number;
    language: string;
    sdkVersion: string;
    context?: string;
    functions: FunctionInfo[];
}

/**
 * Command received from Host.
 */
export interface CommandPayload {
    method: string;
    args: Record<string, unknown>;
}

/**
 * Result to send back to Host.
 */
export interface ResultPayload {
    status: 'success' | 'error';
    result?: unknown;
    message?: string;
}


