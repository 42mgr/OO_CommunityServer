#!/bin/bash
set -e

# Custom script to download OnlyOffice Community Server .deb package
# Place this at: .github/build-scripts/download-deb.sh

echo "=== OnlyOffice Community Server .deb Download Script ==="

# Function to verify downloaded file
verify_deb() {
    local file="$1"
    if [ ! -f "$file" ]; then
        echo "ERROR: File $file does not exist"
        return 1
    fi
    
    if [ ! -s "$file" ]; then
        echo "ERROR: File $file is empty"
        return 1
    fi
    
    # Basic .deb format check
    if ! file "$file" | grep -q "Debian binary package"; then
        echo "ERROR: File $file is not a valid Debian package"
        return 1
    fi
    
    echo "✓ Package $file verified successfully"
    return 0
}

# Method 1: Official download site
download_official() {
    echo "Attempting download from official OnlyOffice site..."
    
    # Get the download page and extract actual .deb URL
    download_page="https://www.onlyoffice.com/download-community.aspx"
    
    # Try to find the actual download link
    deb_url=$(curl -s "$download_page" | grep -o 'https://[^"]*onlyoffice-communityserver[^"]*\.deb' | head -1)
    
    if [ -n "$deb_url" ]; then
        echo "Found download URL: $deb_url"
        if wget -O onlyoffice-communityserver.deb "$deb_url"; then
            if verify_deb "onlyoffice-communityserver.deb"; then
                return 0
            fi
        fi
    fi
    
    # Fallback to known working URLs
    urls=(
        "https://download.onlyoffice.com/install/communityserver/linux/onlyoffice-communityserver_latest_amd64.deb"
        "https://download.onlyoffice.com/install/workspace/community/linux/onlyoffice-communityserver_latest_amd64.deb"
    )
    
    for url in "${urls[@]}"; do
        echo "Trying: $url"
        if wget -O onlyoffice-communityserver.deb "$url" 2>/dev/null; then
            if verify_deb "onlyoffice-communityserver.deb"; then
                return 0
            fi
        fi
        rm -f onlyoffice-communityserver.deb
    done
    
    return 1
}

# Method 2: Use installation script to find URL
download_from_script() {
    echo "Attempting to extract URL from installation script..."
    
    # Download the official installation script
    if wget -O workspace-install.sh "https://download.onlyoffice.com/install/workspace-install.sh"; then
        # Make it readable and extract .deb URLs
        chmod +r workspace-install.sh
        
        # Look for .deb download URLs in the script
        deb_urls=$(grep -o 'https://[^"]*onlyoffice-communityserver[^"]*\.deb' workspace-install.sh)
        
        for url in $deb_urls; do
            echo "Found URL in script: $url"
            if wget -O onlyoffice-communityserver.deb "$url" 2>/dev/null; then
                if verify_deb "onlyoffice-communityserver.deb"; then
                    rm -f workspace-install.sh
                    return 0
                fi
            fi
            rm -f onlyoffice-communityserver.deb
        done
        
        rm -f workspace-install.sh
    fi
    
    return 1
}

# Method 3: Third-party builds (btactic, etc.)
download_third_party() {
    echo "Attempting download from third-party sources..."
    
    # Check btactic builds (they provide custom builds)
    urls=(
        "https://github.com/btactic-oo/OOo-deb-pkgs/releases/latest/download/onlyoffice-communityserver_latest_amd64.deb"
        "https://github.com/btactic-oo/build_tools/releases/latest/download/onlyoffice-communityserver_latest_amd64.deb"
    )
    
    for url in "${urls[@]}"; do
        echo "Trying third-party: $url"
        if wget -O onlyoffice-communityserver.deb "$url" 2>/dev/null; then
            if verify_deb "onlyoffice-communityserver.deb"; then
                echo "⚠️  Using third-party build from: $url"
                return 0
            fi
        fi
        rm -f onlyoffice-communityserver.deb
    done
    
    return 1
}

# Method 4: Build minimal structure (last resort)
create_minimal_structure() {
    echo "Creating minimal package structure as last resort..."
    
    mkdir -p minimal-deb/{DEBIAN,opt/onlyoffice/CommunityServer,var/www/onlyoffice,etc/onlyoffice,usr/share/onlyoffice}
    
    # Create basic control file
    cat > minimal-deb/DEBIAN/control << EOF
Package: onlyoffice-communityserver-minimal
Version: 1.0.0-minimal
Section: web
Priority: optional
Architecture: amd64
Maintainer: Build Script <build@example.com>
Description: Minimal OnlyOffice Community Server structure
 This is a minimal package structure for building custom OnlyOffice.
 Contains basic directory structure without full assets.
EOF
    
    # Create basic postinst
    cat > minimal-deb/DEBIAN/postinst << 'EOF'
#!/bin/bash
set -e

# Create onlyoffice user
if ! id "onlyoffice" >/dev/null 2>&1; then
    useradd -r -s /bin/false -d /var/www/onlyoffice onlyoffice
fi

# Create directories and set permissions
mkdir -p /var/log/onlyoffice /var/www/onlyoffice/Data
chown -R onlyoffice:onlyoffice /opt/onlyoffice/ 2>/dev/null || true
chown -R onlyoffice:onlyoffice /var/www/onlyoffice/ 2>/dev/null || true
chown -R onlyoffice:onlyoffice /var/log/onlyoffice/ 2>/dev/null || true

exit 0
EOF
    
    chmod +x minimal-deb/DEBIAN/postinst
    
    # Build the package
    dpkg-deb --build minimal-deb onlyoffice-communityserver.deb
    
    if verify_deb "onlyoffice-communityserver.deb"; then
        echo "⚠️  Created minimal structure - your custom code will provide functionality"
        rm -rf minimal-deb
        return 0
    fi
    
    return 1
}

# Main execution
main() {
    # Try each method in order
    if download_official; then
        echo "✅ Successfully downloaded from official source"
        return 0
    fi
    
    if download_from_script; then
        echo "✅ Successfully downloaded using installation script"
        return 0
    fi
    
    if download_third_party; then
        echo "✅ Successfully downloaded from third-party source"
        return 0
    fi
    
    if create_minimal_structure; then
        echo "✅ Created minimal structure as fallback"
        return 0
    fi
    
    echo "❌ All download methods failed"
    return 1
}

# Run main function
main "$@"
