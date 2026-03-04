#!/bin/bash
# Build script for TelemetryVideoOverlay solution

export PATH="$HOME/.dotnet:$PATH"

echo "Building TelemetryVideoOverlay..."
dotnet build

if [ $? -eq 0 ]; then
    echo "Build succeeded!"
else
    echo "Build failed!"
    exit 1
fi
