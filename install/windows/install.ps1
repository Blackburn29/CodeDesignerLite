# CodeDesignerLite Windows File Association Installer
# Run as administrator for system-wide install, or as user for per-user install

param(
    [string]$InstallPath = "$env:LOCALAPPDATA\CodeDesignerLite",
    [switch]$SystemWide
)

$ExePath = Join-Path $InstallPath "CodeDesignerLite.exe"
$Binary  = "CodeDesignerLite-win-x64.exe"

if (-not (Test-Path $Binary)) {
    Write-Error "Error: $Binary not found in current directory."
    exit 1
}

Write-Host "Installing CodeDesignerLite to $InstallPath..."

# Copy binary
New-Item -ItemType Directory -Force -Path $InstallPath | Out-Null
Copy-Item $Binary $ExePath -Force

# Set registry root based on scope
$RegRoot = if ($SystemWide) { "HKLM:\Software\Classes" } else { "HKCU:\Software\Classes" }

# Register file extension
New-Item -Force -Path "$RegRoot\.cds" | Set-ItemProperty -Name "(Default)" -Value "CodeDesignerLite.Script"

# Register file type
New-Item -Force -Path "$RegRoot\CodeDesignerLite.Script" | Set-ItemProperty -Name "(Default)" -Value "Code Designer Script"
New-Item -Force -Path "$RegRoot\CodeDesignerLite.Script\DefaultIcon" | Set-ItemProperty -Name "(Default)" -Value "`"$ExePath`",0"
New-Item -Force -Path "$RegRoot\CodeDesignerLite.Script\shell\open\command" | Set-ItemProperty -Name "(Default)" -Value "`"$ExePath`" `"%1`""

# Add to PATH
$CurrentPath = [Environment]::GetEnvironmentVariable("PATH", "User")
if ($CurrentPath -notlike "*$InstallPath*") {
    [Environment]::SetEnvironmentVariable("PATH", "$CurrentPath;$InstallPath", "User")
}

Write-Host "Done! .cds files are now associated with CodeDesignerLite."
Write-Host "You can open files with: CodeDesignerLite myfile.cds"
