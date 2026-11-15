#!/usr/bin/env bash
set -e

REPO="mequann/omni-dev"
INSTALL_DIR="${OMNI_INSTALL_DIR:-$HOME/.omni/bin}"

# Detect OS and architecture
OS="$(uname -s | tr '[:upper:]' '[:lower:]')"
ARCH="$(uname -m)"

case "$OS" in
  linux)
    TARGET_OS="linux"
    ;;
  darwin)
    TARGET_OS="macos"
    ;;
  *)
    echo "Unsupported OS: $OS"
    exit 1
    ;;
esac

case "$ARCH" in
  x86_64)
    TARGET_ARCH="x64"
    ;;
  arm64|aarch64)
    TARGET_ARCH="arm64"
    ;;
  *)
    echo "Unsupported architecture: $ARCH"
    exit 1
    ;;
esac

ARCHIVE_NAME="omni-${TARGET_OS}-${TARGET_ARCH}.tar.gz"
DOWNLOAD_URL="https://github.com/${REPO}/releases/latest/download/${ARCHIVE_NAME}"

echo "⚡ Installing Omni (${TARGET_OS}-${TARGET_ARCH})..."
mkdir -p "$INSTALL_DIR"

TMP_DIR="$(mktemp -d)"
trap 'rm -rf "$TMP_DIR"' EXIT

curl -sSL "$DOWNLOAD_URL" -o "$TMP_DIR/$ARCHIVE_NAME"
tar -xzf "$TMP_DIR/$ARCHIVE_NAME" -C "$TMP_DIR"
mv "$TMP_DIR/omni" "$INSTALL_DIR/omni"
chmod +x "$INSTALL_DIR/omni"

echo "✔ Omni installed successfully to $INSTALL_DIR/omni"
echo ""
echo "Ensure $INSTALL_DIR is in your PATH:"
echo "  export PATH=\"\$PATH:$INSTALL_DIR\""
