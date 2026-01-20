"""
Parser Factory - Selects appropriate parser based on file extension
"""
from pathlib import Path
from core.auto_import import BaseParser
from parsers.python_parser import PythonParser
from parsers.csharp_parser import CSharpParser

def get_parser(filename: str) -> BaseParser:
    """
    Factory function to select parser based on file extension
    
    Args:
        filename: Name of uploaded file (e.g., 'script.py')
        
    Returns:
        Appropriate parser instance
        
    Raises:
        ValueError: If extension is not supported
    """
    ext = Path(filename).suffix.lower()
    
    if ext == ".py":
        return PythonParser()
    elif ext == ".cs":
        return CSharpParser()
    else:
        raise ValueError(f"Unsupported file extension: {ext}. Supported: .py, .cs")


