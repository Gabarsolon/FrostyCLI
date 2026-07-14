# FrostyCLI

FrostyCLI is a command-line interface for the Frosty Toolsuite. It allows you to interact with Frostbite engine games headlessly, making it perfect for automation, batch processing, and scripting.

## Features

FrostyCLI aims to expose the core functionalities of Frosty Editor through the command line:

- **Initialize Profiles**: Load game profiles (e.g., FIFA, Star Wars Battlefront II, Mass Effect Andromeda).
- **List Assets**: Enumerate EBX, RES, and CHUNK files within the game.
- **Export Assets**: Dump assets to disk for external analysis or modification.
- **Build Mods**: (Work in Progress) Compile and apply modifications headlessly.

## Usage

Run the executable via command line and pass the desired action and parameters.

### Basic Syntax
```cmd
FrostyCli.exe <command> [options]
```

### Supported Commands

- `list <profile>`
  Lists all EBX assets available in the specified game profile.
  Example: `FrostyCli.exe list "FIFA 17"`

- `export <profile> <asset_path> <output_dir>`
  Exports a specific EBX asset to the specified output directory.
  Example: `FrostyCli.exe export "FIFA 17" "UI/Global/GlobalUIAsset" "C:\Dump"`

*(More commands are actively being added to match Frosty Editor's capabilities!)*

## Compilation

1. Clone the official [FrostyToolsuite](https://github.com/CadeEvs/FrostyToolsuite).
2. Place this repository adjacent to it.
3. Open the solution in Visual Studio and ensure you have the `.NET Framework 4.8.1 Developer Pack` installed.
4. Compile using the `x64` platform target.
