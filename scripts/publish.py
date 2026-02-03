#!/usr/bin/env python3
"""
Publish script for Lama Grasshopper plugin to Yak package manager.

Usage:
    python publish.py --version 1.0.0                    # Build and create package
    python publish.py --version 1.0.0 --publish          # Build, package, and publish
    python publish.py --version 1.0.0 --publish --test   # Publish to test server
"""

import argparse
import os
import subprocess
import sys
import shutil
from pathlib import Path
import yaml


class Colors:
    """ANSI color codes for terminal output."""
    HEADER = '\033[95m'
    OKBLUE = '\033[94m'
    OKCYAN = '\033[96m'
    OKGREEN = '\033[92m'
    WARNING = '\033[93m'
    FAIL = '\033[91m'
    ENDC = '\033[0m'
    BOLD = '\033[1m'


def print_step(message):
    """Print a step message."""
    print(f"\n{Colors.OKBLUE}▶ {message}{Colors.ENDC}")


def print_success(message):
    """Print a success message."""
    print(f"{Colors.OKGREEN}✓ {message}{Colors.ENDC}")


def print_error(message):
    """Print an error message."""
    print(f"{Colors.FAIL}✗ {message}{Colors.ENDC}")


def print_warning(message):
    """Print a warning message."""
    print(f"{Colors.WARNING}⚠ {message}{Colors.ENDC}")


def run_command(cmd, cwd=None):
    """Run a shell command and return the result."""
    try:
        result = subprocess.run(
            cmd,
            shell=True,
            cwd=cwd,
            check=True,
            capture_output=True,
            text=True
        )
        return True, result.stdout
    except subprocess.CalledProcessError as e:
        return False, e.stderr


def check_yak_installed():
    """Check if Yak CLI is installed."""
    success, output = run_command("yak --version")
    if not success:
        print_error("Yak CLI is not installed or not in PATH")
        print("\nTo install Yak, run:")
        print("  Rhino command line: _PackageManager")
        print("  Or download from: https://www.rhino3d.com/downloads/")
        return False
    print_success(f"Yak is installed: {output.strip()}")
    return True


def clean_build():
    """Clean build artifacts."""
    print_step("Cleaning build artifacts...")
    
    bin_path = Path("bin")
    obj_path = Path("obj")
    
    if bin_path.exists():
        shutil.rmtree(bin_path)
        print_success("Removed bin directory")
    
    if obj_path.exists():
        shutil.rmtree(obj_path)
        print_success("Removed obj directory")


def restore_packages():
    """Restore NuGet packages."""
    print_step("Restoring NuGet packages...")
    success, output = run_command("dotnet restore Lama.csproj")
    if not success:
        print_error(f"Failed to restore packages:\n{output}")
        return False
    print_success("Packages restored")
    return True


def build_plugin():
    """Build the Grasshopper plugin."""
    import platform
    
    # Determine target framework based on OS
    if platform.system() == "Windows":
        target_framework = "net48"
        print_step(f"Building plugin (Release, {target_framework})...")
        success, output = run_command(f"dotnet build Lama.csproj -c Release -f {target_framework}")
    else:
        # macOS/Linux: build for net8.0-windows (will run on Windows Rhino)
        target_framework = "net8.0-windows"
        print_step(f"Building plugin (Release, {target_framework})...")
        # Need to specify windows runtime identifier for cross-platform build
        success, output = run_command(f"dotnet build Lama.csproj -c Release -f {target_framework}")
    
    if not success:
        print_error(f"Build failed:\n{output}")
        return False
    
    # Check if .gha file was created
    gha_path = Path(f"bin/Release/{target_framework}/Lama.gha")
    if not gha_path.exists():
        print_error(f"Build succeeded but .gha file not found at {gha_path}")
        return False
    
    print_success(f"Build completed: {gha_path}")
    return True


def create_manifest(version):
    """Create or update manifest.yml file."""
    print_step(f"Creating manifest.yml for version {version}...")
    
    manifest = {
        'name': 'Lama',
        'version': version,
        'authors': ['Lama'],
        'description': 'Lama Grasshopper plugin for computational design',
        'url': 'https://github.com/pellegrino-research-group/Lama',
        'keywords': ['grasshopper', 'plugin', 'lama'],
        'icon': 'icon.png'  # Optional: add if you have an icon
    }
    
    # Create manifest in scripts folder (same location as this script)
    script_dir = Path(__file__).parent
    manifest_path = script_dir / "manifest.yml"
    with open(manifest_path, 'w') as f:
        yaml.dump(manifest, f, default_flow_style=False, sort_keys=False)
    
    print_success(f"Created {manifest_path}")
    return manifest_path


