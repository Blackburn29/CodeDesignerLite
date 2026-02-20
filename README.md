# CodeDesignerLite

Cross-platform MIPS Assembly Compiler for PlayStation 2

## About

Code Designer Lite is a modern rewrite of Code Designer 2.3 by Gtlcpimp, rebuilt with .NET 10 and Avalonia UI for cross-platform support.

### Core Features

- **MIPS Compiler**: Compiles MIPS assembly into Gameshark (PS2) or PNACH (PCSX2) formats
- **122+ Instructions**: Full support for R-type, I-type, J-type, FPU, and pseudo-instructions
- **Two-Pass Assembly**: Label resolution and import directives with 10-level depth limit
- **Syntax Highlighting**: 19 distinct color styles for instructions, registers, labels, and directives
- **Dark Theme**: Modern dark interface optimized for code editing

### IDE Features

- **Error Panel**: Modern error display with line numbers and descriptions
- **Recent Files**: Quick access to recently opened files
- **Unsaved Changes**: Automatic detection with save prompts
- **Status Bar**: Real-time compilation status and file operations feedback
- **Visual Indicators**: Red border on output when errors present, asterisk in title for unsaved changes
- **Compact UI**: Condensed toolbars with adjustable font sizes (5-36pt)

### Keyboard Shortcuts

| Shortcut | Action |
|----------|--------|
| `Ctrl+N` | New file |
| `Ctrl+O` | Open file |
| `Ctrl+S` | Save file |
| `Ctrl+Shift+S` | Save As |
| `F9` | Compile |
| `Ctrl+Shift+C` | Copy output |

### Output Formats

- **Gameshark (PS2)**: Raw code format for PlayStation 2 cheat devices
- **PNACH (PCSX2)**: Patch format for PCSX2 emulator

### Advanced Features

- **Address Format**: Customize first character in addresses (FORMAT field)
- **Import System**: Include external files with `import "path/to/file.cds"`
- **Label Support**: Case-insensitive labels with FNC prefix highlighting
- **Float Conversion**: Decimal to float conversion with `float` directive
- **Custom Directives**: `address`, `hexcode`, `setreg`, `print`, and more

![cds5](https://github.com/user-attachments/assets/c08b6671-ed45-405a-bbee-8ddfd6a41114)

## Requirements

- .NET 10 SDK
- Supported platforms: Windows, Linux

## Download

Pre-built binaries are automatically generated for each release:
- **Windows**: `CodeDesignerLite-win-x64.exe`
- **Linux**: `CodeDesignerLite-linux-x64`

Download the latest release from the [Releases page](https://github.com/blake32/CodeDesignerLite/releases).

## Installation & File Association

Associate `.cds` files with CodeDesignerLite so they open automatically on double-click.

**Linux** (from the `install/linux/` directory):
```bash
chmod +x install.sh
./install.sh
```

**Windows** (from the `install/windows/` directory, in PowerShell):
```powershell
.\install.ps1
```
Or double-click `install.reg` to register the file association only (requires the exe to be at `C:\Program Files\CodeDesignerLite\`).

## Command Line Usage

Open a file directly from the terminal:
```bash
# Linux
CodeDesignerLite myfile.cds

# Windows
CodeDesignerLite.exe myfile.cds
```

## Building

```bash
dotnet build
```

## Running

```bash
dotnet run --project CodeDesignerLite.Desktop
```

## Publishing

**Windows:**
```bash
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
```

**Linux:**
```bash
dotnet publish -c Release -r linux-x64 --self-contained -p:PublishSingleFile=true
```


[Custom instructions]
```
// set memory location
address $000A0000
// jump or branch to label
jal :label
nop
beq v0, zero, :label:
nop
// print text
print "bla bla bla"
// import files
import "imports/test.cds"
// raw data
hexcode $64
hexcode :label
// set register to value or label
setreg t0, $3F800000
setreg t0, :label
// set label
label:
// float commands convert decimal values to float values
float $-1
float $1.00
float $10.5
// easy branch, b is treated as a standard branch like: beq zero, zero, :label
b :label
nop
// labels starting with "FNC" are bold indicating the start of a function
FNC_Label:
fncLabel:
// multiple formats are supported
addiu $s0, $v0, 0x10
addiu s0, v0, 0x10
addiu s0, v0, $10
// float registers are highlighted unless using $ format
mtc1 f0, t0
mtc1 $f0, t0
```
