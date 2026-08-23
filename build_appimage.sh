#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
export PATH="$HOME/.dotnet:$PATH"

APP_DIR="$SCRIPT_DIR/build/Potato.AppDir"
OUTPUT_DIR="$SCRIPT_DIR/dist"
APPIMAGETOOL="$HOME/.local/share/ACCELA/appimagetool"

echo "🥔 ====================================================="
echo "🥔  Building Project Potato AppImage for Linux x64      "
echo "🥔 ====================================================="

# 1. Clean previous build artifacts
rm -rf "$APP_DIR"
mkdir -p "$APP_DIR/usr/bin"
mkdir -p "$OUTPUT_DIR"

# 2. Publish self-contained .NET 9 binary
echo "📦 Step 1/3: Publishing self-contained .NET 9 binaries..."
dotnet publish "$SCRIPT_DIR/src/Potato.UI/Potato.UI.csproj" \
    -c Release \
    -r linux-x64 \
    --self-contained true \
    -p:PublishSingleFile=true \
    -p:IncludeNativeLibrariesForSelfExtract=true \
    -o "$APP_DIR/usr/bin"

# 3. Create desktop integration files
echo "📝 Step 2/3: Creating AppDir desktop integration..."

# Desktop file
cat << 'EOF' > "$APP_DIR/potato.desktop"
[Desktop Entry]
Type=Application
Name=Potato
GenericName=Steam Deployment Engine
Comment=Modern Linux & Steam Deck Steam Suite
Exec=Potato.UI %u
Icon=potato
Categories=Game;Utility;
Terminal=false
EOF

# Icon
if [ -f "$SCRIPT_DIR/assets/potato.png" ]; then
    cp "$SCRIPT_DIR/assets/potato.png" "$APP_DIR/potato.png"
    cp "$SCRIPT_DIR/assets/potato.png" "$APP_DIR/.DirIcon"
elif [ -f "$HOME/ASSella_git/assela_logo.png" ]; then
    cp "$HOME/ASSella_git/assela_logo.png" "$APP_DIR/potato.png"
    cp "$HOME/ASSella_git/assela_logo.png" "$APP_DIR/.DirIcon"
else
    # Fallback placeholder if no icon
    touch "$APP_DIR/potato.png"
    touch "$APP_DIR/.DirIcon"
fi

# AppRun launcher
cat << 'EOF' > "$APP_DIR/AppRun"
#!/bin/sh
SELF=$(readlink -f "$0")
HERE=${SELF%/*}
export PATH="${HERE}/usr/bin:${PATH}"
exec "${HERE}/usr/bin/Potato.UI" "$@"
EOF
chmod +x "$APP_DIR/AppRun"

# 4. Generate AppImage using appimagetool
echo "🚀 Step 3/3: Packaging AppImage with appimagetool..."
ARCH=x86_64 "$APPIMAGETOOL" "$APP_DIR" "$OUTPUT_DIR/Potato-x86_64.AppImage"

echo "🎉 Done! AppImage created at: $OUTPUT_DIR/Potato-x86_64.AppImage"
echo "👉 You can distribute this single file to any Linux or Steam Deck user!"
