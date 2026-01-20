"""
Rail Bridge - Universal Runtime CLI
Enterprise Polyglot Execution Engine

This is the CLI entry point for executing Rail artifacts from external applications 
(e.g., C# RailEngine, Node.js clients, Java apps, etc.)

Usage:
    python runtime.py --artifact <path> --func <name> --args <json>

Example:
    python runtime.py --artifact "C:/Output/MyTool" --func "add" --args '{"a": 5, "b": 3}'

Architecture:
    1. Loads the manifest to detect language (python, csharp, javascript, etc.)
    2. Instantiates the appropriate runner (PythonRunner, DotNetRunner, etc.)
    3. Executes the function and prints result to stdout (JSON format)
    4. Exit code 0 = success, 1 = error

This enables ANY language to integrate with Rail Factory:
    - C# → Process.Start → Python runtime.py → stdout
    - Node.js → child_process.spawn → Python runtime.py → stdout
    - Java → ProcessBuilder → Python runtime.py → stdout

ZERO DEPENDENCIES: This module uses only Python stdlib (no pydantic, no external libs)
to ensure maximum portability and instant execution.

EMBEDDED PACKAGES: Automatically discovers and loads pythonnet from bundled python-libs
directory for zero-install C# support.
"""

import argparse
import json
import sys
from pathlib import Path
from dataclasses import dataclass
from typing import List, Dict, Any

# ============================================================================
# EMBEDDED PACKAGE DISCOVERY
# ============================================================================
# Automatically add embedded python packages to sys.path
# This allows distribution with pre-installed pythonnet without requiring pip install

def discover_embedded_packages():
    """
    Discovers and adds embedded Python packages to sys.path.
    
    Looks for python-libs directory in the following locations:
    1. Next to runtime.py (Rail-bridge/python-libs)
    2. Parent directory (for bundled distribution)
    3. Two levels up (for flexible deployment)
    """
    runtime_dir = Path(__file__).parent
    
    # Search locations for embedded packages
    search_paths = [
        runtime_dir / "python-libs",                    # Rail-bridge/python-libs
        runtime_dir.parent / "python-libs",             # one level up
        runtime_dir.parent.parent / "python-libs",      # two levels up
    ]
    
    for embedded_path in search_paths:
        if embedded_path.exists() and embedded_path.is_dir():
            embedded_path_str = str(embedded_path.resolve())
            if embedded_path_str not in sys.path:
                sys.path.insert(0, embedded_path_str)
                # Also add to PYTHONPATH for subprocess calls
                import os
                python_path = os.environ.get('PYTHONPATH', '')
                if embedded_path_str not in python_path:
                    os.environ['PYTHONPATH'] = f"{embedded_path_str}{os.pathsep}{python_path}"
            return embedded_path
    
    return None

# Discover embedded packages BEFORE any imports that might need them
embedded_libs = discover_embedded_packages()

# Add parent directory to path for imports (RailSDK root)
sdk_root = Path(__file__).parent.parent
sys.path.insert(0, str(sdk_root))

# Import runners with conditional handling
from runners.python_runner import PythonRunner

# DotNetRunner requires pythonnet - only import if available
try:
    from runners.dotnet_runner import CSharpRunner
    DOTNET_AVAILABLE = True
except ImportError:
    DOTNET_AVAILABLE = False
    CSharpRunner = None

# Import lightweight schemas (zero Pydantic dependency)
from core.schema_lite import ToolDeclarationLite
from core.manifest_lite import ManifestSchemaLite, ArtifactMetadataLite



def load_manifest(artifact_path: str) -> ManifestSchemaLite:
    """
    Load and parse the Rail.manifest.json file
    
    Args:
        artifact_path: Path to artifact directory
        
    Returns:
        ManifestSchemaLite object
        
    Raises:
        FileNotFoundError: If manifest doesn't exist
        ValueError: If manifest is invalid
    """
    manifest_file = Path(artifact_path) / "Rail.manifest.json"
    
    if not manifest_file.exists():
        raise FileNotFoundError(f"Manifest not found: {manifest_file}")
    
    # Use built-in loader from manifest_lite
    return ManifestSchemaLite.load_from_file(str(manifest_file))


