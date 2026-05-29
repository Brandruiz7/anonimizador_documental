# 🔐 Seguridad

[← Volver al README principal](../README.md)

## Autenticación — JWT

El sistema usa **JWT Bearer** con firma **HMAC-SHA256**.

### Flujo de autenticación

```text
1. POST /api/auth/login con credenciales
2. AuthService busca el usuario en BD (SP_USER_GET_BY_USERNAME)
3. BCrypt.Verify() compara la contraseña con el hash almacenado
4. Si es válida, genera un JWT firmado con los siguientes claims:
   - sub: UserId
   - unique_name: Username
   - role: RoleName (Admin / Operator)
   - given_name: FullName
   - jti: GUID único por token
5. El token expira según Jwt:ExpirationHours (por defecto 8 horas)
6. El cliente incluye el token en: Authorization: Bearer {token}
```

### Validaciones del token

Cada request con JWT valida:
- **Issuer** — debe coincidir con `Jwt:Issuer`
- **Audience** — debe coincidir con `Jwt:Audience`
- **Lifetime** — el token no debe estar vencido
- **Firma** — verificada con `Jwt:Key` (mínimo 32 caracteres)

---

## Passwords — BCrypt

Las contraseñas se almacenan con **BCrypt** a cost factor 12. Nunca se almacena la contraseña en texto plano.

Para generar el hash de una contraseña nueva:
```
GET /api/auth/generate-hash?password=TuPassword
```

> ⚠️ Este endpoint debe eliminarse o protegerse antes de desplegar en producción.

---

## Roles

| Rol | Permisos |
|---|---|
| `Admin` | Acceso completo a todos los endpoints |
| `Operator` | Acceso a upload, análisis, historial y métricas |

---

## Rate Limiting

Protección contra ataques de fuerza bruta y abuso:

| Política | Límite | Aplica a |
|---|---|---|
| `login` | 10 requests / minuto / IP | `POST /api/auth/login` |
| `documents` | 30 requests / minuto / IP | Todos los endpoints de documentos |

Cuando se supera el límite retorna `429 Too Many Requests` con JSON estructurado.

Valores configurables en `appsettings.json`:
```json
"RateLimiting": {
  "Login":     { "PermitLimit": 10, "WindowMinutes": 1 },
  "Documents": { "PermitLimit": 30, "WindowMinutes": 1 }
}
```

---

## Headers de seguridad

Se agregan automáticamente a todas las respuestas:

| Header | Valor | Protección |
|---|---|---|
| `X-Content-Type-Options` | `nosniff` | Evita que el navegador interprete el MIME incorrecto |
| `X-Frame-Options` | `DENY` | Previene clickjacking — la API no debe embeberse en iframes |
| `Strict-Transport-Security` | `max-age=31536000` | Fuerza HTTPS en el navegador por 1 año |
| `Referrer-Policy` | `strict-origin-when-cross-origin` | Restringe información de referrer |
| `Permissions-Policy` | `camera=(), microphone=(), geolocation=()` | Deshabilita APIs del navegador no necesarias |
| `Content-Security-Policy` | `default-src 'self'` | Solo permite recursos del mismo origen |

---

## CORS

Configurable por entorno en `appsettings.json`:

```json
"Cors": {
  "AllowedOrigins": [
    "https://tu-dominio-apex.oracle.com",
    "https://localhost:7108"
  ]
}
```

En desarrollo agregar el origen local. En producción agregar el dominio de Oracle APEX institucional.

---

## Trazabilidad — CorrelationId

Cada request recibe un `X-Correlation-ID` único:
- Si el cliente lo envía en el header, se reutiliza
- Si no, se genera un GUID nuevo
- Se propaga en los logs y en el header de respuesta
- Permite rastrear el flujo completo de un request en los logs

---

## Procesamiento en memoria

El documento **nunca se escribe a disco** durante el proceso de anonimización. Todo ocurre en RAM:
- El archivo se carga en `MemoryStream`
- El procesamiento (DOCX o PDF) opera sobre bytes en memoria
- Los bytes originales se limpian con `Array.Clear()` después de procesar
- Solo el resultado anonimizado se retorna como stream al cliente

---

## Logs estructurados — Serilog

Los logs incluyen:
- Timestamp con hora exacta
- Nivel (INF, WRN, ERR)
- CorrelationId en cada request
- Intentos de login (usuario, resultado)
- Errores con stack trace en Development, solo mensaje en Production

Rotación diaria con retención de 30 días en `logs/api-YYYYMMDD.log`.

---

## Ver también

- [Configuración](configuracion.md)
- [Base de datos](base-de-datos.md)