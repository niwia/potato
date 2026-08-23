#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
export PATH="$HOME/.dotnet:$PATH"

echo "🥔 Building Potato Pipeline Console Harness (Self-Contained Linux Executable)..."

OUTPUT_DIR="$SCRIPT_DIR/publish"
mkdir -p "$OUTPUT_DIR"

dotnet publish "$SCRIPT_DIR/src/Potato.Downloader.ConsoleHarness/Potato.Downloader.ConsoleHarness.csproj" \
    -c Release \
    -r linux-x64 \
    --self-contained true \
    -p:PublishSingleFile=true \
    -p:IncludeNativeLibrariesForSelfExtract=true \
    -o "$OUTPUT_DIR"

echo "✅ Build complete! Binary available at: $OUTPUT_DIR/Potato.Downloader.ConsoleHarness"
