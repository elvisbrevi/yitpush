# Cómo Publicar YitPush en NuGet.org

## Paso 1: Crear Cuenta en NuGet.org

1. Ve a https://www.nuget.org/
2. Haz clic en "Sign in" (arriba derecha)
3. Crea una cuenta con Microsoft, GitHub o Google

## Paso 2: Generar API Key

1. Una vez logueado, ve a tu perfil (clic en tu nombre arriba derecha)
2. Selecciona "API Keys" del menú
3. Haz clic en "Create" para crear una nueva API key
4. Configura:
   - **Key Name**: YitPush (o cualquier nombre)
   - **Package owner**: Tu usuario
   - **Expires in**: 365 days (o lo que prefieras)
   - **Select Packages**: Selecciona "All Packages" (o puedes restringirlo después)
   - **Scopes**: Marca "Push" y "Push new packages and package versions"
5. Haz clic en "Create"
6. **IMPORTANTE**: Copia la API key inmediatamente (solo se muestra una vez)

## Paso 3: Publicar el Paquete

Abre tu terminal en la carpeta del proyecto y ejecuta:

```bash
cd YitPush

# 1. Empaquetar (si aún no lo hiciste)
dotnet pack -c Release

# 2. Publicar a NuGet.org
dotnet nuget push bin/Release/YitPush.1.0.0.nupkg \
  --api-key TU_API_KEY_AQUI \
  --source https://api.nuget.org/v3/index.json
```

**Reemplaza `TU_API_KEY_AQUI` con la API key que copiaste**

### Ejemplo real:
```bash
dotnet nuget push bin/Release/YitPush.1.0.0.nupkg \
  --api-key oy2abc...xyz123 \
  --source https://api.nuget.org/v3/index.json
```

## Paso 4: Esperar Validación

- NuGet.org validará el paquete (toma 1-5 minutos)
- Recibirás un email cuando esté disponible
- Puedes verificar en: https://www.nuget.org/packages/YitPush

## Paso 5: ¡Listo! Cualquiera Puede Instalarlo

Una vez publicado, cualquier persona en el mundo puede instalar YitPush con:

```bash
dotnet tool install --global YitPush
```

Y usarlo con:
```bash
yitpush
```

## Actualizar el Paquete (Futuras Versiones)

1. **Actualizar versión** en `YitPush.csproj`:
   ```xml
   <Version>1.0.1</Version>
   ```

2. **Empaquetar y publicar**:
   ```bash
   dotnet pack -c Release
   dotnet nuget push bin/Release/YitPush.1.0.1.nupkg \
     --api-key TU_API_KEY \
     --source https://api.nuget.org/v3/index.json
   ```

3. **Usuarios actualizan** con:
   ```bash
   dotnet tool update --global YitPush
   ```

## Desinstalar (si necesitas)

Para desinstalar la herramienta localmente:
```bash
dotnet tool uninstall --global YitPush
```

## Solución de Problemas

### Error: "Package already exists"
- Ya publicaste esa versión
- Incrementa el número de versión en el `.csproj`

### Error: "Invalid API key"
- Verifica que copiaste la API key completa
- Asegúrate de que la API key tenga permisos de "Push"

### Error: "Package validation failed"
- Revisa el email de NuGet para ver detalles
- Asegúrate de que toda la metadata esté correcta

## URLs Importantes

- **Tu paquete**: https://www.nuget.org/packages/YitPush (después de publicar)
- **API Keys**: https://www.nuget.org/account/apikeys
- **Documentación NuGet**: https://learn.microsoft.com/nuget/

## Versionado Semántico

Usa este formato para versiones: `MAJOR.MINOR.PATCH`

- **MAJOR** (1.x.x): Cambios que rompen compatibilidad
- **MINOR** (x.1.x): Nuevas funcionalidades (sin romper compatibilidad)
- **PATCH** (x.x.1): Corrección de bugs

Ejemplo:
- `1.0.0` → Primera versión
- `1.0.1` → Arreglo de un bug
- `1.1.0` → Nueva funcionalidad
- `2.0.0` → Cambio que rompe compatibilidad

## Comando Completo de Un Solo Paso

Para republicar una nueva versión:

```bash
# 1. Actualiza versión en YitPush.csproj manualmente
# 2. Ejecuta:
dotnet clean && \
dotnet pack -c Release && \
dotnet nuget push bin/Release/YitPush.*.nupkg \
  --api-key TU_API_KEY \
  --source https://api.nuget.org/v3/index.json \
  --skip-duplicate
```

El flag `--skip-duplicate` previene errores si accidentalmente intentas publicar la misma versión dos veces.
