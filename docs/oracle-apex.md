# 🏛️ Oracle APEX — Frontend institucional

[← Volver al README principal](../README.md)

## Requisitos previos

- Oracle XE 21c con APEX 26.x instalado
- Workspace `anonimizador` creado y schema `ANONIMIZADOR` asociado
- ORDS corriendo con JDK 26+
- API .NET corriendo en `http://127.0.0.1:5255`

---

## Arranque del entorno local

```cmd
:: 1. Iniciar servicios de Oracle
net start OracleServiceXE
net start OracleXETNSListener
```

```powershell
# 2. Agregar JDK al PATH y arrancar ORDS
$env:PATH += ";C:\Users\brand\Documents\OracleAPEX\jdk-26_windows-x64_bin\jdk-26.0.1\bin"
cd C:\Users\brand\Documents\OracleAPEX\ords-latest
java -jar ords.war serve
```

URL de acceso: `http://localhost:8080/ords/apex`

---

## Configuración del ACL de red

Oracle bloquea por defecto las llamadas HTTP salientes. Ejecutar como `SYS` en `XEPDB1`:

```sql
ALTER SESSION SET CONTAINER = XEPDB1;

-- Permiso para el schema ANONIMIZADOR
BEGIN
  DBMS_NETWORK_ACL_ADMIN.APPEND_HOST_ACE(
    host => '127.0.0.1',
    ace => xs$ace_type(
      privilege_list => xs$name_list('connect', 'resolve'),
      principal_name => 'ANONIMIZADOR',
      principal_type => xs_acl.ptype_db
    )
  );
  COMMIT;
END;
/

-- Permiso para APEX_PUBLIC_USER (usuario de sesión de APEX)
BEGIN
  DBMS_NETWORK_ACL_ADMIN.APPEND_HOST_ACE(
    host => '127.0.0.1',
    ace => xs$ace_type(
      privilege_list => xs$name_list('connect', 'resolve'),
      principal_name => 'APEX_PUBLIC_USER',
      principal_type => xs_acl.ptype_db
    )
  );
  COMMIT;
END;
/
```

Verificar que quedó configurado:

```sql
SELECT principal, privilege, is_grant
FROM dba_network_acl_privileges
WHERE principal IN ('ANONIMIZADOR', 'APEX_PUBLIC_USER')
ORDER BY principal;
-- Resultado esperado: 4 filas (connect + resolve para cada uno)
```

**Por qué `127.0.0.1` y no `localhost`:** Oracle trata `localhost` (hostname) y `127.0.0.1` (IP) como hosts distintos en el ACL. El paquete PL/SQL usa la IP directa para evitar ambigüedades en la resolución DNS interna de Oracle.

**Por qué dos usuarios:** el schema `ANONIMIZADOR` es el owner del package, pero APEX ejecuta el código bajo la sesión de `APEX_PUBLIC_USER`. La verificación ACL de Oracle usa el usuario de sesión, por lo que ambos necesitan el permiso.

En producción reemplazar `127.0.0.1` por el hostname o IP del servidor donde corre la API.

---

## Deploy del package PL/SQL

Ejecutar en SQL Developer conectado como `anonimizador@XEPDB1`. Los bloques del spec y body deben ejecutarse por separado:

```
1. Ejecutar solo el Package Spec → verificar STATUS = VALID
2. Ejecutar solo el Package Body → verificar STATUS = VALID
```

Ver `DB/Oracle Database/Package.sql` para el código completo.

**Por qué `UTL_HTTP` en lugar de `APEX_WEB_SERVICE`:** `APEX_WEB_SERVICE.MAKE_REST_REQUEST` es un package del schema `APEX_260100`. Cuando lo llama código en `ANONIMIZADOR`, Oracle verifica el ACL contra el schema owner de `APEX_WEB_SERVICE` (`APEX_260100`), no contra `ANONIMIZADOR`. Usar `UTL_HTTP` directamente ejecuta en el contexto de `ANONIMIZADOR` (AUTHID DEFINER), donde el ACL sí está configurado.

---

## Creación de la aplicación en APEX

1. App Builder → **Create** → **New Application**
2. Configuración:

| Campo | Valor |
|---|---|
| Name | `Anonimizador CGR` |
| Theme Style | Vita |
| Navigation | Side Menu |
| Features | Ninguna (desmarcar todas) |
| Schema | `ANONIMIZADOR` |

---

## Tema CGR — Theme Roller

1. Correr la app → Developer Toolbar (barra inferior) → ícono de rodillo de pintura
2. En el panel Theme Roller:
   - Style: **Vita**
   - Theme Color → **Custom** → `#384A99` (Pantone Reflex Blue U — azul institucional CGR)
