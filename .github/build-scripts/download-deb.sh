#!/bin/bash
set -e

echo "=== Simple OnlyOffice Community Server Download ==="

# Function to verify downloaded file
verify_deb() {
    local file="$1"
    local size=$(stat -c%s "$file" 2>/dev/null || echo "0")
    echo "Downloaded file size: $size bytes ($(($size / 1024 / 1024))MB)"
    
    if [ "$size" -lt 50000000 ]; then
        echo "ERROR: File too small - not the real package"
        echo "First few lines of downloaded file:"
        head -10 "$file"
        return 1
    fi
    
    if ! file "$file" | grep -q "Debian binary package"; then
        echo "ERROR: Not a valid Debian package"
        return 1
    fi
    
    echo "✓ Package verified successfully"
    return 0
}

# Install required tools
install_deps() {
    for tool in curl wget jq; do
        if ! command -v "$tool" >/dev/null; then
            echo "Installing $tool..."
            apt-get update -qq && apt-get install -y "$tool"
        fi
    done
}

# Method 1: Direct download with retries
download_direct() {
    echo "Method 1: Direct download from OnlyOffice"
    
    local urls=(
        "https://download.onlyoffice.com/install/communityserver/linux/onlyoffice-communityserver_latest_amd64.deb"
        "https://download.onlyoffice.com/install/workspace/community/linux/onlyoffice-communityserver_latest_amd64.deb"
    )
    
    for url in "${urls[@]}"; do
        echo "Trying: $url"
        
        # Use wget with better options
        if wget --user-agent="Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36" \
                --timeout=30 \
                --tries=3 \
                --continue \
                --progress=bar \
                -O "onlyoffice-communityserver.deb" \
                "$url" 2>/dev/null; then
            
            if verify_deb "onlyoffice-communityserver.deb"; then
                return 0
            fi
        fi
        
        rm -f "onlyoffice-communityserver.deb"
    done
    
    return 1
}

# Method 2: GitHub releases
download_github() {
    echo "Method 2: GitHub releases"
    
    # Get latest release
    local release_url="https://api.github.com/repos/ONLYOFFICE/CommunityServer/releases/latest"
    local release_info=$(curl -s "$release_url" 2>/dev/null)
    
    if [ -n "$release_info" ]; then
        local download_url=$(echo "$release_info" | jq -r '.assets[] | select(.name | contains(".deb")) | .browser_download_url' | head -1)
        
        if [ -n "$download_url" ] && [ "$download_url" != "null" ]; then
            echo "Found: $download_url"
            
            if wget --user-agent="Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36" \
                    --timeout=30 \
                    --tries=3 \
                    --continue \
                    --progress=bar \
                    -O "onlyoffice-communityserver.deb" \
                    "$download_url"; then
                
                if verify_deb "onlyoffice-communityserver.deb"; then
                    return 0
                fi
            fi
        fi
    fi
    
    rm -f "onlyoffice-communityserver.deb"
    return 1
}

# Method 3: APT without GPG verification
download_apt_trusted() {
    echo "Method 3: APT with trusted repository"
    
    # Add repository without GPG verification
    echo "deb [trusted=yes] https://download.onlyoffice.com/repo/debian squeeze main" > /etc/apt/sources.list.d/onlyoffice-trusted.list
    
    # Update and download
    if apt-get update -qq 2>/dev/null; then
        if apt-get download onlyoffice-communityserver -y 2>/dev/null; then
            local deb_file=$(ls onlyoffice-communityserver*.deb 2>/dev/null | head -1)
            if [ -n "$deb_file" ]; then
                mv "$deb_file" "onlyoffice-communityserver.deb"
                if verify_deb "onlyoffice-communityserver.deb"; then
                    rm -f /etc/apt/sources.list.d/onlyoffice-trusted.list
                    return 0
                fi
            fi
        fi
    fi
    
    # Cleanup
    rm -f /etc/apt/sources.list.d/onlyoffice-trusted.list onlyoffice-communityserver*.deb
    return 1
}

# Method 4: Alternative mirrors/sources
download_alternatives() {
    echo "Method 4: Alternative sources"
    
    local alt_urls=(
        "https://github.com/ONLYOFFICE/DocumentServer/releases/latest/download/onlyoffice-communityserver.deb"
        "https://sourceforge.net/projects/onlyoffice/files/latest/download"
    )
    
    for url in "${alt_urls[@]}"; do
        echo "Trying alternative: $url"
        
        if wget --user-agent="Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36" \
                --timeout=30 \
                --tries=2 \
                --continue \
                --progress=bar \
                -O "onlyoffice-communityserver.deb" \
                "$url" 2>/dev/null; then
            
            if verify_deb "onlyoffice-communityserver.deb"; then
                return 0
            fi
        fi
        
        rm -f "onlyoffice-communityserver.deb"
    done
    
    return 1
}

# Main execution
main() {
    echo "Installing dependencies..."
    install_deps
    
    echo "Starting download attempts..."
    
    if download_direct; then
        echo "✅ Success with direct download!"
        exit 0
    fi
    
    if download_github; then
        echo "✅ Success with GitHub download!"
        exit 0
    fi
    
    if download_apt_trusted; then
        echo "✅ Success with APT download!"
        exit 0
    fi
    
    if download_alternatives; then
        echo "✅ Success with alternative source!"
        exit 0
    fi
    
    echo "❌ All methods failed!"
    echo ""
    echo "🔧 Troubleshooting suggestions:"
    echo "1. Check your internet connection"
    echo "2. Try running with: bash -x $0 (for debug output)"
    echo "3. Check if you're behind a firewall/proxy"
    echo "4. Consider using the official Docker image instead:"
    echo "   docker pull onlyoffice/communityserver"
    echo ""
    exit 1
}

main "$@"
