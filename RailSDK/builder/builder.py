"""
Rail Builder - CLI Tool for Artifact Generation
Main entry point for command-line interface

Usage:
    Rail-builder build <source.py> --output ./artifacts/
    Rail-builder validate <artifact_dir/>
    Rail-builder info <artifact_dir/>
"""

import sys
import argparse
from pathlib import Path
from typing import Optional

# Add parent directory and Rail-bridge to path
parent_dir = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(parent_dir))
sys.path.insert(0, str(parent_dir / "Rail-bridge"))

from artifact_generator import ArtifactGenerator
from core.exceptions import (
    ParserException,
    ArtifactException,
    ManifestValidationError
)


def cmd_build(args) -> int:
    """
    Build command: Generate Rail Artifact from source code
    
    Args:
        args: Parsed command-line arguments
        
    Returns:
        Exit code (0 = success, 1 = error)
    """
    source_path = Path(args.source)
    output_dir = Path(args.output) if args.output else Path("./Rail_artifacts")
    
    # Validation
    if not source_path.exists():
        print(f"❌ Error: Source file not found: {source_path}")
        return 1
    
    if not source_path.suffix in ['.py', '.cs', '.js', '.ts']:
        print(f"[ERROR] Error: Unsupported file type: {source_path.suffix}")
        print("   Supported: .py, .cs, .js, .ts")
        return 1
    
    try:
        print(f"\n[*] Rail BUILDER v1.0.0")
        print(f"{'='*60}")
        print(f"[*] Source: {source_path}")
        print(f"[*] Output: {output_dir}")
        print(f"{'='*60}\n")
        
        # Generate artifact
        generator = ArtifactGenerator()
        artifact_path = generator.build(
            source_path=str(source_path),
            output_dir=str(output_dir),
            artifact_name=args.name
        )
        
        print(f"\n[OK] Artifact created successfully!")
        print(f"[*] Location: {artifact_path}")
        print(f"\n[*] Next steps:")
        print(f"   1. Validate: Rail-builder validate {artifact_path}")
        print(f"   2. Use in Runtime: RailRuntime('{artifact_path}')")
        
        return 0
        
    except ParserException as e:
        print(f"\n[ERROR] Parse Error: {e.message}")
        if e.context:
            print(f"   Context: {e.context}")
        return 1
        
    except ArtifactException as e:
        print(f"\n[ERROR] Artifact Error: {e.message}")
        return 1
        
    except Exception as e:
        print(f"\n[ERROR] Unexpected Error: {str(e)}")
        if args.verbose:
            import traceback
            traceback.print_exc()
        return 1


def cmd_validate(args) -> int:
    """
    Validate command: Check artifact integrity and manifest
    
    Args:
        args: Parsed command-line arguments
        
    Returns:
        Exit code (0 = valid, 1 = invalid)
    """
    artifact_path = Path(args.artifact)
    
    if not artifact_path.exists():
        print(f"❌ Error: Artifact directory not found: {artifact_path}")
        return 1
    
    try:
        print(f"\n🔍 VALIDATING ARTIFACT")
        print(f"{'='*60}")
        
        # Load and validate manifest
        from rail_bridge.core.manifest import ManifestSchema
        
        manifest_file = artifact_path / "manifest.json"
        if not manifest_file.exists():
            print(f"❌ Error: manifest.json not found in {artifact_path}")
            return 1
        
        manifest = ManifestSchema.load_from_file(str(manifest_file))
        
        print(f"✅ Manifest valid")
        print(f"\n📋 Artifact Info:")
        print(f"   Language: {manifest.language}")
        print(f"   Version: {manifest.version}")
        print(f"   Entry Point: {manifest.entry_point}")
        print(f"   Tools: {len(manifest.tools)}")
        print(f"   Dependencies: {len(manifest.dependencies)}")
        
        # Check if entry point exists
        entry_file = artifact_path / manifest.entry_point
        if entry_file.exists():
            print(f"✅ Entry point file exists")
        else:
            print(f"⚠️  Warning: Entry point file not found: {entry_file}")
        
        # Validate integrity
        if manifest.validate_integrity():
            print(f"✅ Integrity check passed")
        
        print(f"\n✅ Artifact is valid!")
        return 0
        
    except ManifestValidationError as e:
        print(f"\n❌ Validation Error: {e.message}")
        return 1
        
    except Exception as e:
        print(f"\n❌ Error: {str(e)}")
        if args.verbose:
            import traceback
            traceback.print_exc()
        return 1


