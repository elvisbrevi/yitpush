#!/bin/bash

# YitPush Installation Script

echo "🚀 Installing YitPush..."

# Build and pack the project
echo "📦 Building project..."
dotnet pack -c Release

# Install as global tool
echo "🔧 Installing as global tool..."
dotnet tool install --global --add-source ./bin/Release YitPush --version 1.0.0

# Check if installation was successful
if [ $? -eq 0 ]; then
    echo "✅ YitPush installed successfully!"
    echo ""
    echo "⚠️  Don't forget to set your DeepSeek API key:"
    echo "   export DEEPSEEK_API_KEY='your-api-key-here'"
    echo ""
    echo "Now you can use 'yitpush' from anywhere!"
else
    echo "❌ Installation failed. Please check the error messages above."
    exit 1
fi
