# IAM API Documentation

Documento simple y directo de los endpoints actuales del servicio IAM.

## Endpoints

| Metodo | Ruta |
| --- | --- |
| POST | /api/iam/auth/register |
| POST | /api/iam/auth/login |
| GET | /api/iam/users/{userId} |

## 1. POST `/api/iam/auth/register`

### Header

| Campo | Descripcion |
| --- | --- |
| Content-Type | application/json |

### Request

| Campo | Tipo | Descripcion |
| --- | --- | --- |
| email | String | Correo del usuario |
| password | String | Clave del usuario |
| firstName | String | Nombre del usuario |
| lastName | String | Apellido del usuario |
| phoneNumber | String? | Telefono del usuario, opcional |

### Request Example

```json
{
  "email": "daniel@example.com",
  "password": "123456",
  "firstName": "Daniel",
  "lastName": "Clark",
  "phoneNumber": "999888777"
}
```

### Success 200

| Campo | Tipo | Descripcion |
| --- | --- | --- |
| message | String | Usuario registrado exitosamente. |

### Success Response Example

```json
HTTP/1.1 200 OK
{
  "message": "Usuario registrado exitosamente."
}
```

### Error 4xx

| Campo | Tipo | Descripcion |
| --- | --- | --- |
| message | String | Mensaje devuelto por la excepcion de negocio. |

### Error Response Example

```json
HTTP/1.1 400 BAD_REQUEST
{
  "message": "Error de validacion o negocio"
}
```

## 2. POST `/api/iam/auth/login`

### Header

| Campo | Descripcion |
| --- | --- |
| Content-Type | application/json |

### Request

| Campo | Tipo | Descripcion |
| --- | --- | --- |
| email | String | Correo del usuario |
| password | String | Clave del usuario |

### Request Example

```json
{
  "email": "daniel@example.com",
  "password": "123456"
}
```

### Success 200

| Campo | Tipo | Descripcion |
| --- | --- | --- |
| userId | Long | Identificador del usuario autenticado |
| email | String | Correo del usuario |
| firstName | String | Nombre del usuario |
| lastName | String | Apellido del usuario |
| phoneNumber | String? | Telefono del usuario, opcional |
| token | String | JWT emitido al autenticar |

### Success Response Example

```json
HTTP/1.1 200 OK
{
  "userId": 123,
  "email": "daniel@example.com",
  "firstName": "Daniel",
  "lastName": "Clark",
  "phoneNumber": "999888777",
  "token": "eyJhbGciOi..."
}
```

### Error 4xx

| Campo | Tipo | Descripcion |
| --- | --- | --- |
| message | String | Credenciales inválidas. |

### Error Response Example

```json
HTTP/1.1 401 UNAUTHORIZED
{
  "message": "Credenciales inválidas."
}
```

## 3. GET `/api/iam/users/{userId}`

### Header

| Campo | Descripcion |
| --- | --- |
| Authorization | Bearer {jwt} |

### Request

| Campo | Tipo | Descripcion |
| --- | --- | --- |
| userId | Long | Identificador del usuario |

### Request Example

```http
GET /api/iam/users/123
Authorization: Bearer eyJhbGciOi...
```

### Success 200

| Campo | Tipo | Descripcion |
| --- | --- | --- |
| userId | Long | Identificador del usuario |
| email | String | Correo del usuario |
| firstName | String | Nombre del usuario |
| lastName | String | Apellido del usuario |
| phoneNumber | String? | Telefono del usuario, opcional |

### Success Response Example

```json
HTTP/1.1 200 OK
{
  "userId": 123,
  "email": "daniel@example.com",
  "firstName": "Daniel",
  "lastName": "Clark",
  "phoneNumber": "999888777"
}
```

### Error 4xx

| Campo | Tipo | Descripcion |
| --- | --- | --- |
| message | String | Usuario no encontrado. |

### Error Response Example

```json
HTTP/1.1 404 NOT_FOUND
{
  "message": "Usuario no encontrado."
}
```

## Coverage

Este documento incluye todos los endpoints encontrados actualmente en el codigo fuente.