def cmd_info(args) -> int:
    """
    Info command: Display detailed artifact information
    
    Args:
        args: Parsed command-line arguments
        
    Returns:
        Exit code (0 = success, 1 = error)
    """
    artifact_path = Path(args.artifact)
    
    try:
        from rail_bridge.core.manifest import ManifestSchema
        
        manifest_file = artifact_path / "manifest.json"
        manifest = ManifestSchema.load_from_file(str(manifest_file))
        
        print(f"\n📦 ARTIFACT INFORMATION")
        print(f"{'='*60}")
        print(f"\n🏷️  Metadata:")
        print(f"   ID: {manifest.metadata.artifact_id}")
        print(f"   Created: {manifest.metadata.created_at}")
        if manifest.metadata.author:
            print(f"   Author: {manifest.metadata.author}")
        if manifest.metadata.description:
            print(f"   Description: {manifest.metadata.description}")
        
        print(f"\n⚙️  Configuration:")
        print(f"   Language: {manifest.language}")
        print(f"   Version: {manifest.version}")
        print(f"   Entry Point: {manifest.entry_point}")
        
        print(f"\n🔧 Tools ({len(manifest.tools)}):")
        for tool in manifest.tools:
            print(f"   - {tool.name}: {tool.description}")
        
        if manifest.dependencies:
            print(f"\n📦 Dependencies ({len(manifest.dependencies)}):")
            for dep in manifest.dependencies:
                print(f"   - {dep}")
        
        return 0
        
    except Exception as e:
        print(f"❌ Error: {str(e)}")
        return 1


def main():
    """Main CLI entry point"""
    parser = argparse.ArgumentParser(
        prog='Rail-builder',
        description='Rail Builder - Generate portable artifacts from source code',
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""
Examples:
  # Build artifact from Python file
  Rail-builder build my_script.py --output ./artifacts/
  
  # Validate existing artifact
  Rail-builder validate ./artifacts/my_script/
  
  # Show artifact details
  Rail-builder info ./artifacts/my_script/
        """
    )
    
    parser.add_argument('--version', action='version', version='Rail Builder 1.0.0')
    parser.add_argument('-v', '--verbose', action='store_true', help='Enable verbose output')
    
    subparsers = parser.add_subparsers(dest='command', help='Available commands')
    
    # Build command
    build_parser = subparsers.add_parser('build', help='Generate artifact from source code')
    build_parser.add_argument('source', type=str, help='Source file path (e.g., script.py)')
    build_parser.add_argument('-o', '--output', type=str, help='Output directory (default: ./Rail_artifacts)')
    build_parser.add_argument('-n', '--name', type=str, help='Custom artifact name (default: source filename)')
    build_parser.add_argument('--runtime', type=str, help='Runtime language hint') 
    build_parser.set_defaults(func=cmd_build)
    
    # Validate command
    validate_parser = subparsers.add_parser('validate', help='Validate artifact integrity')
    validate_parser.add_argument('artifact', type=str, help='Artifact directory path')
    validate_parser.set_defaults(func=cmd_validate)
    
    # Info command
    info_parser = subparsers.add_parser('info', help='Display artifact information')
    info_parser.add_argument('artifact', type=str, help='Artifact directory path')
    info_parser.set_defaults(func=cmd_info)
    
    # Parse arguments
    args = parser.parse_args()
    
    if not args.command:
        parser.print_help()
        return 1
    
    # Execute command
    return args.func(args)


if __name__ == '__main__':
    sys.exit(main())


