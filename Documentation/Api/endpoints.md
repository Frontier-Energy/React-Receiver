# API Endpoints

This document summarizes the public HTTP surface of React-Receiver.

## Conventions

- Error responses use RFC 7807 problem details.
- Some mutable resources use optimistic concurrency with `ETag` and `If-Match`.
- Health endpoints are infrastructure checks.

## Health

- `GET /health/live`
- `GET /health/ready`
- `GET /health/startup`

## Auth

### `POST /auth/login`

Purpose:

- lookup-style login by email

Request body:

```json
{
  "email": "jane.doe@example.com"
}
```

### `POST /auth/register`

Purpose:

- create a user if one does not already exist

Request body:

```json
{
  "firstName": "Jane",
  "lastName": "Doe",
  "email": "jane.doe@example.com"
}
```

## Users

### `POST /users/lookup`

Request body:

```json
{
  "userId": "user-123"
}
```

Responses:

- `200 OK`
- `404 Not Found`

### `GET /users/me`

Responses:

- `200 OK`

## Inspections

### `POST /inspections`

Consumes:

- `multipart/form-data`

Form fields:

- `Payload`: JSON string
- `Files`: optional file collection

Payload example:

```json
{
  "sessionId": "test-session-001",
  "userId": "test-user-001",
  "name": "Test Inspection",
  "queryParams": {
    "priority": "high"
  }
}
```

Typical response body:

```json
{
  "status": "Received",
  "sessionId": "test-session-001",
  "name": "Test Inspection",
  "queryParams": {
    "priority": "high"
  },
  "message": "Accepted for eventual processing"
}
```

Notes:

- success means the request was accepted and staged
- downstream queue publication may still complete asynchronously

### `GET /inspections/{sessionId}`

Responses:

- `200 OK`
- `404 Not Found`

### `GET /inspections/{sessionId}/files/{fileName}`

Responses:

- `200 OK`
- `404 Not Found`

## Form Schemas

### `GET /form-schemas`

Lists available schema catalog entries.

### `GET /form-schemas/{formType}`

Responses:

- `200 OK`
- `404 Not Found`

Headers:

- `ETag`
- `X-Form-Schema-Version`

### `PUT /form-schemas/{formType}`

Headers:

- `If-Match` required for updates to an existing resource

Responses:

- `201 Created`
- `200 OK`
- `428 Precondition Required`
- `412 Precondition Failed`

## Translations

### `GET /translations/{language}`

Responses:

- `200 OK`
- `404 Not Found`

Headers:

- `ETag`

### `PUT /translations/{language}`

Headers:

- `If-Match` required for updates to an existing resource

Responses:

- `201 Created`
- `200 OK`
- `428 Precondition Required`
- `412 Precondition Failed`

## Tenant Config

### `GET /tenant-config?tenantId={tenantId}`

Responses:

- `200 OK`
- `404 Not Found`

Headers:

- `ETag`

### `PUT /tenant-config/{tenantId}`

Headers:

- `If-Match` required for updates to an existing resource

Responses:

- `201 Created`
- `200 OK`
- `428 Precondition Required`
- `412 Precondition Failed`

## Error Handling

Mapped problem responses include:

- `400 Invalid request payload`
- `412 If-Match precondition failed`
- `428 Missing If-Match header`
- `500 Schema content unavailable`
- `500 An unexpected error occurred`