def get_runner_for_language(language: str):
    """
    Get the appropriate runner instance for the specified language
    
    Args:
        language: Language identifier ("python", "csharp", "javascript", etc.)
        
    Returns:
        Runner instance (PythonRunner, DotNetRunner, etc.)
        
    Raises:
        ValueError: If language is not supported
    """
    language = language.lower()
    
    if language == "python":
        return PythonRunner()
    elif language == "csharp":
        if not DOTNET_AVAILABLE:
            raise ValueError(
                "C# execution requires pythonnet. Install with: pip install pythonnet\n"
                "For Python-only artifacts, pythonnet is not needed."
            )
        return CSharpRunner()
    # Future languages:
    # elif language == "javascript":
    #     return JavaScriptRunner()
    # elif language == "go":
    #     return GoRunner()
    else:
        raise ValueError(f"Unsupported language: {language}")


def execute_function(artifact_path: str, function_name: str, args_json: str) -> dict:
    """
    Universal function executor
    
    1. Loads manifest from artifact
    2. Detects language
    3. Instantiates runner
    4. Executes function
    5. Returns result
    
    Args:
        artifact_path: Path to artifact directory
        function_name: Name of function to execute
        args_json: JSON string of function arguments
        
    Returns:
        dict with {"status": "success", "result": <value>} or {"status": "error", "error": <msg>}
    """
    try:
        # 1. Load manifest
        manifest = load_manifest(artifact_path)
        
        # 2. Get runner for language
        runner = get_runner_for_language(manifest.language)
        
        # 3. Parse arguments
        try:
            args = json.loads(args_json)
        except json.JSONDecodeError as e:
            return {
                "status": "error",
                "error": f"Invalid JSON arguments: {str(e)}"
            }
        
        # 4. Get source file path from manifest
        source_file = Path(artifact_path) / manifest.entry_point
        
        if not source_file.exists():
            return {
                "status": "error",
                "error": f"Entry point not found: {manifest.entry_point}"
            }
        
        # 5. Load module
        with open(source_file, 'r', encoding='utf-8') as f:
            source_code = f.read()
        
        module = runner.load_module(source_code)
        
        # 6. Execute function
        result = runner.execute_function(module, function_name, args)
        
        # 7. Return success
        return {
            "status": "success",
            "result": result
        }
        
    except FileNotFoundError as e:
        return {
            "status": "error",
            "error": f"Artifact not found: {str(e)}"
        }
    except ValueError as e:
        return {
            "status": "error",
            "error": f"Validation error: {str(e)}"
        }
    except Exception as e:
        return {
            "status": "error",
            "error": f"Execution failed: {str(e)}",
            "type": type(e).__name__
        }


def main():
    """
    CLI entry point
    """
    # Parse arguments
    parser = argparse.ArgumentParser(
        description="Rail Runtime - Universal Polyglot Execution Engine",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""
Examples:
  python runtime.py --artifact "C:/Output/Calculator" --func "add" --args '{"a": 5, "b": 3}'
  python runtime.py --artifact "./plugins/DataAnalyzer" --func "analyze" --args '{"data": [1,2,3]}'
  
Supported Languages:
  - Python (.py)
  - C# (.cs)
  - JavaScript (.js) [Coming Soon]
  - Go (.go) [Coming Soon]
        """
    )
    
    parser.add_argument(
        '--artifact',
        required=True,
        help='Path to the Rail artifact directory (containing Rail.manifest.json)'
    )
    
    parser.add_argument(
        '--func',
        required=True,
        help='Name of the function to execute'
    )
    
    parser.add_argument(
        '--args',
        required=True,
        help='Function arguments as JSON string (e.g., \'{"a": 5, "b": 3}\')'
    )
    
    args = parser.parse_args()
    
    # Execute function
    result = execute_function(args.artifact, args.func, args.args)
    
    # Print result as JSON to stdout (this is what C# reads)
    print(json.dumps(result, ensure_ascii=False))
    
    # Exit with appropriate code
    sys.exit(0 if result["status"] == "success" else 1)


if __name__ == "__main__":
    main()


