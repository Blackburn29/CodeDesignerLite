#!/bin/bash
set -e

BINARY="CodeDesignerLite-linux-x64"
INSTALL_DIR="/usr/local/bin"
DESKTOP_DIR="$HOME/.local/share/applications"
MIME_DIR="$HOME/.local/share/mime/packages"

if [ ! -f "$BINARY" ]; then
    echo "Error: $BINARY not found in current directory."
    exit 1
fi

echo "Installing CodeDesignerLite..."

# Install binary
sudo install -m 755 "$BINARY" "$INSTALL_DIR/CodeDesignerLite"

# Install .desktop entry
mkdir -p "$DESKTOP_DIR"
cp codedesignerlite.desktop "$DESKTOP_DIR/codedesignerlite.desktop"
update-desktop-database "$DESKTOP_DIR" 2>/dev/null || true

# Register MIME type
mkdir -p "$MIME_DIR"
cp codedesignerlite-mime.xml "$MIME_DIR/codedesignerlite.xml"
update-mime-database "$HOME/.local/share/mime" 2>/dev/null || true

# Associate .cds files
xdg-mime default codedesignerlite.desktop application/x-cds 2>/dev/null || true

echo "Done! .cds files are now associated with CodeDesignerLite."
echo "You can open files with: CodeDesignerLite myfile.cds"
