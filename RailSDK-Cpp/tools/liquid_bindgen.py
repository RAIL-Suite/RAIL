#!/usr/bin/env python3
"""
============================================================================
Rail BINDING GENERATOR FOR C++
============================================================================
Parses C++ headers using libclang and generates binding code.

Usage:
    python Rail_bindgen.py --headers include/*.h --output bindings.cpp

============================================================================
"""

import argparse
import re
import sys
from pathlib import Path
from typing import List, Tuple, Optional

# ============================================================================
# SIMPLE HEADER PARSER (without libclang dependency)
# ============================================================================

class MethodInfo:
    def __init__(self, class_name: str, method_name: str, return_type: str, params: List[Tuple[str, str]]):
        self.class_name = class_name
        self.method_name = method_name
        self.return_type = return_type
        self.params = params  # List of (type, name)

def parse_header_simple(header_path: Path) -> List[MethodInfo]:
    """
    Simple regex-based C++ header parser.
    For production, use libclang for accurate parsing.
    """
    methods = []
    content = header_path.read_text(encoding='utf-8', errors='ignore')
    
    # Find class definitions
    class_pattern = r'class\s+(\w+)\s*(?::\s*(?:public|private|protected)\s+\w+)?\s*\{'
    method_pattern = r'(\w+(?:\s*\*)?)\s+(\w+)\s*\(([^)]*)\)\s*(?:const)?\s*(?:override)?\s*[;{]'
    
    current_class = None
    brace_count = 0
    
    lines = content.split('\n')
    for line in lines:
        # Track class scope
        class_match = re.search(class_pattern, line)
        if class_match:
            current_class = class_match.group(1)
            brace_count = 0
        
        # Track braces for scope
        brace_count += line.count('{') - line.count('}')
        if brace_count <= 0:
            current_class = None
        
        # Find methods
        if current_class:
            method_match = re.search(method_pattern, line)
            if method_match:
                return_type = method_match.group(1).strip()
                method_name = method_match.group(2).strip()
                params_str = method_match.group(3).strip()
                
                # Skip constructors, destructors, operators
                if method_name == current_class or method_name.startswith('~'):
                    continue
                if method_name.startswith('operator'):
                    continue
                
                # Skip private/internal methods
                if method_name.startswith('_'):
                    continue
                
                # Parse parameters
                params = []
                if params_str:
                    for param in params_str.split(','):
                        param = param.strip()
                        if param:
                            parts = param.rsplit(' ', 1)
                            if len(parts) == 2:
                                params.append((parts[0].strip(), parts[1].strip()))
                
                methods.append(MethodInfo(current_class, method_name, return_type, params))
    
    return methods

# ============================================================================
# BINDING CODE GENERATION
# ============================================================================

def generate_bindings(methods: List[MethodInfo]) -> str:
    """Generate C++ binding registration code."""
    lines = [
        "// ============================================================================",
        "// AUTO-GENERATED Rail BINDINGS - DO NOT EDIT",
        "// ============================================================================",
        "",
        "#include <Rail/Rail.h>",
        "#include <nlohmann/json.hpp>",
        "",
        "using json = nlohmann::json;",
        "",
    ]
    
    # Group by class
    by_class = {}
    for method in methods:
        if method.class_name not in by_class:
            by_class[method.class_name] = []
        by_class[method.class_name].append(method)
    
    # Generate includes (would need actual headers in practice)
    lines.append("// Forward declarations")
    for class_name in by_class:
        lines.append(f"class {class_name};")
    lines.append("")
    
    # Generate registration function
    lines.append("void Rail_register_all() {")
    
    for class_name, class_methods in by_class.items():
        for method in class_methods:
            full_name = f"{class_name}::{method.method_name}"
            lines.append(f'    Rail::register_method("{class_name}", "{method.method_name}", "",')
            lines.append(f'        [](const std::string& cmd_json) -> std::string {{')
            lines.append(f'            // TODO: Implement dispatch for {full_name}')
            lines.append(f'            return R"({{"status": "error", "message": "Not implemented"}})";')
            lines.append(f'        }});')
            lines.append("")
    
    lines.append("}")
    
    return "\n".join(lines)

# ============================================================================
# MAIN
# ============================================================================

def main():
    parser = argparse.ArgumentParser(description='Generate Rail C++ bindings')
    parser.add_argument('--headers', nargs='+', required=True, help='Header files to parse')
    parser.add_argument('--output', required=True, help='Output file path')
    
    args = parser.parse_args()
    
    all_methods = []
    
    for header_path in args.headers:
        path = Path(header_path)
        if path.exists():
            methods = parse_header_simple(path)
            all_methods.extend(methods)
            print(f"Parsed {path.name}: found {len(methods)} methods")
    
    binding_code = generate_bindings(all_methods)
    
    output_path = Path(args.output)
    output_path.parent.mkdir(parents=True, exist_ok=True)
    output_path.write_text(binding_code, encoding='utf-8')
    
    print(f"Generated bindings: {output_path}")
    print(f"Total methods registered: {len(all_methods)}")

if __name__ == '__main__':
    main()


