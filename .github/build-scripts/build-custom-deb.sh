#!/bin/bash
set -e

# ────────────── INPUT VALIDATION ──────────────
if [[ -z "$1" || -z "$2" ]]; then
  echo "❌ Usage: $0 <extracted-deb-dir> <modules-dir>"
  exit 1
fi

EXTRACTED_DIR="$1"
MODULES_DIR="$2"

if [[ ! -d "$EXTRACTED_DIR" ]]; then
  echo "❌ Directory not found: $EXTRACTED_DIR"
  exit 1
fi

if [[ ! -d "$MODULES_DIR" ]]; then
  echo "❌ Directory not found: $MODULES_DIR"
  exit 1
fi

# ────────────── CONFIG ──────────────
BUILD_DIR="./rebuilt-deb"
PACKAGE_NAME="onlyoffice-desktopeditors"
VERSION="8.9.0.999"
ARCH="amd64"
OUTPUT_DEB="${PACKAGE_NAME}_${VERSION}_${ARCH}.deb"
TARGET_DIR="opt/onlyoffice/desktopeditors"
MAIN_DLL="ASC.Data.Reassigns.dll"
LAUNCHER="DesktopEditors"

# ────────────── CLEANUP & COPY ──────────────
echo "🧹 Cleaning up previous build..."
rm -rf "$BUILD_DIR"
cp -r "$EXTRACTED_DIR" "$BUILD_DIR"

# ────────────── REMOVE ORIGINAL BINARY ──────────────
echo "🗑️  Removing original launcher..."
rm -f "$BUILD_DIR/$TARGET_DIR/$LAUNCHER"

# ────────────── ADD DOTNET WRAPPER ──────────────
echo "⚙️  Creating dotnet launcher..."
cat >"$BUILD_DIR/$TARGET_DIR/$LAUNCHER" <<EOF
#!/bin/bash
exec dotnet /$TARGET_DIR/$MAIN_DLL "\$@"
EOF

chmod +x "$BUILD_DIR/$TARGET_DIR/$LAUNCHER"

# ────────────── COPY DLLs & CONFIG FILES ──────────────
echo "📦 Copying DLLs and config files from modules..."
find "$MODULES_DIR" -type f \( -name "*.dll" -o -name "*.config" \) -path "*/bin/Release/*" \
  -exec cp {} "$BUILD_DIR/$TARGET_DIR/" \;

# ────────────── UPDATE CONTROL FILE ──────────────
echo "📝 Updating control file..."
mkdir -p "$BUILD_DIR/DEBIAN"
cat >"$BUILD_DIR/DEBIAN/control" <<EOF
Package: $PACKAGE_NAME
Version: $VERSION
Section: editors
Priority: optional
Architecture: $ARCH
Depends: dotnet-runtime-7.0
Maintainer: You <you@example.com>
Description: Custom ONLYOFFICE Editors (ASC modules)
 Rebuilt to run .NET DLLs from multiple ASC modules, using the ONLYOFFICE wrapper.
EOF

# ────────────── BUILD .DEB ──────────────
echo "🔧 Building .deb package with Docker..."
docker run --rm -v "$PWD":/build -w /build ubuntu:22.04 \
  bash -c "apt update && apt install -y dpkg-dev && dpkg-deb --build rebuilt-deb"

echo "✅ DONE: $OUTPUT_DEB"
