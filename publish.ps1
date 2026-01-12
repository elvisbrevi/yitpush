# Script para publicar YitPush en NuGet.org

Write-Host "🚀 Publicando YitPush en NuGet.org" -ForegroundColor Cyan
Write-Host ""

# Verificar que existe API key
$apiKey = $env:NUGET_API_KEY
if ([string]::IsNullOrWhiteSpace($apiKey)) {
    Write-Host "❌ Error: Variable NUGET_API_KEY no está configurada" -ForegroundColor Red
    Write-Host ""
    Write-Host "Para configurarla:" -ForegroundColor Yellow
    Write-Host "  `$env:NUGET_API_KEY='tu-api-key-aqui'" -ForegroundColor White
    Write-Host ""
    Write-Host "Obtén tu API key en: https://www.nuget.org/account/apikeys" -ForegroundColor Yellow
    exit 1
}

# Cambiar al directorio del proyecto
Set-Location YitPush

# Limpiar builds anteriores
Write-Host "🧹 Limpiando builds anteriores..." -ForegroundColor Yellow
dotnet clean

# Empaquetar
Write-Host "📦 Empaquetando proyecto..." -ForegroundColor Yellow
dotnet pack -c Release

if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Error al empaquetar" -ForegroundColor Red
    exit 1
}

# Buscar el archivo .nupkg más reciente
$package = Get-ChildItem -Path "bin/Release/*.nupkg" | Sort-Object LastWriteTime -Descending | Select-Object -First 1

if ($null -eq $package) {
    Write-Host "❌ No se encontró el paquete .nupkg" -ForegroundColor Red
    exit 1
}

Write-Host "📤 Publicando $($package.Name)..." -ForegroundColor Yellow
Write-Host ""

# Publicar a NuGet.org
dotnet nuget push $package.FullName `
    --api-key $apiKey `
    --source https://api.nuget.org/v3/index.json `
    --skip-duplicate

if ($LASTEXITCODE -eq 0) {
    Write-Host ""
    Write-Host "✅ ¡Paquete publicado exitosamente!" -ForegroundColor Green
    Write-Host ""
    Write-Host "El paquete estará disponible en unos minutos en:" -ForegroundColor Yellow
    Write-Host "https://www.nuget.org/packages/YitPush" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Los usuarios podrán instalarlo con:" -ForegroundColor Yellow
    Write-Host "  dotnet tool install --global YitPush" -ForegroundColor White
} else {
    Write-Host ""
    Write-Host "❌ Error al publicar el paquete" -ForegroundColor Red
    exit 1
}
