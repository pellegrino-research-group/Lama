# Lama - Structural Analysis for Grasshopper

Lama is a Grasshopper plugin for computational design and structural analysis.

## Table of Contents
- [Project Structure](#project-structure)
- [Building the Project](#building-the-project)
- [Development Notes](#development-notes)
- [Publishing](#publishing)

## Project Structure

The project has been organized into a modular structure to separate core functionality from Grasshopper-specific code:

### Lama.Core
Contains the core functionality and data structures that are framework-independent.

```
Lama.Core/
└── Materials/
    ├── MaterialBase.cs          - Base class for all materials
    ├── IsotropicMaterial.cs     - Isotropic material properties
    ├── OrthotropicMaterial.cs   - Orthotropic material properties
    ├── SpringMaterial.cs        - Spring/connector elements
    └── StiffnessMatrixMaterial.cs - Custom stiffness matrix materials
```

**Namespace**: `Lama.Core.Materials`

### Lama.Grasshopper
Contains Grasshopper-specific components, widgets, and plugin infrastructure.

```
Lama.Grasshopper/
├── Components/
│   ├── MaterialComponent.cs     - Material definition component
│   └── RunWithExeComponent.cs   - External executable runner
├── Widgets/
│   ├── GH_SwitcherComponent.cs  - Switchable component base
│   ├── GH_ExtendableComponent.cs - Extendable component base
│   ├── EvaluationUnit.cs        - Component evaluation units
│   └── ... (other widget files)
└── LamaInfo.cs                  - Grasshopper plugin info
```

**Namespace**: `Lama.Grasshopper.Components`, `Lama.Grasshopper.Widgets`

### Scripts
Contains build and publishing automation scripts.

```
scripts/
├── publish.py       - Yak package publishing script
└── manifest.yml     - Generated package manifest
```

## Building the Project

The project uses .NET 8.0 and can be built using:

```bash
dotnet build
```

### Dependencies
- Core functionality depends only on standard .NET libraries
- Grasshopper components depend on:
  - Lama.Core
  - RhinoCommon 8.23.*
  - Grasshopper 8.23.*

## Development Notes

- **Namespace Conflicts**: Due to the `Lama.Grasshopper` namespace, references to the Grasshopper SDK require the `global::` qualifier in some cases (e.g., `global::Grasshopper.Kernel.Special.GH_ValueListMode`)
- **Future Extensibility**: The modular structure allows for adding support for other platforms (e.g., Rhino.Inside, CLI tools) by referencing Lama.Core

## Publishing

### Prerequisites

1. **Yak CLI**: The Rhino package manager CLI tool must be installed
   - Usually installed with Rhino 7+
   - Verify with: `yak --version`
   - More info: https://developer.rhino3d.com/guides/yak/

2. **Python 3**: Required to run the publish script
   - Install PyYAML: `pip install pyyaml`

3. **Rhino Account**: You'll need to authenticate with your Rhino account to publish

### Quick Start

**Build and create package only (no publishing):**

```bash
python scripts/publish.py --version 1.0.0
```

This will:
- Restore NuGet packages
- Build the plugin in Release mode
- Create a `manifest.yml` file
- Package everything into a `.yak` file in the `dist/` directory

**Build and publish to production:**

```bash
python scripts/publish.py --version 1.0.0 --publish
```

**Build and publish to test server:**

```bash
python scripts/publish.py --version 1.0.0 --publish --test
```

### Command-Line Options

- `--version VERSION`: **Required**. Semantic version number (e.g., 1.0.0)
- `--publish`: Publish the package to Yak server after building
- `--test`: Publish to test server instead of production
- `--clean`: Clean build artifacts before building
- `--skip-build`: Skip build step and use existing build artifacts

### Examples

```bash
# First release
python scripts/publish.py --version 1.0.0 --publish

# Bug fix release (use existing build)
python scripts/publish.py --version 1.0.1 --skip-build --publish

# Test publishing process
python scripts/publish.py --version 1.0.0-beta --publish --test

# Clean build
python scripts/publish.py --version 1.0.0 --clean --publish
```

### Manual Publishing

If you prefer to publish manually:

```bash
# 1. Build the package
python scripts/publish.py --version 1.0.0

# 2. Manually publish later
yak push dist/Lama-1.0.0.yak

# Or to test server
yak push dist/Lama-1.0.0.yak --source https://test.yak.rhino3d.com
```

### Customizing the Manifest

The script auto-generates `scripts/manifest.yml`. To customize it, you can edit the file after it's created or modify the `create_manifest()` function in `scripts/publish.py`.

Example manifest structure:
```yaml
name: Lama
version: 1.0.0
authors:
  - Your Name
description: Lama Grasshopper plugin for computational design
url: https://github.com/pellegrino-research-group/Lama
keywords:
  - grasshopper
  - plugin
  - lama
```

### Versioning

Follow [Semantic Versioning](https://semver.org/):
- **MAJOR.MINOR.PATCH** (e.g., 1.0.0)
- MAJOR: Breaking changes
- MINOR: New features (backward compatible)
- PATCH: Bug fixes

### Publishing Checklist

Before publishing:
- [ ] Update version number appropriately
- [ ] Test the plugin locally
- [ ] Update README and documentation
- [ ] Commit and tag the release in Git
- [ ] Test on test server first (use `--test` flag)
- [ ] Publish to production

### Troubleshooting

**Yak not found:**
- Ensure Rhino 7+ is installed
- Add Yak to your PATH (usually in Rhino installation directory)

**Authentication issues:**
- Run `yak login` to authenticate
- You'll need a McNeel account

**Build errors:**
- Ensure .NET SDK is installed
- Check that Rhino/Grasshopper references are correct
- Try `--clean` flag to clean build artifacts

## Resources

- [Yak CLI Guide](https://developer.rhino3d.com/guides/yak/)
- [Creating Yak Packages](https://developer.rhino3d.com/guides/yak/creating-a-package/)
- [Grasshopper Plugin Development](https://developer.rhino3d.com/guides/grasshopper/)
