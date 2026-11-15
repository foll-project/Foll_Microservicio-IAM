# Consumidores RabbitMQ

Los consumidores arrancan automaticamente. Se pueden desactivar con
`IntegrationConsumers:Enabled=false` (variable de entorno: `IntegrationConsumers__Enabled=false`).
La publicacion del outbox se controla por separado con `IntegrationMessaging:Enabled`.
Todos usan los mismos parametros de `RabbitMq` y el exchange topic durable `foll.integration`.

| Servicio receptor | Cola durable | Evento | Efecto |
| --- | --- | --- | --- |
| Care | care.account-deletion-requested.v1 | iam.account-deletion-requested.v1 | Elimina pacientes cuyo cuidador principal es el usuario; retira sus otras relaciones, restaura turnos al principal y elimina sus invitaciones y anotaciones. |
| IAM | iam.care-account-deletion-completed.v1 | care.account-deletion-completed.v1 | Valida OperationId y UserId, elimina el usuario y conserva la solicitud como Completed. |
| DeviceManagment | devices.care-account-deletion-completed.v1 | care.account-deletion-completed.v1 | Desvincula y reinicia el monitoreo de los dispositivos de los pacientes eliminados. |
| NotificationCommunication (en foll-backend) | notificationcommunication.invitation-events.v1 | care.invitation-created.v1 / care.invitation-accepted.v1 / care.invitation-rejected.v1 | Emite invitation.changed al grupo SignalR user:{TargetUserId}. |

## Eliminacion de cuenta

IAM responde 202 al guardar solicitud y outbox. Su publicador pasa la solicitud a
WaitingForConfirmations cuando RabbitMQ confirma el envio; eso aun no significa eliminacion.

Care consume la solicitud. La limpieza, el inbox y el evento
`care.account-deletion-completed.v1` se guardan en una misma transaccion local.
La confirmacion contiene EventId, OperationId, UserId, DeletedPatientIds, OccurredAtUtc y Version=1.
Care declara y vincula las dos colas de confirmacion al publicar, por lo que IAM y Devices pueden estar apagados.

IAM consume la confirmacion correspondiente a una solicitud existente y guarda Completed y CompletedAtUtc
junto al borrado del usuario y su inbox. La migracion elimina la FK que antes impedia conservar la solicitud
despues del borrado. Una confirmacion repetida no vuelve a ejecutar la eliminacion.
Devices procesa la misma confirmacion en su propia cola; su limpieza es eventualmente consistente y puede
finalizar despues de IAM. El dispositivo se conserva disponible para una nueva vinculacion.

Este flujo implementa la decision de eliminar tambien los pacientes del cuidador principal.
La eliminacion individual de un paciente por REST no publica esta confirmacion de eliminacion de cuenta.

## Entrega y errores

- ACK manual despues del commit de cambios e inbox.
- Prefetch=1 por consumidor.
- Inbox durable por servicio con EventId como clave; un duplicado confirmado se omite.
- PostgreSQL advisory locks por operacion y evento serializan entregas concurrentes entre replicas.
- El payload del inbox usa text para conservar exactamente el JSON recibido y detectar EventId reutilizados.
- Se valida version, identificadores, fecha UTC, routing key y metadatos MessageId/CorrelationId/Type.
- Errores transitorios: hasta cinco intentos, con esperas de 2, 4, 8 y 16 segundos.
- JSON/contratos invalidos y errores agotados: se publica el mensaje original en `{cola}.dead`,
  persistente y con publisher confirm; solo despues se confirma el original.
- Si falla el envio a .dead, se cierra el canal y RabbitMQ vuelve a entregar el original.
- Al perder conexion, el consumidor vuelve a conectar cada cinco segundos. HTTP sigue iniciando sin broker.

Para reprocesar una .dead: corregir la causa, publicar su cuerpo original en foll.integration con la routing key
original y preservar Type, MessageId y CorrelationId. Conservar el EventId permite deduplicacion.
No reenviar mensajes modificados con el mismo EventId. El inbox guarda datos personales del evento;
debe incluirse en la politica de retencion del sistema.

## Invitaciones y SignalR

El receptor usa el hub existente `/hubs/notifications`, requiere JWT y conserva el mensaje
`invitation.changed`. Agrega eventId y operationId para que el frontend pueda deduplicar y correlacionar.
El contrato recibido no contiene el nombre del solicitante; requesterName usa "Cuidador", manteniendo RequesterUserId.
El consumidor no consulta tablas de Care ni de IAM y no requiere propagar un JWT desde un worker.

SignalR es una entrega en vivo: un destinatario desconectado no recibe retroactivamente el aviso.
El estado durable de la invitacion se consulta en Care. Si el proceso cae entre el envio SignalR y el commit,
puede repetirse el aviso; el cliente debe deduplicar por eventId y usar occurredAt para tolerar desorden.

No se agrega revocacion distribuida de JWT ya emitidos; su validacion sigue las reglas existentes.
Completed confirma la limpieza de Care y el borrado de IAM, no una transaccion global con otros bounded contexts.

## Arranque y prueba manual

1. Reiniciar IAM, Care, DeviceManagment y foll-backend (este ultimo aloja NotificationCommunication).
   Sus migraciones de inbox se aplican con la configuracion de inicio existente.
2. Abrir RabbitMQ Management en http://localhost:15672 y verificar Consumers=1 por cola
   si se ejecuta una instancia de cada servicio.
3. Crear/aceptar/rechazar una invitacion por el gateway. Verificar logs de "procesado",
   el inbox notification.integration_inbox_messages y el aviso con el destinatario conectado al hub.
4. Con una cuenta descartable que tenga pacientes, solicitar DELETE /api/iam/auth
   con password. Verificar iam.account_deletion_requests.status=Completed,
   pacientes eliminados en Care y dispositivos liberados en Devices.
5. Detener un receptor, generar un evento y verificar Ready>0. Reiniciarlo y comprobar que procesa el pendiente.

Las solicitudes de eliminacion ya pendientes tambien se procesan al arrancar los nuevos consumidores.

## Pruebas automatizadas

Desde la raiz de cada uno de los cuatro proyectos:

```powershell
dotnet run --project tests/Consumers.Checks/Consumers.Checks.csproj -c CodexConsumers -- --rabbitmq
```

Usan PostgreSQL real: crean y eliminan exclusivamente una base temporal
`foll_checks_consumers_<guid>`; el usuario local debe tener permiso CREATEDB.
Sin --rabbitmq prueban handlers, transacciones, deduplicacion y validacion.
Con --rabbitmq tambien crean exchange/colas foll.checks.consumers.<guid>, verifican recepcion,
reintento transitorio, duplicados y aislamiento de JSON invalido en .dead, y eliminan esos recursos.
No consumen colas de negocio ni borran datos de las bases configuradas.

Las pruebas de publicacion existentes siguen disponibles en tests/Care.V1.Checks y tests/Iam.V1.Checks,
tambien con --rabbitmq. Care verifica el fan-out de la confirmacion a dos colas aisladas.

