# YitPush Installation Script for Windows

Write-Host "🚀 Installing YitPush..." -ForegroundColor Cyan

# Build and pack the project
Write-Host "📦 Building project..." -ForegroundColor Yellow
dotnet pack -c Release

if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Build failed. Please check the error messages above." -ForegroundColor Red
    exit 1
}

# Install as global tool
Write-Host "🔧 Installing as global tool..." -ForegroundColor Yellow
dotnet tool install --global --add-source ./bin/Release YitPush --version 1.0.0

if ($LASTEXITCODE -eq 0) {
    Write-Host "✅ YitPush installed successfully!" -ForegroundColor Green
    Write-Host ""
    Write-Host "⚠️  Don't forget to set your DeepSeek API key:" -ForegroundColor Yellow
    Write-Host "   `$env:DEEPSEEK_API_KEY='your-api-key-here'" -ForegroundColor White
    Write-Host ""
    Write-Host "To make it permanent (PowerShell as Administrator):" -ForegroundColor Yellow
    Write-Host "   [System.Environment]::SetEnvironmentVariable('DEEPSEEK_API_KEY', 'your-api-key-here', 'User')" -ForegroundColor White
    Write-Host ""
    Write-Host "Now you can use 'yitpush' from anywhere!" -ForegroundColor Green
} else {
    Write-Host "❌ Installation failed. Please check the error messages above." -ForegroundColor Red
    exit 1
}
