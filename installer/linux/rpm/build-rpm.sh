#!/bin/bash
# Build RPM package for LCDPossible
set -e

VERSION="${1:-0.1.0}"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/../../.." && pwd)"
BUILD_DIR="$PROJECT_ROOT/.build/rpm-build"
OUTPUT_DIR="$SCRIPT_DIR"

echo "=== Building RPM Package ==="
echo "Version: $VERSION"

# Check for rpmbuild
if ! command -v rpmbuild &> /dev/null; then
    echo "rpmbuild not found. Install with: sudo dnf install rpm-build (Fedora) or sudo yum install rpm-build (RHEL/CentOS)"
    exit 1
fi

# Clean build directory
rm -rf "$BUILD_DIR"
mkdir -p "$BUILD_DIR"/{BUILD,RPMS,SOURCES,SPECS,SRPMS}

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

# Create source tarball
SOURCE_DIR="$BUILD_DIR/SOURCES/lcdpossible-$VERSION"
mkdir -p "$SOURCE_DIR"
cp "$PUBLISH_DIR/LCDPossible" "$SOURCE_DIR/"
cp "$SCRIPT_DIR/../99-lcdpossible.rules" "$SOURCE_DIR/"
cp "$SCRIPT_DIR/../lcdpossible.service" "$SOURCE_DIR/"

cd "$BUILD_DIR/SOURCES"
tar -czvf "lcdpossible-$VERSION.tar.gz" "lcdpossible-$VERSION"

# Copy spec file
cp "$SCRIPT_DIR/lcdpossible.spec" "$BUILD_DIR/SPECS/"

# Build RPM
rpmbuild -bb \
    --define "_topdir $BUILD_DIR" \
    --define "version $VERSION" \
    "$BUILD_DIR/SPECS/lcdpossible.spec"

# Copy result to output directory
cp "$BUILD_DIR/RPMS/x86_64"/*.rpm "$OUTPUT_DIR/"

echo ""
echo "=== Package Created ==="
ls -la "$OUTPUT_DIR"/*.rpm
