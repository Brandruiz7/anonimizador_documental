# 🏛️ Oracle APEX — Frontend institucional

[← Volver al README principal](../README.md)

## Requisitos previos

- Oracle XE 21c con APEX 26.1 instalado
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

## Arquitectura de comunicación

El browser **nunca llama directamente a la API .NET**. Toda la comunicación es server-side:

```text
Browser → Oracle APEX (submit de página)
              ↓
         PKG_WIZARD_ANON (UTL_HTTP)
              ↓
         API .NET :5255
```

Por este motivo **CORS está deshabilitado** en la API. No se necesita — el origen de los requests es siempre el servidor Oracle, no el browser del usuario.

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

## Deploy de los packages PL/SQL

Ejecutar en SQL Developer conectado como `anonimizador@XEPDB1`. Los bloques del spec y body deben ejecutarse por separado:

```
1. Ejecutar solo el Package Spec → verificar STATUS = VALID
2. Ejecutar solo el Package Body → verificar STATUS = VALID
```

### Package de autenticación

Ver `DB/Oracle Database/Package.sql`. Implementa el Custom Authentication Scheme que conecta el login de APEX con la API JWT.

### Package del wizard (`PKG_WIZARD_ANON`)

Ver `DB/Oracle Database/pkg_wizard_anon.sql`. Gestiona todo el flujo del wizard de anonimización.

**Objetos que crea:**

| Objeto | Tipo | Descripción |
|---|---|---|
| `wizard_result_files` | TABLE | Almacena el documento anonimizado (BLOB) entre el procesamiento y la descarga |
| `wizard_session_files` | TABLE | Persiste el archivo original entre pasos del wizard |
| `t_person_data` | TYPE OBJECT | Datos de una persona con 15 campos incluyendo variantes |
| `t_persons_list` | TYPE TABLE | Lista de personas para pasar al API |
| `pkg_wizard_anon` | PACKAGE | Package principal del wizard |

**Campos de `t_person_data`:**

| Campo | Columna APEX_COLLECTION | Descripción |
|---|---|---|
| `fullname` | c001 | Nombre completo |
| `identification` | c002 | Cédula o identificación |
| `email` | c003 | Correo electrónico |
| `phone_number` | c004 | Teléfono |
| `position` | c005 | Cargo o puesto |
| `address` | c006 | Dirección |
| `institution` | c007 | Institución |
| `bank_account` | c008 | Cuenta bancaria |
| `medical_cond` | c009 | Condición médica |
| `free_text` | c010 | Texto libre a anonimizar |
| `name_vars` | c011 | Variantes del nombre (separadas por `\|`) |
| `id_vars` | c012 | Variantes de cédula (separadas por `\|`) |
| `phone_vars` | c013 | Variantes de teléfono (separadas por `\|`) |
| `bank_vars` | c014 | Variantes de cuenta bancaria (separadas por `\|`) |
| `medical_vars` | c015 | Variantes de condición médica (separadas por `\|`) |

**Procedimientos del package:**

| Procedimiento / Función | Descripción |
|---|---|
| `upload_document` | Lee el archivo de `wizard_session_files`, construye el multipart, llama al API y guarda el resultado en `wizard_result_files` |
| `analyze_document` | Llama al endpoint de análisis IA y retorna el JSON con personas detectadas |
| `get_result_blob` | Recupera el BLOB del documento anonimizado por clave de sesión |
| `clean_old_results` | Elimina registros de más de 2 horas en ambas tablas temporales |
| `send_request_text` | Helper interno para llamadas HTTP cuya respuesta es texto/JSON |
| `send_request_binary` | Helper interno para llamadas HTTP cuya respuesta es binaria (el documento anonimizado) |

**Por qué dos funciones `send_request`:** la función original usaba un parámetro OUT BLOB para distinguir el modo binario del texto, pero en PL/SQL un LOB temporal inicializado no es `NULL` — la condición `IF p_resp_blob IS NOT NULL` siempre era TRUE. Al separar en dos funciones (`send_request_text` y `send_request_binary`) se elimina la ambigüedad y cada función maneja su tipo de respuesta correctamente.

