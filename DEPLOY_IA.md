# Deploy de Quantitary IA

La clave de Gemini no se guarda en el repositorio.

## Configuración requerida

1. Ejecutar `script.sql` sobre la base de datos de Quantitary. La creación de `IAChats`, `IAMensajes` e `IAAuditoria` ya está integrada en ese único archivo y no vuelve a crear esas tablas si ya existen.
2. Configurar la variable de entorno `GEMINI_API_KEY` con la clave de Google AI Studio.
3. En un servidor que no use la base local, configurar `ConnectionStrings__DefaultConnection` con la cadena de conexión de SQL Server.

Para desarrollo local también puede configurarse la clave con:

```bash
dotnet user-secrets set "GEMINI_API_KEY" "TU_CLAVE"
```

## Verificación antes de publicar

```bash
dotnet restore
dotnet build
dotnet run
```

Probar el chat, el historial, las alertas y una modificación de producto con un usuario de pruebas. Las altas y modificaciones requieren dos confirmaciones y quedan registradas en `dbo.IAAuditoria`.
