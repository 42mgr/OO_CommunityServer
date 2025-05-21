#!/bin/bash
set -e

# ───────── CONFIG ─────────
KEY_URL="https://download.onlyoffice.com/repo/onlyoffice.key"
REPO_BASE_URL="https://download.onlyoffice.com/repo/debian"
DIST="squeeze"
ARCH="amd64"
PACKAGE="onlyoffice-desktopeditors"

# Temp directories
WORK_DIR=$(mktemp -d)
KEYRING="$WORK_DIR/onlyoffice.gpg"

echo "🔑 Downloading GPG key..."
curl -fsSL "$KEY_URL" -o "$WORK_DIR/onlyoffice.key"

echo "🧰 Converting to GPG format..."
gpg --dearmor <"$WORK_DIR/onlyoffice.key" >"$KEYRING"

echo "📦 Fetching package index..."
INDEX_URL="$REPO_BASE_URL/dists/$DIST/main/binary-$ARCH/Packages.gz"
curl -fsSL "$INDEX_URL" -o "$WORK_DIR/Packages.gz"

echo "📖 Parsing package info..."
gzip -d "$WORK_DIR/Packages.gz"

DEB_URL=$(awk -v pkg="$PACKAGE" '
  $1 == "Package:" && $2 == pkg {found=1}
  found && $1 == "Filename:" { print $2; exit }
' "$WORK_DIR/Packages")

if [[ -z "$DEB_URL" ]]; then
  echo "❌ Package not found in repo index."
  exit 1
fi

FULL_URL="$REPO_BASE_URL/$DEB_URL"
DEB_FILE=$(basename "$DEB_URL")

echo "⬇️ Downloading $PACKAGE..."
curl -fsSL -o "$DEB_FILE" "$FULL_URL"

echo "✅ Done: $DEB_FILE"

# Optional: Cleanup
rm -rf "$WORK_DIR"
