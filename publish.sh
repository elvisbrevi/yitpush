#!/bin/bash

# Script para publicar YitPush en NuGet.org

echo "🚀 Publicando YitPush en NuGet.org"
echo ""

# Verificar que existe API key
if [ -z "$NUGET_API_KEY" ]; then
    echo "❌ Error: Variable NUGET_API_KEY no está configurada"
    echo ""
    echo "Para configurarla:"
    echo "  export NUGET_API_KEY='tu-api-key-aqui'"
    echo ""
    echo "Obtén tu API key en: https://www.nuget.org/account/apikeys"
    exit 1
fi

# Cambiar al directorio del proyecto
cd YitPush

# Limpiar builds anteriores
echo "🧹 Limpiando builds anteriores..."
dotnet clean

# Empaquetar
echo "📦 Empaquetando proyecto..."
dotnet pack -c Release

if [ $? -ne 0 ]; then
    echo "❌ Error al empaquetar"
    exit 1
fi

# Buscar el archivo .nupkg más reciente
PACKAGE=$(ls -t bin/Release/*.nupkg | head -n 1)

if [ -z "$PACKAGE" ]; then
    echo "❌ No se encontró el paquete .nupkg"
    exit 1
fi

echo "📤 Publicando $PACKAGE..."
echo ""

# Publicar a NuGet.org
dotnet nuget push "$PACKAGE" \
    --api-key "$NUGET_API_KEY" \
    --source https://api.nuget.org/v3/index.json \
    --skip-duplicate

if [ $? -eq 0 ]; then
    echo ""
    echo "✅ ¡Paquete publicado exitosamente!"
    echo ""
    echo "El paquete estará disponible en unos minutos en:"
    echo "https://www.nuget.org/packages/YitPush"
    echo ""
    echo "Los usuarios podrán instalarlo con:"
    echo "  dotnet tool install --global YitPush"
else
    echo ""
    echo "❌ Error al publicar el paquete"
    exit 1
fi