def create_yak_package(version):
    """Create Yak package."""
    import platform
    print_step("Creating Yak package...")
    
    # Create dist directory if it doesn't exist
    dist_dir = Path("dist")
    dist_dir.mkdir(exist_ok=True)
    
    # Prepare package directory
    package_dir = dist_dir / "package"
    if package_dir.exists():
        shutil.rmtree(package_dir)
    package_dir.mkdir()
    
    # Determine source path based on OS
    if platform.system() == "Windows":
        target_framework = "net48"
    else:
        target_framework = "net8.0-windows"
    
    # Copy .gha file
    gha_source = Path(f"bin/Release/{target_framework}/Lama.gha")
    gha_dest = package_dir / "Lama.gha"
    shutil.copy2(gha_source, gha_dest)
    print_success(f"Copied {gha_source} to package")
    
    # Copy manifest (from scripts folder)
    script_dir = Path(__file__).parent
    manifest_source = script_dir / "manifest.yml"
    manifest_dest = package_dir / "manifest.yml"
    shutil.copy2(manifest_source, manifest_dest)
    print_success("Copied manifest.yml to package")
    
    # Build the Yak package
    success, output = run_command("yak build", cwd=package_dir)
    if not success:
        print_error(f"Failed to create Yak package:\n{output}")
        return False
    
    # Move the .yak file to dist directory
    yak_files = list(package_dir.glob("*.yak"))
    if not yak_files:
        print_error("No .yak file was created")
        return False
    
    yak_file = yak_files[0]
    final_yak = dist_dir / yak_file.name
    if final_yak.exists():
        final_yak.unlink()
    shutil.move(str(yak_file), str(final_yak))
    
    print_success(f"Created package: {final_yak}")
    return True, final_yak


def publish_package(yak_file, test_server=False):
    """Publish package to Yak server."""
    print_step("Publishing package to Yak server...")
    
    if not yak_file.exists():
        print_error(f"Package file not found: {yak_file}")
        return False
    
    # Build publish command
    cmd = f"yak push {yak_file}"
    if test_server:
        cmd += " --source https://test.yak.rhino3d.com"
        print_warning("Publishing to TEST server")
    
    print(f"\nCommand: {cmd}")
    print("\nNote: You may need to authenticate with Yak.")
    print("Press Enter to continue or Ctrl+C to cancel...")
    input()
    
    success, output = run_command(cmd)
    if not success:
        print_error(f"Failed to publish package:\n{output}")
        return False
    
    print_success("Package published successfully!")
    print(output)
    return True


def main():
    parser = argparse.ArgumentParser(
        description="Publish Lama Grasshopper plugin to Yak package manager"
    )
    parser.add_argument(
        "--version",
        required=True,
        help="Version number (e.g., 1.0.0)"
    )
    parser.add_argument(
        "--publish",
        action="store_true",
        help="Publish to Yak server (default: only build package)"
    )
    parser.add_argument(
        "--test",
        action="store_true",
        help="Publish to test server instead of production"
    )
    parser.add_argument(
        "--clean",
        action="store_true",
        help="Clean build artifacts before building"
    )
    parser.add_argument(
        "--skip-build",
        action="store_true",
        help="Skip build step (use existing build)"
    )
    
    args = parser.parse_args()
    
    print(f"\n{Colors.HEADER}{Colors.BOLD}Lama Grasshopper Plugin Publisher{Colors.ENDC}")
    print(f"{Colors.HEADER}{'=' * 50}{Colors.ENDC}\n")
    
    # Change to project root directory (parent of scripts folder)
    script_dir = Path(__file__).parent.parent
    os.chdir(script_dir)
    
    # Check if Yak is installed
    if not check_yak_installed():
        sys.exit(1)
    
    # Clean if requested
    if args.clean:
        clean_build()
    
    # Build steps
    if not args.skip_build:
        if not restore_packages():
            sys.exit(1)
        
        if not build_plugin():
            sys.exit(1)
    else:
        import platform
        print_warning("Skipping build step")
        # Verify .gha exists
        target_framework = "net48" if platform.system() == "Windows" else "net8.0-windows"
        gha_path = Path(f"bin/Release/{target_framework}/Lama.gha")
        if not gha_path.exists():
            print_error(f"Build artifact not found: {gha_path}")
            sys.exit(1)
    
    # Create manifest
    manifest_path = create_manifest(args.version)
    if not manifest_path:
        sys.exit(1)
    
    # Create package
    success, yak_file = create_yak_package(args.version)
    if not success:
        sys.exit(1)
    
    # Publish if requested
    if args.publish:
        if not publish_package(yak_file, args.test):
            sys.exit(1)
    else:
        print_warning("\nPackage created but not published.")
        print(f"To publish later, run:")
        print(f"  yak push {yak_file}")
    
    print(f"\n{Colors.OKGREEN}{Colors.BOLD}✓ All done!{Colors.ENDC}\n")


if __name__ == "__main__":
    try:
        main()
    except KeyboardInterrupt:
        print(f"\n\n{Colors.WARNING}Cancelled by user{Colors.ENDC}")
        sys.exit(1)
    except Exception as e:
        print_error(f"Unexpected error: {e}")
        import traceback
        traceback.print_exc()
        sys.exit(1)
