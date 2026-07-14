# FrostyCLI

FrostyCLI is a command-line interface for the Frosty Toolsuite. It allows you to interact with Frostbite engine games headlessly, making it perfect for automation, batch processing, and scripting.

## Features

FrostyCLI exposes the core non-visual/non-audio functionalities of Frosty Editor and Mod Manager:

- **List Assets**: Enumerate EBX, RES, and CHUNK files within the game.
- **Export Assets**: Export any asset (EBX, RES, or CHUNK) directly to a file on your disk.
- **Project Management**: Create new projects (`.fbproject`) and print summary information about them.
- **Asset Importing**: Import modified assets back into a `.fbproject` project file headlessly.
- **Build Mods**: Compile a `.fbproject` into a shareable `.fbmod` file.
- **Apply Mods**: Execute the Frosty Mod Executor to compile a list of `.fbmod` files into a modded game data folder (`ModData`).
- **Launch Game**: Boot the game directly with the compiled mod pack applied.

## Usage

Run the executable via command line and pass the desired command and parameters.

### Basic Syntax
```cmd
FrostyCli.exe <command> <profile> [options]
```

### Supported Commands

- `list-assets <profile> [type]`
  Lists all assets of a specific type (`ebx` (default), `res`, or `chunk`) in the game.
  Example: `FrostyCli.exe list-assets "FIFA 17" ebx`

- `export-asset <profile> <type> <asset_path_or_guid> <output_file>`
  Exports an asset to a file.
  Example: `FrostyCli.exe export-asset "FIFA 17" ebx "UI/Global/GlobalUIAsset" "C:\Dump\GlobalUI"`
  Example: `FrostyCli.exe export-asset "FIFA 17" chunk "a897b7b1-2bd1-4c11-9f2d-88682a2bb91f" "C:\Dump\mychunk"`

- `import-asset <profile> <project_path> <type> <asset_path_or_guid> <input_file> <output_project_path>`
  Imports a modified raw binary file into a `.fbproject` file and saves the output.
  Example: `FrostyCli.exe import-asset "FIFA 17" "myproject.fbproject" ebx "UI/Global/GlobalUIAsset" "modified_globalui.ebx" "updated_project.fbproject"`

- `create-project <profile> <project_path>`
  Creates a new empty `.fbproject` file.
  Example: `FrostyCli.exe create-project "FIFA 17" "C:\Projects\NewProject.fbproject"`

- `project-info <profile> <project_path>`
  Displays details about a project (dirty assets count, title, author, etc.).
  Example: `FrostyCli.exe project-info "FIFA 17" "myproject.fbproject"`

- `build-mod <profile> <project_path> <output_mod_path> [title] [author] [category] [version]`
  Compiles a `.fbproject` into a `.fbmod` file.
  Example: `FrostyCli.exe build-mod "FIFA 17" "myproject.fbproject" "C:\Mods\MyCoolMod.fbmod" "My Awesome Mod" "Gabarsolon" "Gameplay" "1.0"`

- `apply-mods <profile> <mod_pack_name> <mod1.fbmod> [mod2.fbmod ...]`
  Compiles a set of `.fbmod` files into the game's `ModData\<mod_pack_name>` directory.
  Example: `FrostyCli.exe apply-mods "FIFA 17" "Default" "ModA.fbmod" "ModB.fbmod"`

- `launch <profile> <mod_pack_name> [additional_args]`
  Launches the game directly with the compiled mod pack.
  Example: `FrostyCli.exe launch "FIFA 17" "Default" "-windowed"`

## Compilation

1. Clone the official [FrostyToolsuite](https://github.com/CadeEvs/FrostyToolsuite).
2. Place this repository adjacent to it.
3. Open the solution in Visual Studio and ensure you have the `.NET Framework 4.8.1 Developer Pack` installed.
4. Compile using the `x64` platform target.
