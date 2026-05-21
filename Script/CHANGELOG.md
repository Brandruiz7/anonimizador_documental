\# Changelog — DocumentAnonymizerDB



Historial de cambios aplicados a la base de datos.

Cada migración tiene su archivo correspondiente en la carpeta `Migrations/`.



\---



\## \[Base] — Script inicial

\*\*Archivo:\*\* `DocumentAnonymizerDB.sql`



\- Tablas: `PROCESS\_STATUS`, `DOCUMENTS`, `DOCUMENT\_VERSIONS`,

&#x20; `ANONYMIZED\_FIELDS`, `PROCESS\_ERRORS`, `ROLES`, `USERS`

\- Índices de rendimiento

\- SPs: inserción, consulta, métricas y autenticación

\- Datos iniciales: estados del proceso, roles y usuario admin



\---



\## \[001] — 2026-05-21 — AddFullNameToUsers

\*\*Archivo:\*\* `Migrations/001\_AddFullNameToUsers.sql`

\*\*Autor:\*\* Ruiz



\### Cambios

\- Agrega columna `FullName NVARCHAR(200) NULL` a la tabla `USERS`

\- Actualiza `SP\_USER\_GET\_BY\_USERNAME` para retornar `FullName`

\- Actualiza el usuario `admin` con nombre completo



\### Estado

✅ Integrado

