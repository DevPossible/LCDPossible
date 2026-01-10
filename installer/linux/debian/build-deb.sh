#!/bin/bash
# Build Debian package for LCDPossible
set -e

VERSION="${1:-0.1.0}"
ARCH="${2:-amd64}"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/../../.." && pwd)"
BUILD_DIR="$PROJECT_ROOT/.build/deb-package"
OUTPUT_DIR="$SCRIPT_DIR"

echo "=== Building Debian Package ==="
echo "Version: $VERSION"
echo "Architecture: $ARCH"

# Clean build directory
rm -rf "$BUILD_DIR"
mkdir -p "$BUILD_DIR/opt/lcdpossible"
mkdir -p "$BUILD_DIR/etc/systemd/system"
mkdir -p "$BUILD_DIR/DEBIAN"

# Check for published binary
PUBLISH_DIR="$PROJECT_ROOT/.build/publish/LCDPossible/linux-x64"
if [ ! -f "$PUBLISH_DIR/LCDPossible" ]; then
    # Try artifacts directory (CI)
    PUBLISH_DIR="$PROJECT_ROOT/artifacts/lcdpossible-linux-x64"
    if [ ! -f "$PUBLISH_DIR/LCDPossible" ]; then
        echo "Building application..."
        cd "$PROJECT_ROOT"
        dotnet publish src/LCDPossible/LCDPossible.csproj \
            --configuration Release \
            --runtime linux-x64 \
            --self-contained true \
            -p:PublishSingleFile=true \
            -p:Version=$VERSION \
            --output "$PROJECT_ROOT/.build/publish/LCDPossible/linux-x64"
        PUBLISH_DIR="$PROJECT_ROOT/.build/publish/LCDPossible/linux-x64"
    fi
fi

# Copy application files
cp "$PUBLISH_DIR/LCDPossible" "$BUILD_DIR/opt/lcdpossible/"
chmod +x "$BUILD_DIR/opt/lcdpossible/LCDPossible"

# Copy supporting files
cp "$SCRIPT_DIR/../99-lcdpossible.rules" "$BUILD_DIR/opt/lcdpossible/"
cp "$SCRIPT_DIR/../lcdpossible.service" "$BUILD_DIR/etc/systemd/system/"

# Copy and update DEBIAN control files
cp "$SCRIPT_DIR/DEBIAN/control" "$BUILD_DIR/DEBIAN/"
sed -i "s/VERSION_PLACEHOLDER/$VERSION/g" "$BUILD_DIR/DEBIAN/control"

cp "$SCRIPT_DIR/DEBIAN/postinst" "$BUILD_DIR/DEBIAN/"
cp "$SCRIPT_DIR/DEBIAN/prerm" "$BUILD_DIR/DEBIAN/"
cp "$SCRIPT_DIR/DEBIAN/postrm" "$BUILD_DIR/DEBIAN/"
chmod 755 "$BUILD_DIR/DEBIAN/postinst"
chmod 755 "$BUILD_DIR/DEBIAN/prerm"
chmod 755 "$BUILD_DIR/DEBIAN/postrm"

# Calculate installed size
INSTALLED_SIZE=$(du -sk "$BUILD_DIR" | cut -f1)
echo "Installed-Size: $INSTALLED_SIZE" >> "$BUILD_DIR/DEBIAN/control"

# Build the package
PACKAGE_NAME="lcdpossible_${VERSION}_${ARCH}.deb"
dpkg-deb --build --root-owner-group "$BUILD_DIR" "$OUTPUT_DIR/$PACKAGE_NAME"

echo ""
echo "=== Package Created ==="
echo "Output: $OUTPUT_DIR/$PACKAGE_NAME"

# Verify package
echo ""
echo "Package info:"
dpkg-deb --info "$OUTPUT_DIR/$PACKAGE_NAME"