3. **Save As** → nombre `CGR` → marcar **Set as Default Style** → **Save**

Paleta institucional completa (referencia `docs/paleta-cgr.png` si existe):

| Color | Hex | Rol en APEX |
|---|---|---|
| Reflex Blue U | `#384A99` | Primary — botones, links, focus |
| Pantone 152 U | `#E57B3C` | Secondary accent — wizard, robots SVG |
| Process Blue U | `#1270B8` | Active/pressed states |
| Pantone 306 U | `#28A9E0` | Info — gráficos, badges |
| Pantone 116 U | `#F8AE40` | Warning — alertas suaves |
| Verde oliva | `#8EA725` | Success — confirmaciones |

---

## Application Items

Crear en **Shared Components → Application Items**. Los tres items almacenan el estado de sesión del usuario autenticado.

| Name | Scope | Session State Protection |
|---|---|---|
| `G_JWT_TOKEN` | Application | Restricted - May not be set from browser |
| `G_USER_ROLE` | Application | Restricted - May not be set from browser |
| `G_USER_FULLNAME` | Application | Restricted - May not be set from browser |

La protección `Restricted` impide que JavaScript o URL params sobrescriban estos valores — solo código server-side puede escribirlos.

**Los Application Items no requieren cambios en la tabla `USERS` de la BD.** El token JWT es autocontenido y vive únicamente en la sesión de APEX. Cuando la sesión termina, APEX la limpia automáticamente.

---

## Authentication Scheme

1. Shared Components → **Authentication Schemes** → **Create**
2. Seleccionar **Based on a pre-configured scheme from the gallery** → Scheme Type: **Custom**
3. Configuración:

| Campo | Valor |
|---|---|
| Name | `Anonimizador API JWT` |
| Authentication Function Name | `pkg_auth_anonimizador.authenticate_user` |

4. Guardar → en la lista hacer click en el scheme → **Make Current Scheme**

**Nota sobre el Static ID:** APEX puede generar un Static ID duplicado si ya existe una entrada con el mismo nombre derivado. Si aparece el error "Static ID must be unique", cambiar el valor del campo Static ID en el tab Advanced (ej: `cgr-jwt-auth`) antes de guardar.

---

## Navegación

### Página Anonimizador

1. App Builder → **Create Page** → **Blank Page**
2. Page Number: `10`, Name: `Anonimizador`

### Entrada en el menú lateral

1. Shared Components → **Navigation → Navigation Menu → Desktop Navigation Menu**
2. **Create Entry**:

| Campo | Valor |
|---|---|
| List Entry Label | `Anonimizador` |
| Target Type | Page in this Application |
| Page | `10` |
| Image/Class | `fa-user-secret` (u otro ícono Font Awesome) |

---

## Configuración de sesión

### Expiración del JWT

El JWT expira según `Jwt:ExpirationHours` en `appsettings.json` (default: 8 horas). Para que la sesión de APEX no sobreviva al token, configurar en **Shared Components → Security Attributes**:

- **Max Session Length in Seconds**: igual o menor a `Jwt:ExpirationHours × 3600`
- **Max Session Idle Time in Seconds**: según política de seguridad institucional

Si el JWT expira mientras la sesión de APEX está activa, las llamadas al API retornarán `401`. Manejarlo redirigiendo al login desde un Application Process global.

### Usernames case-insensitive

El package envía el username en `UPPER()` antes de llamar al API:
```sql
l_body := '{"username":' || apex_json.stringify(UPPER(p_username)) || ...
```
El SP `SP_USER_GET_BY_USERNAME` hace la comparación con `UPPER()` en ambos lados. Esto permite ingresar con `admin`, `Admin` o `ADMIN` indistintamente, consistente con el comportamiento del sistema institucional.

---

## Pase a producción

| Elemento | Dev local | Producción |
|---|---|---|
| URL de la API en Package.sql | `http://127.0.0.1:5255` | URL institucional HTTPS |
| ACL host | `127.0.0.1` | Hostname/IP del servidor API |
| Protocolo | HTTP | HTTPS (requiere Oracle Wallet con cert) |
| `Jwt:ExpirationHours` | 8h | Alinear con max session de APEX institucional |
| Endpoint `generate-hash` | Disponible | Remover o proteger con autorización |
| CORS `AllowedOrigins` | localhost | Dominio institucional de Oracle APEX |

---

## Ver también

- [Base de datos](base-de-datos.md)
- [Seguridad](seguridad.md)
- [Configuración](configuracion.md)