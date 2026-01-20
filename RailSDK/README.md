# Liquid SDK

**Python SDK for Liquid Factory** - Transform code into portable, executable artifacts.

## 🎯 What is This?

Liquid SDK is a clean, SDK-only version of Liquid Factory that enables:
- **Building artifacts** from Python/C# code
- **Loading artifacts** instantly (<10ms)
- **Executing functions** from artifacts via Python or C# SDK

**No web components** - This is purely SDK/CLI focused.

## 📦 Components

### Core Modules
- **`core/`** - Interfaces, exceptions, manifest schemas
- **`parsers/`** - Python & C# code parsers (AST-based)
- **`runners/`** - Python & .NET execution engines

### Public APIs
- **`runtime/`** - Python SDK library (`LiquidRuntime` class)
- **`builder/`** - CLI tool for building artifacts
- **`cli/`** - CLI executor (used by C# `RailEngine`)

## 🚀 Quick Start

### Install Dependencies
```bash
pip install -r requirements.txt
```

### Build an Artifact (Python API)
```python
from builder.artifact_generator import ArtifactGenerator

# Build artifact from source
generator = ArtifactGenerator()
artifact_path = generator.generate_artifact(
    source_file="my_script.py",
    output_dir="./artifacts"
)
```

### Load and Execute (Python SDK)
```python
from runtime import LiquidRuntime

# Load artifact
runtime = LiquidRuntime("./artifacts/my_script")
runtime.load()

# Execute function
result = runtime.execute("my_function", {"arg1": "value"})
print(result)
```

### Use from C# (.NET SDK)
```csharp
using RailFactory.Core;

// Load artifact
var engine = new RailEngine(@"C:\Artifacts\MyTool");
var toolsJson = engine.Load();

// Execute function
var result = engine.Execute("calculate", "{ \"x\": 5, \"y\": 3 }");
Console.WriteLine(result);
```

## 📁 Architecture

```
RailSDK/
├── core/           → Interfaces, manifest, exceptions
├── parsers/        → Python/C# parsers
├── runners/        → Python/.NET execution engines
├── runtime/        → Python SDK (LiquidRuntime class)
├── builder/        → CLI artifact builder
└── cli/            → CLI executor (for RailEngine.cs)
```

## 🔧 Requirements

- **Python 3.10+**
- **Optional:** .NET 8.0+ (for C# artifact execution)
- **Optional:** pythonnet (for C# support in Python)

## 📖 Documentation

See `implementation_plan.md` for full architectural details.

## 🧹 What Was Removed

This is a cleaned version of `liquid-factory/` with all web-app components removed:
- ❌ FastAPI server (`main.py`)
- ❌ Web UI (`liquid-ui/`)
- ❌ LLM integration (`intelligence/`)
- ❌ Web configuration

**Result:** ~400MB lighter, SDK-focused, production-ready.

---

**License:** MIT

