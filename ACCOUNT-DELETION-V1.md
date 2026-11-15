> Actualizacion: el flujo ahora incluye consumidores de Care, IAM y Devices, y termina en Completed. La descripcion V1 de abajo documenta la etapa original; consultar [el flujo vigente](docs/rabbitmq-consumers.md).

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

`IntegrationMessaging:Enabled` esta configurado en `true` para el broker local.
RabbitMqIntegrationEventPublisher implementa IIntegrationEventPublisher y esta
registrado en DI. Publica JSON persistente con publisher confirms y mandatory.
Puede deshabilitarse IntegrationMessaging para conservar mensajes pendientes.
Si falla la conexion, autenticacion o publicacion, el outbox conserva el mensaje
y programa reintentos. No se simula una publicacion exitosa.

Exchange: `foll.integration` (topic durable).
Cola: `care.account-deletion-requested.v1` (durable).
Routing key: `iam.account-deletion-requested.v1`.
La cola se declara y vincula al establecer la primera publicacion. Todavia no
hay consumidor de Care ni confirmaciones de negocio. Publisher confirms solo
permite avanzar a WaitingForConfirmations, nunca significa cuenta eliminada.

Ver `docs/rabbitmq-publication.md` para configuracion y pruebas del transporte.

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
Sin argumentos no conecta a PostgreSQL ni al broker. Con --rabbitmq agrega pruebas reales de publicacion en recursos aislados. No reemplaza pruebas de
transacciones, concurrencia y entrega cuando se conecte el transporte.
