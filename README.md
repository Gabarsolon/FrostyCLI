# FrostyCLI (FrostyCmd)

FrostyCLI is a command-line interface for the Frosty Toolsuite. It allows you to interact with Frostbite engine games (such as FIFA 17) headlessly, making it perfect for automation, batch processing, and modding pipelines.

---

## Features

- **Dual Syntax Support**: Accepts both `<command> <profile>` (FrostyToolsuite standard) and `<profile> <command>` (pipeline format, e.g., `FIFA17 import-legacy ...`).
- **Headless GUI Null Check Fix**: Patched `FrostyMessageBox` so headless automation does not crash with `NullReferenceException` when `Application.Current.MainWindow` is null.
- **Legacy File Management**:
  - `import-legacy`: Import single legacy files (e.g., databases, meta XMLs, textures) into a `.fbproject`.
  - `inject-legacy`: Inject single files or batch-scan directory trees (e.g. `data/ui/imgAssets/`) directly into a project.
  - `export-legacy`: Export legacy assets directly from a `.fbproject` or base game.
- **Team Asset Importer**: `import-team` automatically injects kit, crest, and minikit DDS textures into `.fbproject`.
- **Project Building & Launching**: `launch-project` compiles mod data from `.fbproject` and launches the game directly.
- **Frostbite Native Assets**:
  - `list-assets`: Enumerate EBX, RES, and CHUNK files within the game.
  - `export-asset`: Export any EBX, RES, or CHUNK asset.
  - `import-asset`: Import modified raw binary into a `.fbproject`.
- **Mod Compilation**:
  - `create-project`: Create empty `.fbproject`.
  - `project-info`: Print dirty assets count, mod metadata, and assets summary.
  - `build-mod`: Compile `.fbproject` into a shareable `.fbmod`.
  - `apply-mods`: Run Frosty Mod Executor to compile `.fbmod` list into `ModData`.
  - `launch`: Launch game directly with compiled mod pack.

---

## Usage

### Syntax
Both calling conventions are supported:
```cmd
FrostyCmd.exe <command> <profile> [options]
FrostyCmd.exe <profile> <command> [options]
```

### Automation Commands

#### 1. Import Legacy File
Imports a file into the project's legacy asset system:
```powershell
.\FrostyCmd.exe FIFA17 import-legacy "MyProject.fbproject" "data/loc/eng_us.db" "modified_eng_us.db"
```

#### 2. Inject Legacy Asset(s)
Injects a single asset or scans a directory structure:
```powershell
.\FrostyCmd.exe FIFA17 inject-legacy "MyProject.fbproject" "assets/teams/PoliTimisoara"
```

#### 3. Export Legacy File
Exports legacy files from a project (or base game if no project specified):
```powershell
.\FrostyCmd.exe FIFA17 export-legacy "MyProject.fbproject" "data/loc/eng_us.db" "extracted_eng_us.db"
```

#### 4. Import Full Team
Imports extracted and converted textures (kits, crests, minikits) from a FIFA 16 team into FIFA 17:
```powershell
.\FrostyCmd.exe FIFA17 import-team <fifa16_id> <fifa17_id> <assets_dir> <fbproject>
```

#### 5. Launch Project
Builds the project headlessly and launches the game:
```powershell
.\FrostyCmd.exe FIFA17 launch-project "MyProject.fbproject"
```

#### 6. Project Info
```powershell
.\FrostyCmd.exe FIFA17 project-info "MyProject.fbproject"
```

#### 7. Mod Compilation
```powershell
.\FrostyCmd.exe build-mod "FIFA 17" "MyProject.fbproject" "MyMod.fbmod" "Title" "Author" "Category" "1.0"
.\FrostyCmd.exe apply-mods "FIFA 17" "Default" "MyMod.fbmod"
```

---

## Building

Build using the .NET CLI:

```powershell
dotnet build FrostyCli.csproj -c Developer -p:Platform=x64
```

*Output executable will be generated at `bin/Developer/Debug/FrostyCmd.exe`.*

## Building: `lib/` folder (not tracked)

The `lib/` folder with the Frosty assemblies is intentionally not in git. Copy these from your own
FrostyEditor build (`FrostyEditor\bin\Developer\Debug`) into `lib/` before building:
`FrostyControls.dll`, `FrostyCore.dll`, `FrostyHash.dll`, `FrostyModSupport.dll`, `FrostySdk.dll`,
`Newtonsoft.Json.dll`, `SharpDX*.dll`. If `lib/` is missing, `FrostyCli.csproj` falls back to a local FrostyEditor path.

`FrostyControls.dll` must be built from Frosty sources with the headless fix in `FrostyMessageBox`
(constructor and `ShowInternal` must not dereference `Application.Current.MainWindow`, which is null in CLI mode).
