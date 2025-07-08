﻿#!/bin/bash

# FluentAzure Code Formatting & Style Check Script for Linux/Mac
# Usage: ./scripts/format.sh [--check] [--severity info|warn|error]

set -e  # Exit on any error

# Default values
CHECK_MODE=false
SEVERITY="warn"

# Parse command line arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        --check)
            CHECK_MODE=true
            shift
            ;;
        --severity)
            SEVERITY="$2"
            shift 2
            ;;
        *)
            echo "Unknown option: $1"
            echo "Usage: $0 [--check] [--severity info|warn|error]"
            exit 1
            ;;
    esac
done

# Get script directory and project root
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"
cd "$PROJECT_ROOT"

echo "ðŸ” FluentAzure Code Formatting & Style Check"
echo "Project Root: $PROJECT_ROOT"

# Step 1: Format code
echo ""
echo "ðŸ“ Step 1: Formatting code..."

if [ "$CHECK_MODE" = true ]; then
    echo "Running in check mode (no changes will be made)..."
    dotnet format --verify-no-changes --severity "$SEVERITY" --verbosity diagnostic
else
    echo "Formatting code..."
    dotnet format --severity "$SEVERITY" --verbosity diagnostic
fi

if [ $? -ne 0 ]; then
    echo "âŒ Code formatting failed!"
    exit 1
fi
echo "âœ… Code formatting completed successfully!"

# Step 2: Build the project
echo ""
echo "ðŸ”¨ Step 2: Building project..."

dotnet build --configuration Release --no-restore
if [ $? -ne 0 ]; then
    echo "âŒ Build failed!"
    exit 1
fi
echo "âœ… Build completed successfully!"

# Step 3: Run tests
echo ""
echo "ðŸ§ª Step 3: Running tests..."

dotnet test --configuration Release --no-build --logger "console;verbosity=normal"
if [ $? -ne 0 ]; then
    echo "âŒ Tests failed!"
    exit 1
fi
echo "âœ… All tests passed!"

# Step 4: Code analysis summary
echo ""
echo "ðŸ“Š Step 4: Code analysis summary..."

BUILD_OUTPUT=$(dotnet build --configuration Release --verbosity minimal 2>&1)
WARNINGS=$(echo "$BUILD_OUTPUT" | grep -i "warning" || true)
ERRORS=$(echo "$BUILD_OUTPUT" | grep -i "error" || true)

if [ -n "$ERRORS" ]; then
    echo "âŒ Found errors:"
    echo "$ERRORS"
    exit 1
fi

if [ -n "$WARNINGS" ]; then
    echo "âš ï¸  Found warnings:"
    echo "$WARNINGS"
else
    echo "âœ… No warnings found!"
fi

echo ""
echo "ðŸŽ‰ Code formatting and style check completed successfully!"
echo "Project is ready for commit! ðŸš€"
