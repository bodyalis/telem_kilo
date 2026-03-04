#!/bin/bash
# Build script for TelemetryVideoOverlay solution

# Setup dotnet path
export DOTNET_ROOT="$HOME/.dotnet"
export PATH="$DOTNET_ROOT:$PATH"

# Install dotnet if not present
if ! command -v dotnet &> /dev/null; then
    echo "Installing .NET SDK..."
    
    # Create .dotnet directory
    mkdir -p "$DOTNET_ROOT"
    
    # Download and install .NET 8.0 SDK
    curl -sSL https://dot.net/v1/dotnet-install.sh | bash /dev/stdin --channel 8.0 --install-dir "$DOTNET_ROOT"
    
    if [ $? -ne 0 ]; then
        echo "Failed to install .NET SDK"
        exit 1
    fi
    
    echo ".NET SDK installed successfully"
fi

echo "Using .NET: $(dotnet --version)"

# Build solution
echo "Building TelemetryVideoOverlay..."
dotnet build

if [ $? -eq 0 ]; then
    echo "Build succeeded!"
else
    echo "Build failed!"
    exit 1
fi