**Por qué `wizard_session_files`:** APEX elimina los archivos de `apex_application_temp_files` al terminar el request del submit (cuando el usuario pasa del Paso 1 al Paso 2). Al copiar el archivo a `wizard_session_files` en el proceso "Ir a Paso 2", el archivo queda disponible cuando el usuario presiona "Anonimizar Documento" en el Paso 2.

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

Paleta institucional completa:

| Color | Hex | Rol en APEX |
|---|---|---|
| Reflex Blue U | `#384A99` | Primary — botones, links, focus |
| Pantone 152 U | `#E57B3C` | Secondary accent |
| Process Blue U | `#1270B8` | Active/pressed states |
| Pantone 306 U | `#28A9E0` | Info — gráficos, badges |
| Pantone 116 U | `#F8AE40` | Warning — alertas suaves |
| Verde oliva | `#8EA725` | Success — botón Agregar Persona |

---

## Application Items

Crear en **Shared Components → Application Items**:

| Name | Scope | Session State Protection |
|---|---|---|
| `G_JWT_TOKEN` | Application | Restricted - May not be set from browser |
| `G_USER_ROLE` | Application | Restricted - May not be set from browser |
| `G_USER_FULLNAME` | Application | Restricted - May not be set from browser |

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

---

## Wizard de Anonimización — Page 20

### Items de sesión

| Item | Tipo | Descripción |
|---|---|---|
| `P20_STEP` | Hidden | Paso actual del wizard (1, 2 o 3). Default: `1` |
| `P20_FILE` | File Upload | Archivo a anonimizar. Storage: `APEX_APPLICATION_TEMP_FILES`, Purge: `End of Session` |
| `P20_MODE` | Radio Group | Tipo de procesamiento: MANUAL o IA. CSS Class: `modo-toggle` |
| `P20_CASE_NUMBER` | Text Field | Número de expediente |
| `P20_OFFICE_NUMBER` | Text Field | Número de oficio |
| `P20_SESSION_FILE_KEY` | Hidden | Clave para recuperar el archivo de `wizard_session_files`. Value Protected: No |
| `P20_RESULT_KEY` | Hidden | Clave para recuperar el documento anonimizado de `wizard_result_files` |
| `P20_RESULT_FILE` | Hidden | Nombre del archivo anonimizado |
| `P20_JWT_TOKEN` | Hidden | Token JWT copiado de `G_JWT_TOKEN` al inicio de sesión |

### Items del diálogo de persona (DLG_PERSON)

| Item | Descripción |
|---|---|
| `P20_DLG_SEQ` | Seq ID de la colección (0 = nuevo) |
| `P20_DLG_FULLNAME` | Nombre completo |
| `P20_DLG_ID` | Identificación |
| `P20_DLG_EMAIL` | Correo electrónico |
| `P20_DLG_PHONE` | Teléfono |
| `P20_DLG_POSITION` | Cargo |
| `P20_DLG_ADDRESS` | Dirección |
| `P20_DLG_INSTITUTION` | Institución |
| `P20_DLG_BANK` | Cuenta bancaria |
| `P20_DLG_MEDICAL` | Condición médica |
| `P20_DLG_FREETEXT` | Texto libre |
| `P20_DLG_NAME_VARS` | Variantes nombre (separadas por `\|`). Value Protected: No |
| `P20_DLG_ID_VARS` | Variantes cédula. Value Protected: No |
| `P20_DLG_PHONE_VARS` | Variantes teléfono. Value Protected: No |
| `P20_DLG_BANK_VARS` | Variantes cuenta bancaria. Value Protected: No |
| `P20_DLG_MEDICAL_VARS` | Variantes condición médica. Value Protected: No |

### Procesos principales (Processing)

