import sys
import os
from pathlib import Path
import clr
from System import AppDomain, ResolveEventHandler
from System.Reflection import Assembly

# Singleton state
_initialized = False
_resolve_handler = None

# Roslyn types (exposed for consumers)
CSharpSyntaxTree = None
CSharpSyntaxWalker = None
SyntaxKind = None
SyntaxNode = None

def get_lib_path():
    """Get path to local lib directory"""
    # Assuming this file is in Rail-bridge/core/
    # lib is in Rail-bridge/lib/
    return Path(__file__).parent.parent / "lib"

def _resolve_assembly(sender, args):
    """AssemblyResolve handler"""
    try:
        name = args.Name.split(",")[0]
        lib_path = get_lib_path()
        dll_path = lib_path / f"{name}.dll"
        if dll_path.exists():
            # print(f"DEBUG: Resolving {name} -> {dll_path}")
            return Assembly.LoadFrom(str(dll_path))
    except:
        pass
    return None

def initialize_roslyn():
    """
    Initialize Roslyn environment.
    Idempotent: safe to call multiple times.
    """
    global _initialized, _resolve_handler
    global CSharpSyntaxTree, CSharpSyntaxWalker, SyntaxKind, SyntaxNode
    
    if _initialized:
        return

    print("[INFO] Initializing Roslyn (Shared Loader)...")
    
    try:
        lib_path = get_lib_path()
        print(f"DEBUG: Lib path: {lib_path}")
        
        # Add to sys.path
        if lib_path.exists():
            if str(lib_path) not in sys.path:
                sys.path.append(str(lib_path))
        
        # Register Assembly Resolver
        _resolve_handler = ResolveEventHandler(_resolve_assembly)
        AppDomain.CurrentDomain.AssemblyResolve += _resolve_handler
        
        # Load Core Assemblies
        clr.AddReference("System")
        clr.AddReference("System.Runtime")
        clr.AddReference("System.Linq")
        clr.AddReference("System.Reflection")
        
        # Load Dependencies
        deps = [
            "System.Collections.Immutable.dll",
            "System.Reflection.Metadata.dll",
            "System.Runtime.CompilerServices.Unsafe.dll",
            "System.Text.Encoding.CodePages.dll",
            "System.Memory.dll",
            "System.Buffers.dll",
            "System.Threading.Tasks.Extensions.dll",
            "System.Numerics.Vectors.dll"
        ]
        for dep in deps:
            try:
                if (lib_path / dep).exists():
                    clr.AddReference(str(lib_path / dep))
            except:
                pass

        # Load Roslyn Assemblies
        # FORCE LOAD BY PATH: This is critical for pythonnet to map namespaces correctly
        # when DLLs are not in GAC.
        try:
            dll_path_common = lib_path / "Microsoft.CodeAnalysis.dll"
            dll_path_csharp = lib_path / "Microsoft.CodeAnalysis.CSharp.dll"
            
            if not dll_path_common.exists() or not dll_path_csharp.exists():
                raise FileNotFoundError(f"Roslyn DLLs not found in {lib_path}")

            print(f"DEBUG: Loading Roslyn from: {dll_path_common}")
            clr.AddReference(str(dll_path_common))
            
            print(f"DEBUG: Loading Roslyn C# from: {dll_path_csharp}")
            clr.AddReference(str(dll_path_csharp))
            
            print("DEBUG: Roslyn assemblies loaded by explicit path")
        except Exception as e:
            print(f"[CRITICAL] Failed to load Roslyn DLLs: {e}")
            raise

        # Import Namespaces
        print("DEBUG: Importing Roslyn namespaces...")
        try:
            import Microsoft.CodeAnalysis.CSharp as CS
            from Microsoft.CodeAnalysis import SyntaxNode as SN
            
            # Export types
            CSharpSyntaxTree = CS.CSharpSyntaxTree
            CSharpSyntaxWalker = CS.CSharpSyntaxWalker
            SyntaxKind = CS.SyntaxKind
            SyntaxNode = SN
            
            print("[OK] Roslyn Imports Successful")
            
        except ImportError as e:
            print(f"[WARN] Import failed ({e}), attempting Reflection Fallback...")
            
            try:
                from System import Type
                
                # Helper to get type safely
                def get_type(name, asm_name):
                    full_name = f"{name}, {asm_name}"
                    t = Type.GetType(full_name)
                    if not t:
                        # Try finding in loaded assemblies manually
                        for asm in AppDomain.CurrentDomain.GetAssemblies():
                            if asm.GetName().Name == asm_name:
                                t = asm.GetType(name)
                                if t: break
                    if not t:
                        raise Exception(f"Type {name} not found in {asm_name}")
                    return t

                CSharpSyntaxTree = get_type("Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree", "Microsoft.CodeAnalysis.CSharp")
                CSharpSyntaxWalker = get_type("Microsoft.CodeAnalysis.CSharp.CSharpSyntaxWalker", "Microsoft.CodeAnalysis.CSharp")
                SyntaxKind = get_type("Microsoft.CodeAnalysis.CSharp.SyntaxKind", "Microsoft.CodeAnalysis.CSharp")
                SyntaxNode = get_type("Microsoft.CodeAnalysis.SyntaxNode", "Microsoft.CodeAnalysis")
                
                print("[OK] Roslyn Reflection Fallback Successful")
                
            except Exception as ex:
                print(f"[CRITICAL] Reflection Fallback Failed: {ex}")
                raise e

        _initialized = True
        print("[OK] Roslyn Shared Loader Initialized")

    except Exception as e:
        print(f"[CRITICAL] Roslyn Initialization Failed: {e}")
        import traceback
        traceback.print_exc()
        raise

# Auto-initialize on import
initialize_roslyn()


