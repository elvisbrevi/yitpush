## feat: añade generación de descripción de PRs vía DeepSeek API

## Resumen
Este PR introduce una nueva funcionalidad para generar automáticamente descripciones de pull requests en formato Markdown, comparando dos ramas de Git y utilizando la API de DeepSeek Reasoning. La función se activa con el nuevo flag `--pr-description` y puede combinarse con opciones de idioma y modo detallado.

## Cambios
- **Nueva opción `--pr-description`**: Añadido flag para activar el modo generador de descripciones de PR
- **Flujo interactivo**: Implementado menú interactivo para seleccionar ramas origen y destino
- **Integración con DeepSeek**: Conexión con API de DeepSeek para generar descripciones en múltiples idiomas
- **Soporte para modos**: Compatible con modo simple y detallado (usando flag `--detailed`)
- **Gestión de ramas**: Nuevos métodos para listar ramas disponibles y obtener diferencias entre ellas
- **Persistencia de resultados**: Descripciones generadas se guardan en archivos Markdown con nombres descriptivos
- **Actualización de ayuda**: Documentación ampliada en el mensaje de ayuda del programa
- **Manejo de errores**: Implementado sistema de reintentos y gestión de errores de API

**Archivos modificados:**
- `Program.cs`: Añadidas funciones `GeneratePrDescription`, `GetGitBranches`, `GetBranchDiff` y `GeneratePrDescriptionContent`