| Proceso | Botón | Descripción |
|---|---|---|
| `Ir a Paso 2` | `BTN_SIGUIENTE` | Copia el archivo a `wizard_session_files`, limpia la colección `WIZARD_PERSONS` y setea `P20_STEP = 2` |
| `Guardar Persona` | `BTN_SAVE_PERSON` | Agrega o actualiza una persona en `APEX_COLLECTION('WIZARD_PERSONS')` usando c001-c015 |
| `Anonimizar Documento` | `BTN_ANON_WIZ` | Construye `t_persons_list` desde la colección y llama a `pkg_wizard_anon.upload_document` |
| `Corregir` | `BTN_CORREGIR_WIZ` | Setea `P20_STEP = 2` |
| `Reset` | `BTN_RESET_WIZ` | Limpia colección, items de sesión y setea `P20_STEP = 1` |
| `Ir Atrás` | `BTN_ATRAS_WIZ` | Setea `P20_STEP = 1` |

### Ajax Callbacks

| Proceso | Descripción |
|---|---|
| `LOAD_PERSON` | Carga los datos de una persona (c001-c015) desde la colección para edición |
| `DELETE_PERSON` | Elimina una persona de la colección por seq_id |
| `DOWNLOAD_RESULT` | Sirve el documento anonimizado como stream para descarga |

### Application Processes (Shared Components)

| Proceso | Point | Descripción |
|---|---|---|
| `DOWNLOAD_RESULT_APP` | Ajax Callback | Descarga el documento con `Content-Disposition: attachment` |
| `PREVIEW_RESULT_APP` | Ajax Callback | Sirve el documento inline para el iframe de preview (PDF) |

### Notas sobre APEX 26.1

**Static IDs no se renderizan como `id` en el HTML.** APEX 26.1 genera IDs numéricos internos (ej: `B6071101805330905`). Para interactuar con botones desde JavaScript, usar selección por texto:

```javascript
apex.jQuery('button').filter(function() {
    return apex.jQuery(this).text().trim().indexOf('Descargar') !== -1;
});
```

**Botones en el wizard header.** Los botones de navegación (SIGUIENTE, ATRÁS, ANONIMIZAR, CORREGIR, DESCARGAR, PROCESAR OTRO) se ubican en el slot `Edit` o `Change` de la región `HDR_WIZARD2` con `Server-side Condition: P20_STEP = N`. Esto evita depender de JavaScript para mostrarlos/ocultarlos.

**Descarga de archivos.** El botón Descargar usa `Action: Defined by Dynamic Action` para que no haga submit. JavaScript captura el clic y llama al Application Process `DOWNLOAD_RESULT_APP` vía `fetch()`, convierte la respuesta a Blob y crea un enlace de descarga temporal.

---

## Configuración de sesión

### Expiración del JWT

El JWT expira según `Jwt:ExpirationHours` en `appsettings.json` (default: 8 horas). Para que la sesión de APEX no sobreviva al token, configurar en **Shared Components → Security Attributes**:

- **Max Session Length in Seconds**: igual o menor a `Jwt:ExpirationHours × 3600`
- **Max Session Idle Time in Seconds**: según política de seguridad institucional

### Usernames case-insensitive

El package envía el username en `UPPER()` antes de llamar al API. El SP `SP_USER_GET_BY_USERNAME` hace la comparación con `UPPER()` en ambos lados. Esto permite ingresar con `admin`, `Admin` o `ADMIN` indistintamente.

---

## Pase a producción

| Elemento | Dev local | Producción |
|---|---|---|
| URL de la API en `pkg_wizard_anon.sql` | `http://127.0.0.1:5255` | URL institucional HTTPS |
| URL de la API en `Package.sql` | `http://127.0.0.1:5255` | URL institucional HTTPS |
| ACL host | `127.0.0.1` | Hostname/IP del servidor API |
| Protocolo | HTTP | HTTPS (requiere Oracle Wallet con cert) |
| `Jwt:ExpirationHours` | 8h | Alinear con max session de APEX institucional |
| Endpoint `generate-hash` | Disponible | Remover o proteger con autorización |

---

## Ver también

- [Base de datos](base-de-datos.md)
- [Seguridad](seguridad.md)
- [Configuración](configuracion.md)