"""
Core Schema Definitions
Pydantic models for request/response and tool declarations
"""
from pydantic import BaseModel
from typing import List, Dict, Any, Optional

class ChatReq(BaseModel):
    """Chat request from frontend"""
    message: str

class ToolDeclaration(BaseModel):
    """Tool declaration for LLM"""
    name: str
    description: str
    parameters: Dict[str, Any]

class FunctionResult(BaseModel):
    """Result from function execution"""
    success: bool
    result: Any
    error: Optional[str] = None


