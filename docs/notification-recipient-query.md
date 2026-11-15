# Datos mínimos para destinatarios

`GET /internal/iam/users/{userId}/notification-recipient` es la consulta específica que Care necesita al resolver destinatarios desde un worker sin sesión de usuario. Requiere `X-Foll-Service-Key` con `InternalApi:BackgroundQueryKey` y devuelve únicamente `userId`, `firstName`, `lastName` y `phoneNumber`.

No expone correo, hash de contraseña ni datos de cuenta adicionales. No modifica usuarios ni emite JWT de servicio. `/api/iam/users/{id}` mantiene su autenticación por JWT para peticiones con contexto de usuario.

Notifications ya no consulta IAM para el nombre de quien cerró un incidente: Emergency incluye `ClosedByName` en el evento a partir de los claims de su JWT validado.

La llave compartida de desarrollo se configura en `appsettings.json`, como en Care/Notifications. No se añadió una credencial por microservicio ni una migración de base de datos. Swagger identifica esta consulta con el esquema `BackgroundQueryKey`.
