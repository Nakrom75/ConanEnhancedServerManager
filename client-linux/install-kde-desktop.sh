#!/usr/bin/env bash
set -e

INSTALL_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
DESKTOP_DIR="$HOME/.local/share/applications"
ICON_DIR="$HOME/.local/share/icons/hicolor/256x256/apps"

mkdir -p "$DESKTOP_DIR"
mkdir -p "$ICON_DIR"

chmod +x "$INSTALL_DIR/ConanServerManager.Linux" "$INSTALL_DIR/run.sh" 2>/dev/null || true

# Copy icon
if [ -f "$INSTALL_DIR/icon.png" ]; then
    cp "$INSTALL_DIR/icon.png" "$ICON_DIR/conan-server-manager.png"
fi

# Create user desktop file
cat > "$DESKTOP_DIR/conan-server-manager.desktop" <<EOF
[Desktop Entry]
Version=1.0
Type=Application
Name=Conan Server Manager
GenericName=Dedicated Server Manager
Comment=Remote management console for Conan Exiles Dedicated Server
Exec=$INSTALL_DIR/ConanServerManager.Linux
Icon=$ICON_DIR/conan-server-manager.png
Terminal=false
Categories=Game;Network;Utility;
StartupWMClass=ConanServerManager.Linux
EOF

chmod +x "$DESKTOP_DIR/conan-server-manager.desktop"

# Refresh KDE menu cache if tool exists
if command -v kbuildsycoca6 >/dev/null 2>&1; then
    kbuildsycoca6 >/dev/null 2>&1 || true
elif command -v kbuildsycoca5 >/dev/null 2>&1; then
    kbuildsycoca5 >/dev/null 2>&1 || true
fi

echo "✅ Conan Server Manager successfully integrated into Fedora KDE Plasma desktop!"
echo "You can now launch 'Conan Server Manager' from your KDE Application Launcher."
