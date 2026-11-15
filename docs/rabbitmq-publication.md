> Actualizacion: IAM ya consume care.account-deletion-completed.v1 y completa la eliminacion. Consultar [consumidores y pruebas](rabbitmq-consumers.md); las referencias a consumidores futuros corresponden a la etapa inicial.

# Publicación RabbitMQ

## Configuración actual

`IntegrationMessaging:Enabled` está activo. Los parámetros de conexión y publicación están en `appsettings.json`, sección `RabbitMq`, visibles y editables. No se usa User Secrets ni se reorganizan perfiles de configuración.

Durante la comprobación local, el broker rechazó la contraseña proporcionada en la conversación. Se conservó el usuario y se utilizó la contraseña de inicialización del contenedor foll-rabbitmq, cuya autenticación se verificó. Revisar el valor de RabbitMq:Password si se cambia el broker. No se modificaron cuentas, permisos ni contraseñas del broker.

- Host y puerto locales: localhost:5672; virtual host: /.
- Exchange: `foll.integration`, tipo topic, durable.
- Cola de destino: `care.account-deletion-requested.v1`, durable, no exclusiva y sin auto-delete.
- Routing keys/tipos: iam.account-deletion-requested.v1.
- Timeout por publicación, incluyendo espera del canal y conexión: 10 segundos.

El publicador declara exchange, cola y bindings antes de su primer envío. Esto conserva eventos aunque el consumidor aún no exista. La creación de topología requiere permisos configure/write/read apropiados. Las réplicas de un mismo consumidor compartirán su cola; otros IAMs necesitan colas propias.

## Archivos

- `IAM/Infrastructure/Messaging/Configuration/RabbitMqOptions.cs`: opciones de conexión y destino.
- `IAM/Infrastructure/Messaging/RabbitMq/RabbitMqConnection.cs`: conexión/canal persistentes, publicación serializada, topología, timeout, confirms y reconexión por reintento del outbox.
- `IAM/Infrastructure/Messaging/RabbitMq/RabbitMqIntegrationEventPublisher.cs`: implementación del contrato de Application y serialización del evento.
- `Program.cs`: validación de opciones y registro singleton de conexión/publicador.
- Proyecto: dependencia RabbitMQ.Client 7.2.2.

## Garantías y límites

El adaptador publica JSON UTF-8 en PascalCase, delivery mode persistente y mandatory. Usa publisher confirms con seguimiento: nack, basic.return, timeout o desconexión producen excepción. El outbox solo marca PublishedAtUtc tras aceptación confirmada del broker. Preserva MessageId = EventId, CorrelationId = OperationId, Type/routing key = tipo estable y timestamp del evento.

Una conexión/canal por instancia se reutiliza y un SemaphoreSlim serializa sus operaciones. Si falla un envío se descarta la conexión y el siguiente intento del outbox vuelve a conectar y declarar la topología. El worker mantiene sus reintentos con backoff y puede reenviar un evento aceptado si falla el commit local. Los consumidores futuros deben deduplicar por EventId y tolerar reordenamientos.

Publisher confirms NO confirma procesamiento de negocio. Esta etapa no incorpora consumidores, inbox ni cambios al protocolo de eliminación de cuentas. Tampoco envía avisos al frontend: NotificationCommunication debe consumir las invitaciones y emitirlos.

## Prueba local

Reiniciar el servicio con el código nuevo. Los eventos pendientes anteriores también pueden enviarse. No es necesario reiniciar ni modificar el broker.

La aplicación y su worker utilizan la base real configurada. Esta integración no añade una migración nueva. En IAM, la publicación confirmada lleva la solicitud a WaitingForConfirmations; no elimina la cuenta. En Care, crear/aceptar/rechazar invitaciones genera los eventos. Estas operaciones de invitación no consultan DeviceManagment, pero requieren pacientes/invitaciones existentes; crear o consultar pacientes sigue consultando DeviceManagment.

En RabbitMQ Management (http://localhost:15672) comprobar la cola y el exchange anteriores. Las colas aparecen cuando se realiza el primer envío; al no existir consumidor, los mensajes se acumulan como Ready.

Comprobaciones existentes sin conexión al broker:

```powershell
dotnet run --project tests/Iam.V1.Checks/Iam.V1.Checks.csproj
```

Agregar comprobaciones contra el broker:

```powershell
dotnet run --project tests/Iam.V1.Checks/Iam.V1.Checks.csproj -- --rabbitmq
```

Las comprobaciones del broker publican eventos sintéticos solo en un exchange/cola con nombre foll.checks y sufijo único, leen sus propios mensajes y eliminan esos recursos al terminar. Verifican JSON, persistencia, routing, identificadores en reintentos, publicación sin ruta, reconexión, cancelación, versión y concurrencia de llamadas. No publican eventos sintéticos en las colas de negocio ni requieren una base PostgreSQL real.