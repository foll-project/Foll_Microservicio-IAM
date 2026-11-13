# Eliminacion de cuenta — V1

Esta version registra una solicitud y su evento de integracion. No elimina ni bloquea
la cuenta. No consume confirmaciones de otros BCs ni toma decisiones de commit/cancelacion.

## API

Ambos endpoints requieren `Authorization: Bearer <JWT>` y usan exclusivamente el
claim `userId`. No admiten seleccionar otro usuario por URL o body.

- `DELETE /api/iam/auth`, body `{"password":"..."}`: valida la contrasena, registra
  la operacion y responde `202 Accepted` con `Location` apuntando al endpoint de estado.
  Una repeticion con credenciales validas devuelve la operacion existente, sin otro evento.
  Body invalido/contrasena incorrecta: `400`. JWT ausente/invalido: `401`.
- `GET /api/iam/auth/account-deletion`: devuelve `200` y el estado de la cuenta
  autenticada, o `404` si no tiene solicitud.

Ejemplo de respuesta (las fechas y el identificador varian):

```json
{
  "operationId": "b00fd20a-1436-4e7c-8ba4-ea0ef26f7423",
  "status": "PendingPublication",
  "requestedAtUtc": "2026-11-13T15:00:00Z",
  "publishedAtUtc": null
}
```

## Estados y persistencia

- `PendingPublication`: la solicitud y el mensaje estan guardados; falta confirmacion del transporte.
- `WaitingForConfirmations`: el transporte acepto el mensaje de forma durable. No implica
  que los BCs lo hayan recibido, procesado o aprobado. Es el ultimo estado de esta V1.

`UserCommandService` agrega `AccountDeletionRequest` mediante `IUserRepository` y
registra el evento mediante `IUserEventService`. `OutboxUserEventService` agrega el
mensaje al mismo `AppDbContext`; un solo `IUnitOfWork.CompleteAsync` guarda ambos
con la transaccion local automatica de EF Core. Una restriccion unica por `UserId`
impide solicitudes simultaneas duplicadas; el perdedor recupera la operacion existente.
La restriccion limita esta V1 a una operacion por usuario. Al agregar estados terminales
y nuevas solicitudes habra que evolucionarla para conservar historial.

Tablas nuevas: `iam.account_deletion_requests` y `iam.integration_outbox_messages`.
La migracion incluida se aplica al arrancar si `ApplyMigrationsOnStartup` esta habilitado.
La solicitud referencia al usuario con borrado restringido; antes de implementar el
borrado definitivo habra que decidir la conservacion del historial y adaptar esa relacion.

## Contrato para el broker

Tipo estable: `iam.account-deletion-requested.v1`.
Payload JSON: `EventId`, `OperationId`, `UserId`, `OccurredAtUtc`, `Version` (1).
La serializacion actual usa esos nombres PascalCase. No contiene contrasenas ni hashes.

`IUserEventService` es el puerto de Application para registrar eventos de usuario.
`IIntegrationEventPublisher` es el puerto de Application para entregar al transporte;
su implementacion concreta pertenece a Infrastructure y depende del broker elegido.

El adaptador debe:

- Publicar el contrato en el destino logico acordado, con entrega durable y confirmacion.
- Completar `PublishAsync` solo cuando el transporte confirme la aceptacion; lanzar
  excepcion si falla o no se conoce el resultado. Respetar `CancellationToken` y
  configurar un timeout finito en el cliente del broker.
- Preservar `EventId` y `OperationId` en reintentos. No generar identificadores nuevos.

## Activacion

`IntegrationMessaging:Enabled` es `false` por defecto. El worker informa que esta
deshabilitado y no modifica los mensajes. No existe un publicador falso ni una llamada
HTTP provisional. Sin broker, el estado permanece `PendingPublication`.

Cuando exista el adaptador, registrar su implementacion de `IIntegrationEventPublisher`
en DI y habilitar `IntegrationMessaging`. Habilitarlo sin adaptador hace fallar el arranque
explicitamente. `PollIntervalSeconds` y `BatchSize` controlan el procesamiento.

El worker reclama un lote mediante `FOR UPDATE SKIP LOCKED` para permitir replicas.
Mantiene la transaccion del lote durante los envios, por lo que conviene usar lotes
pequenos y timeouts de transporte acotados. Los fallos programan reintentos con espera
exponencial de hasta 300 segundos; intento y ultimo error quedan en el outbox.
No hay politica de intervencion manual/dead-letter en esta V1; los fallos permanentes
requieren corregir el adaptador o los datos y observar los errores registrados.

Publicacion y estado de la solicitud se actualizan juntos. Si el transporte acepta
pero el guardado falla, se repetira la entrega con el mismo `EventId`. Los futuros
consumidores deben ser idempotentes. Tambien deben tolerarse confirmaciones que
lleguen antes de que IAM registre localmente `WaitingForConfirmations`.

## Verificacion

```powershell
dotnet run --project tests/Iam.V1.Checks/Iam.V1.Checks.csproj
```

Es un ejecutable de comprobaciones sin paquetes de test adicionales. Comprueba reglas,
servicios, contrato, configuracion EF, SQL de migraciones y controles del worker/API.
No conecta a PostgreSQL ni a un broker; no reemplaza pruebas de integracion reales de
transacciones, concurrencia y entrega cuando se conecte el transporte.
