# Publicación Rápida en NuGet.org

## Resumen Ultra-Rápido

```bash
# 1. Obtener API key en: https://www.nuget.org/account/apikeys
# 2. Configurar variable de entorno
export NUGET_API_KEY='tu-api-key-aqui'

# 3. Ejecutar script de publicación
./publish.sh
```

¡Eso es todo! En 5 minutos cualquiera podrá instalar con:
```bash
dotnet tool install --global YitPush
```

---

## Proceso Detallado

### 1️⃣ Crear Cuenta y Obtener API Key

1. **Crear cuenta**: https://www.nuget.org/
   - Sign in con Microsoft, GitHub o Google

2. **Obtener API key**: https://www.nuget.org/account/apikeys
   - Click en "Create"
   - Key Name: `YitPush`
   - Select Packages: `All Packages`
   - Scopes: ✅ `Push` y ✅ `Push new packages`
   - Click "Create"
   - **¡COPIA LA KEY INMEDIATAMENTE!** (solo se muestra una vez)

### 2️⃣ Configurar API Key

**Linux/macOS:**
```bash
export NUGET_API_KEY='tu-api-key-aqui'

# Para que persista (opcional)
echo 'export NUGET_API_KEY="tu-api-key-aqui"' >> ~/.bashrc
```

**Windows PowerShell:**
```powershell
$env:NUGET_API_KEY='tu-api-key-aqui'

# Para que persista (opcional)
[System.Environment]::SetEnvironmentVariable('NUGET_API_KEY', 'tu-api-key-aqui', 'User')
```

### 3️⃣ Publicar con Script Automático

**Linux/macOS:**
```bash
./publish.sh
```

**Windows:**
```powershell
.\publish.ps1
```

El script hace todo automáticamente:
- ✅ Limpia builds anteriores
- ✅ Empaqueta el proyecto
- ✅ Publica en NuGet.org
- ✅ Maneja errores

### 4️⃣ Verificar Publicación

- **Tu paquete**: https://www.nuget.org/packages/YitPush
- Toma 1-5 minutos en estar disponible
- Recibirás un email de confirmación

---

## Publicación Manual (Sin Script)

Si prefieres hacerlo manualmente:

```bash
cd YitPush

# Limpiar y empaquetar
dotnet clean
dotnet pack -c Release

# Publicar
dotnet nuget push bin/Release/YitPush.1.0.0.nupkg \
  --api-key TU_API_KEY \
  --source https://api.nuget.org/v3/index.json
```

---

## Actualizar Versión (Publicar Nueva Versión)

### 1. Actualizar número de versión

Edita `YitPush/YitPush.csproj`:
```xml
<Version>1.0.1</Version>  <!-- Cambia esto -->
```

### 2. Publicar nueva versión

```bash
./publish.sh
```

¡Eso es todo! Los usuarios actualizan con:
```bash
dotnet tool update --global YitPush
```

---

## Versionado

Usa **Semantic Versioning** (semver.org):

- **1.0.0** → Primera versión pública
- **1.0.1** → Arreglo de bugs (PATCH)
- **1.1.0** → Nueva funcionalidad (MINOR)
- **2.0.0** → Cambios que rompen compatibilidad (MAJOR)

---

## Solución de Problemas Rápida

### ❌ "NUGET_API_KEY no está configurada"
```bash
export NUGET_API_KEY='tu-api-key'
```

### ❌ "Package already exists"
- Ya publicaste esa versión
- Incrementa versión en `YitPush.csproj`

### ❌ "Invalid API key"
- Verifica que copiaste la API key completa
- Crea una nueva en https://www.nuget.org/account/apikeys

### ❌ "Validation failed"
- Revisa email de NuGet para detalles
- Verifica metadata en `YitPush.csproj`

---

## Después de Publicar

### Los usuarios instalan con:
```bash
dotnet tool install --global YitPush
```

### Los usuarios usan con:
```bash
yitpush
```

### Los usuarios actualizan con:
```bash
dotnet tool update --global YitPush
```

### Los usuarios desinstalan con:
```bash
dotnet tool uninstall --global YitPush
```

---

## Checklist de Publicación

- [ ] Cuenta en NuGet.org creada
- [ ] API key generada y guardada
- [ ] Variable `NUGET_API_KEY` configurada
- [ ] Versión actualizada en `.csproj` (si es actualización)
- [ ] Script ejecutado: `./publish.sh`
- [ ] Verificado en https://www.nuget.org/packages/YitPush

---

## Información del Paquete Actual

- **Package ID**: YitPush
- **Version**: 1.0.0
- **Command**: yitpush
- **License**: MIT
- **Author**: Elvis Brevi
- **Repository**: https://github.com/elvisbrevi/yitpush (actualizar si es diferente)

---

## URLs Importantes

- 🌐 **NuGet.org**: https://www.nuget.org/
- 🔑 **API Keys**: https://www.nuget.org/account/apikeys
- 📦 **Tu Paquete**: https://www.nuget.org/packages/YitPush (después de publicar)
- 📚 **Documentación**: https://learn.microsoft.com/nuget/

---

¿Listo para publicar? ¡Solo ejecuta `./publish.sh` y listo! 🚀
