/*******************************************************************************
 Script     : DocumentAnonymizerDB_Oracle.sql
 Descripción: Crea el esquema completo del sistema de anonimización documental:
              tablas, secuencias, triggers, índices y stored procedures.
 Autor      : Ruiz
 Fecha      : 2026

 INSTRUCCIONES DE EJECUCIÓN:
 1. Conectarse en SQL Developer como: anonimizador / TuPassword@XEPDB1
 2. Ejecutar este script completo (F5)
*******************************************************************************/


-- =============================================
-- LIMPIEZA SEGURA — PROCEDURES primero
-- =============================================
BEGIN EXECUTE IMMEDIATE 'DROP PROCEDURE SP_DOCUMENT_PROCESS_INSERT';        EXCEPTION WHEN OTHERS THEN NULL; END;
/
BEGIN EXECUTE IMMEDIATE 'DROP PROCEDURE SP_DOCUMENT_VERSION_INSERT';        EXCEPTION WHEN OTHERS THEN NULL; END;
/
BEGIN EXECUTE IMMEDIATE 'DROP PROCEDURE SP_ANONYMIZED_FIELD_INSERT';        EXCEPTION WHEN OTHERS THEN NULL; END;
/
BEGIN EXECUTE IMMEDIATE 'DROP PROCEDURE SP_DOCUMENT_PROCESS_UPDATE_STATUS'; EXCEPTION WHEN OTHERS THEN NULL; END;
/
BEGIN EXECUTE IMMEDIATE 'DROP PROCEDURE SP_USER_GET_BY_USERNAME';           EXCEPTION WHEN OTHERS THEN NULL; END;
/
BEGIN EXECUTE IMMEDIATE 'DROP PROCEDURE SP_DOCUMENT_GET_ALL';               EXCEPTION WHEN OTHERS THEN NULL; END;
/
BEGIN EXECUTE IMMEDIATE 'DROP PROCEDURE SP_DOCUMENT_GET_FULL';              EXCEPTION WHEN OTHERS THEN NULL; END;
/
BEGIN EXECUTE IMMEDIATE 'DROP PROCEDURE SP_DOCUMENT_GET_BY_HASH';          EXCEPTION WHEN OTHERS THEN NULL; END;
/
BEGIN EXECUTE IMMEDIATE 'DROP PROCEDURE SP_METRICS_SUMMARY';               EXCEPTION WHEN OTHERS THEN NULL; END;
/
BEGIN EXECUTE IMMEDIATE 'DROP PROCEDURE SP_METRICS_DOCUMENTS_BY_STATUS';   EXCEPTION WHEN OTHERS THEN NULL; END;
/
BEGIN EXECUTE IMMEDIATE 'DROP PROCEDURE SP_METRICS_DOCUMENTS_BY_MONTH';    EXCEPTION WHEN OTHERS THEN NULL; END;
/
BEGIN EXECUTE IMMEDIATE 'DROP PROCEDURE SP_METRICS_DOCUMENTS_BY_USER';     EXCEPTION WHEN OTHERS THEN NULL; END;
/


-- =============================================
-- LIMPIEZA SEGURA — TRIGGERS
-- =============================================
BEGIN EXECUTE IMMEDIATE 'DROP TRIGGER TRG_DOCUMENTS_ID';          EXCEPTION WHEN OTHERS THEN NULL; END;
/
BEGIN EXECUTE IMMEDIATE 'DROP TRIGGER TRG_DOCUMENT_VERSIONS_ID';  EXCEPTION WHEN OTHERS THEN NULL; END;
/
BEGIN EXECUTE IMMEDIATE 'DROP TRIGGER TRG_ANONYMIZED_FIELDS_ID';  EXCEPTION WHEN OTHERS THEN NULL; END;
/
BEGIN EXECUTE IMMEDIATE 'DROP TRIGGER TRG_PROCESS_ERRORS_ID';     EXCEPTION WHEN OTHERS THEN NULL; END;
/
BEGIN EXECUTE IMMEDIATE 'DROP TRIGGER TRG_ROLES_ID';              EXCEPTION WHEN OTHERS THEN NULL; END;
/
BEGIN EXECUTE IMMEDIATE 'DROP TRIGGER TRG_USERS_ID';              EXCEPTION WHEN OTHERS THEN NULL; END;
/


-- =============================================
-- LIMPIEZA SEGURA — TABLAS (hijo → padre)
-- =============================================
BEGIN EXECUTE IMMEDIATE 'DROP TABLE ANONYMIZED_FIELDS CASCADE CONSTRAINTS';  EXCEPTION WHEN OTHERS THEN NULL; END;
/
BEGIN EXECUTE IMMEDIATE 'DROP TABLE PROCESS_ERRORS CASCADE CONSTRAINTS';     EXCEPTION WHEN OTHERS THEN NULL; END;
/
BEGIN EXECUTE IMMEDIATE 'DROP TABLE DOCUMENT_VERSIONS CASCADE CONSTRAINTS';  EXCEPTION WHEN OTHERS THEN NULL; END;
/
BEGIN EXECUTE IMMEDIATE 'DROP TABLE DOCUMENTS CASCADE CONSTRAINTS';          EXCEPTION WHEN OTHERS THEN NULL; END;
/
BEGIN EXECUTE IMMEDIATE 'DROP TABLE USERS CASCADE CONSTRAINTS';              EXCEPTION WHEN OTHERS THEN NULL; END;
/
BEGIN EXECUTE IMMEDIATE 'DROP TABLE ROLES CASCADE CONSTRAINTS';              EXCEPTION WHEN OTHERS THEN NULL; END;
/
BEGIN EXECUTE IMMEDIATE 'DROP TABLE PROCESS_STATUS CASCADE CONSTRAINTS';     EXCEPTION WHEN OTHERS THEN NULL; END;
/


-- =============================================
-- LIMPIEZA SEGURA — SECUENCIAS
-- =============================================
BEGIN EXECUTE IMMEDIATE 'DROP SEQUENCE SEQ_DOCUMENTS';          EXCEPTION WHEN OTHERS THEN NULL; END;
/
BEGIN EXECUTE IMMEDIATE 'DROP SEQUENCE SEQ_DOCUMENT_VERSIONS';  EXCEPTION WHEN OTHERS THEN NULL; END;
/
BEGIN EXECUTE IMMEDIATE 'DROP SEQUENCE SEQ_ANONYMIZED_FIELDS';  EXCEPTION WHEN OTHERS THEN NULL; END;
/
BEGIN EXECUTE IMMEDIATE 'DROP SEQUENCE SEQ_PROCESS_ERRORS';     EXCEPTION WHEN OTHERS THEN NULL; END;
/
BEGIN EXECUTE IMMEDIATE 'DROP SEQUENCE SEQ_ROLES';              EXCEPTION WHEN OTHERS THEN NULL; END;
/
BEGIN EXECUTE IMMEDIATE 'DROP SEQUENCE SEQ_USERS';              EXCEPTION WHEN OTHERS THEN NULL; END;
/


-- =============================================
-- TABLAS
-- =============================================

-- Catálogo de estados del proceso de anonimización
CREATE TABLE PROCESS_STATUS (
    StatusId   NUMBER(10)   NOT NULL,  -- Identificador del estado
    Name       VARCHAR2(50) NOT NULL,  -- Nombre del estado: UPLOADED, PROCESSING, ANONYMIZED, FAILED
    CONSTRAINT PK_PROCESS_STATUS      PRIMARY KEY (StatusId),
    CONSTRAINT UQ_PROCESS_STATUS_NAME UNIQUE      (Name)
);

-- Registro de documentos subidos al sistema
CREATE TABLE DOCUMENTS (
    DocumentId       NUMBER(10)    NOT NULL,
    OriginalFileName VARCHAR2(260) NOT NULL,
    ContentType      VARCHAR2(100) NOT NULL,  -- MIME type del archivo
    FileSizeKB       NUMBER(19)    NOT NULL,
    FileHash         VARCHAR2(64)  NOT NULL,  -- Hash SHA-256 del archivo (64 chars hex)
    UploadedBy       VARCHAR2(100) NULL,
    CurrentStatusId  NUMBER(10)    NOT NULL,
    IsProcessed      NUMBER(1)     DEFAULT 0 NOT NULL,  -- 0 = pendiente, 1 = anonimizado
    CreatedAt        TIMESTAMP     DEFAULT SYSTIMESTAMP NOT NULL,
    UpdatedAt        TIMESTAMP     DEFAULT SYSTIMESTAMP NOT NULL,
    CONSTRAINT PK_DOCUMENTS        PRIMARY KEY (DocumentId),
    CONSTRAINT FK_DOCUMENTS_STATUS FOREIGN KEY (CurrentStatusId) REFERENCES PROCESS_STATUS(StatusId)
);

-- Versiones de un documento (ORIGINAL y ANONYMIZED)
CREATE TABLE DOCUMENT_VERSIONS (
    VersionId   NUMBER(10)   NOT NULL,
    DocumentId  NUMBER(10)   NOT NULL,
    VersionType VARCHAR2(50) NOT NULL,  -- 'ORIGINAL' o 'ANONYMIZED'
    FileHash    VARCHAR2(64) NOT NULL,  -- Hash SHA-256 de esta versión
    CreatedAt   TIMESTAMP    DEFAULT SYSTIMESTAMP NOT NULL,
    CONSTRAINT PK_DOCUMENT_VERSIONS PRIMARY KEY (VersionId),
    CONSTRAINT FK_VERSIONS_DOCUMENT FOREIGN KEY (DocumentId) REFERENCES DOCUMENTS(DocumentId)
);

-- Campos sensibles detectados y anonimizados por versión de documento
CREATE TABLE ANONYMIZED_FIELDS (
    FieldId         NUMBER(10)    NOT NULL,
    VersionId       NUMBER(10)    NOT NULL,
    FieldType       VARCHAR2(100) NOT NULL,  -- Categoría del campo: NOMBRE, CEDULA, TELEFONO, etc.
    OriginalValue   VARCHAR2(500) NOT NULL,  -- Valor original encontrado en el documento
    AnonymizedValue VARCHAR2(100) NOT NULL,  -- Etiqueta de reemplazo asignada
    DetectionMethod VARCHAR2(50)  DEFAULT 'MANUAL' NOT NULL,  -- 'MANUAL' o 'IA'
    CreatedAt       TIMESTAMP     DEFAULT SYSTIMESTAMP NOT NULL,
    CONSTRAINT PK_ANONYMIZED_FIELDS PRIMARY KEY (FieldId),
    CONSTRAINT FK_FIELDS_VERSION    FOREIGN KEY (VersionId) REFERENCES DOCUMENT_VERSIONS(VersionId)
);

-- Errores ocurridos durante el procesamiento de documentos
CREATE TABLE PROCESS_ERRORS (
    ErrorId    NUMBER(10) NOT NULL,
    DocumentId NUMBER(10) NULL,  -- NULL si el error ocurrió antes de registrar el documento
    Message    CLOB       NULL,
    StackTrace CLOB       NULL,
    CreatedAt  TIMESTAMP  DEFAULT SYSTIMESTAMP NOT NULL,
    CONSTRAINT PK_PROCESS_ERRORS  PRIMARY KEY (ErrorId),
    CONSTRAINT FK_ERRORS_DOCUMENT FOREIGN KEY (DocumentId) REFERENCES DOCUMENTS(DocumentId)
);

-- Catálogo de roles del sistema
CREATE TABLE ROLES (
    RoleId   NUMBER(10)   NOT NULL,  -- Admin, Operator
    RoleName VARCHAR2(50) NOT NULL,
    CONSTRAINT PK_ROLES      PRIMARY KEY (RoleId),
    CONSTRAINT UQ_ROLES_NAME UNIQUE      (RoleName)
);

-- Usuarios del sistema con autenticación BCrypt y control de acceso por rol
CREATE TABLE USERS (
    UserId       NUMBER(10)    NOT NULL,
    Username     VARCHAR2(100) NOT NULL,
    PasswordHash VARCHAR2(256) NOT NULL,  -- Hash BCrypt de la contraseña
    FullName     VARCHAR2(200) NULL,
    RoleId       NUMBER(10)    NOT NULL,
    IsActive     NUMBER(1)     DEFAULT 1 NOT NULL,  -- 0 = deshabilitado, 1 = activo
    CreatedAt    TIMESTAMP     DEFAULT SYSTIMESTAMP NOT NULL,
    CONSTRAINT PK_USERS          PRIMARY KEY (UserId),
    CONSTRAINT UQ_USERS_USERNAME UNIQUE      (Username),
    CONSTRAINT FK_USERS_ROLES    FOREIGN KEY (RoleId) REFERENCES ROLES(RoleId)
);


-- =============================================
-- SECUENCIAS
-- =============================================
CREATE SEQUENCE SEQ_DOCUMENTS         START WITH 1 INCREMENT BY 1 NOCACHE NOCYCLE;
CREATE SEQUENCE SEQ_DOCUMENT_VERSIONS START WITH 1 INCREMENT BY 1 NOCACHE NOCYCLE;
CREATE SEQUENCE SEQ_ANONYMIZED_FIELDS START WITH 1 INCREMENT BY 1 NOCACHE NOCYCLE;
CREATE SEQUENCE SEQ_PROCESS_ERRORS    START WITH 1 INCREMENT BY 1 NOCACHE NOCYCLE;
CREATE SEQUENCE SEQ_ROLES             START WITH 1 INCREMENT BY 1 NOCACHE NOCYCLE;
CREATE SEQUENCE SEQ_USERS             START WITH 1 INCREMENT BY 1 NOCACHE NOCYCLE;


-- =============================================
-- TRIGGERS (auto-increment)
-- =============================================

-- Asigna el siguiente valor de SEQ_DOCUMENTS antes de cada INSERT en DOCUMENTS
CREATE OR REPLACE TRIGGER TRG_DOCUMENTS_ID
    BEFORE INSERT ON DOCUMENTS FOR EACH ROW
BEGIN
    IF :NEW.DocumentId IS NULL THEN
        SELECT SEQ_DOCUMENTS.NEXTVAL INTO :NEW.DocumentId FROM DUAL;
    END IF;
END;
/

-- Asigna el siguiente valor de SEQ_DOCUMENT_VERSIONS antes de cada INSERT en DOCUMENT_VERSIONS
CREATE OR REPLACE TRIGGER TRG_DOCUMENT_VERSIONS_ID
    BEFORE INSERT ON DOCUMENT_VERSIONS FOR EACH ROW
BEGIN
    IF :NEW.VersionId IS NULL THEN
        SELECT SEQ_DOCUMENT_VERSIONS.NEXTVAL INTO :NEW.VersionId FROM DUAL;
    END IF;
END;
/

-- Asigna el siguiente valor de SEQ_ANONYMIZED_FIELDS antes de cada INSERT en ANONYMIZED_FIELDS
CREATE OR REPLACE TRIGGER TRG_ANONYMIZED_FIELDS_ID
    BEFORE INSERT ON ANONYMIZED_FIELDS FOR EACH ROW
BEGIN
    IF :NEW.FieldId IS NULL THEN
        SELECT SEQ_ANONYMIZED_FIELDS.NEXTVAL INTO :NEW.FieldId FROM DUAL;
    END IF;
END;
/

-- Asigna el siguiente valor de SEQ_PROCESS_ERRORS antes de cada INSERT en PROCESS_ERRORS
CREATE OR REPLACE TRIGGER TRG_PROCESS_ERRORS_ID
    BEFORE INSERT ON PROCESS_ERRORS FOR EACH ROW
BEGIN
    IF :NEW.ErrorId IS NULL THEN
        SELECT SEQ_PROCESS_ERRORS.NEXTVAL INTO :NEW.ErrorId FROM DUAL;
    END IF;
END;
/

-- Asigna el siguiente valor de SEQ_ROLES antes de cada INSERT en ROLES
CREATE OR REPLACE TRIGGER TRG_ROLES_ID
    BEFORE INSERT ON ROLES FOR EACH ROW
BEGIN
    IF :NEW.RoleId IS NULL THEN
        SELECT SEQ_ROLES.NEXTVAL INTO :NEW.RoleId FROM DUAL;
    END IF;
END;
/

-- Asigna el siguiente valor de SEQ_USERS antes de cada INSERT en USERS
CREATE OR REPLACE TRIGGER TRG_USERS_ID
    BEFORE INSERT ON USERS FOR EACH ROW
BEGIN
    IF :NEW.UserId IS NULL THEN
        SELECT SEQ_USERS.NEXTVAL INTO :NEW.UserId FROM DUAL;
    END IF;
END;
/


-- =============================================
-- ÍNDICES
-- =============================================
CREATE INDEX IX_DOCUMENTS_Status         ON DOCUMENTS         (CurrentStatusId);
CREATE INDEX IX_DOCUMENTS_CreatedAt      ON DOCUMENTS         (CreatedAt DESC);
CREATE INDEX IX_DOCUMENTS_Status_Created ON DOCUMENTS         (CurrentStatusId, CreatedAt DESC);
CREATE INDEX IX_VERSIONS_DocumentId      ON DOCUMENT_VERSIONS (DocumentId);
CREATE INDEX IX_FIELDS_VersionId         ON ANONYMIZED_FIELDS (VersionId);
CREATE INDEX IX_FIELDS_Type              ON ANONYMIZED_FIELDS (FieldType);


-- =============================================
-- DATOS INICIALES
-- =============================================
INSERT INTO PROCESS_STATUS (StatusId, Name) VALUES (1, 'UPLOADED');
INSERT INTO PROCESS_STATUS (StatusId, Name) VALUES (2, 'PROCESSING');
INSERT INTO PROCESS_STATUS (StatusId, Name) VALUES (3, 'ANONYMIZED');
INSERT INTO PROCESS_STATUS (StatusId, Name) VALUES (4, 'FAILED');

INSERT INTO ROLES (RoleName) VALUES ('Admin');
INSERT INTO ROLES (RoleName) VALUES ('Operator');

-- ⚠️  Reemplazar el hash antes de ejecutar
-- Generarlo con: GET /api/auth/generate-hash?password=TuPassword
INSERT INTO USERS (Username, PasswordHash, FullName, RoleId, IsActive)
VALUES (
    'admin',
    '$2a$12$cnMg268Ym4KY.s7MlnlzyO8xujqYlMFOc78prb3q796Ldci/3wMxG',
    'Brandon José Ruiz Miranda',
    (SELECT RoleId FROM ROLES WHERE RoleName = 'Admin'),
    1
);

COMMIT;


-- =============================================
-- STORED PROCEDURES
-- =============================================

/*
 * SP_DOCUMENT_PROCESS_INSERT
 * Registra un nuevo documento en estado PROCESSING (StatusId = 2).
 *
 * IN  p_FileName    VARCHAR2  Nombre original del archivo
 * IN  p_ContentType VARCHAR2  MIME type del archivo
 * IN  p_FileSizeKB  NUMBER    Tamaño del archivo en kilobytes
 * IN  p_FileHash    VARCHAR2  Hash SHA-256 del archivo
 * IN  p_UploadedBy  VARCHAR2  Username del usuario que realizó la subida
 * OUT p_DocumentId  NUMBER    ID asignado al nuevo documento
 */
CREATE OR REPLACE PROCEDURE SP_DOCUMENT_PROCESS_INSERT (
    p_FileName    IN  VARCHAR2,
    p_ContentType IN  VARCHAR2,
    p_FileSizeKB  IN  NUMBER,
    p_FileHash    IN  VARCHAR2,
    p_UploadedBy  IN  VARCHAR2,
    p_DocumentId  OUT NUMBER
)
AS
BEGIN
    INSERT INTO DOCUMENTS (
        OriginalFileName, ContentType, FileSizeKB,
        FileHash, UploadedBy, CurrentStatusId, IsProcessed
    ) VALUES (
        p_FileName, p_ContentType, p_FileSizeKB,
        p_FileHash, p_UploadedBy, 2, 0
    ) RETURNING DocumentId INTO p_DocumentId;
END;
/

/*
 * SP_DOCUMENT_VERSION_INSERT
 * Registra una versión de un documento.
 *
 * IN  p_DocumentId  NUMBER    ID del documento al que pertenece la versión
 * IN  p_VersionType VARCHAR2  Tipo de versión: 'ORIGINAL' o 'ANONYMIZED'
 * IN  p_FileHash    VARCHAR2  Hash SHA-256 de esta versión del archivo
 * OUT p_VersionId   NUMBER    ID asignado a la nueva versión
 */
CREATE OR REPLACE PROCEDURE SP_DOCUMENT_VERSION_INSERT (
    p_DocumentId  IN  NUMBER,
    p_VersionType IN  VARCHAR2,
    p_FileHash    IN  VARCHAR2,
    p_VersionId   OUT NUMBER
)
AS
BEGIN
    INSERT INTO DOCUMENT_VERSIONS (DocumentId, VersionType, FileHash)
    VALUES (p_DocumentId, p_VersionType, p_FileHash)
    RETURNING VersionId INTO p_VersionId;
END;
/

/*
 * SP_ANONYMIZED_FIELD_INSERT
 * Registra un campo sensible detectado y anonimizado en una versión de documento.
 *
 * IN  p_VersionId       NUMBER    ID de la versión a la que pertenece el campo
 * IN  p_FieldType       VARCHAR2  Categoría del campo: NOMBRE, CEDULA, TELEFONO, etc.
 * IN  p_OriginalValue   VARCHAR2  Valor original encontrado en el documento
 * IN  p_AnonymizedValue VARCHAR2  Etiqueta de reemplazo asignada
 */
CREATE OR REPLACE PROCEDURE SP_ANONYMIZED_FIELD_INSERT (
    p_VersionId       IN NUMBER,
    p_FieldType       IN VARCHAR2,
    p_OriginalValue   IN VARCHAR2,
    p_AnonymizedValue IN VARCHAR2
)
AS
BEGIN
    INSERT INTO ANONYMIZED_FIELDS (
        VersionId, FieldType, OriginalValue, AnonymizedValue
    ) VALUES (
        p_VersionId, p_FieldType, p_OriginalValue, p_AnonymizedValue
    );
END;
/

/*
 * SP_DOCUMENT_PROCESS_UPDATE_STATUS
 * Actualiza el estado de un documento y sincroniza IsProcessed.
 * Establece IsProcessed = 1 cuando StatusId = 3 (ANONYMIZED), 0 en cualquier otro caso.
 *
 * IN  p_DocumentId  NUMBER  ID del documento a actualizar
 * IN  p_StatusId    NUMBER  Nuevo estado (ver tabla PROCESS_STATUS)
 */
CREATE OR REPLACE PROCEDURE SP_DOCUMENT_PROCESS_UPDATE_STATUS (
    p_DocumentId IN NUMBER,
    p_StatusId   IN NUMBER
)
AS
BEGIN
    UPDATE DOCUMENTS
    SET CurrentStatusId = p_StatusId,
        IsProcessed     = CASE WHEN p_StatusId = 3 THEN 1 ELSE 0 END,
        UpdatedAt       = SYSTIMESTAMP
    WHERE DocumentId = p_DocumentId;
END;
/

/*
 * SP_USER_GET_BY_USERNAME
 * Recupera los datos de un usuario por nombre de usuario.
 * La comparación es case-insensitive mediante UPPER() en ambos lados.
 *
 * IN  p_Username   VARCHAR2      Username a buscar
 * OUT p_ResultSet  SYS_REFCURSOR Cursor con los siguientes campos:
 *   UserId       NUMBER    ID del usuario
 *   Username     VARCHAR2  Username almacenado
 *   PasswordHash VARCHAR2  Hash BCrypt de la contraseña
 *   IsActive     NUMBER    Estado de la cuenta: 1 = activa, 0 = deshabilitada
 *   FullName     VARCHAR2  Nombre completo del usuario
 *   RoleName     VARCHAR2  Nombre del rol asignado
 */
CREATE OR REPLACE PROCEDURE SP_USER_GET_BY_USERNAME (
    p_Username  IN  VARCHAR2,
    p_ResultSet OUT SYS_REFCURSOR
)
AS
BEGIN
    OPEN p_ResultSet FOR
        SELECT
            u.UserId,
            u.Username,
            u.PasswordHash,
            u.IsActive,
            u.FullName,
            r.RoleName
        FROM USERS u
        INNER JOIN ROLES r ON u.RoleId = r.RoleId
        WHERE UPPER(u.Username) = UPPER(p_Username);
END;
/

/*
 * SP_DOCUMENT_GET_ALL
 * Retorna todos los documentos del sistema con su estado actual.
 * Ordenado por CreatedAt DESC.
 *
 * OUT p_ResultSet  SYS_REFCURSOR  Cursor con los siguientes campos:
 *   DocumentId        NUMBER     ID del documento
 *   OriginalFileName  VARCHAR2   Nombre original del archivo
 *   ContentType       VARCHAR2   MIME type
 *   FileSizeKB        NUMBER     Tamaño en kilobytes
 *   UploadedBy        VARCHAR2   Username del subidor
 *   Status            VARCHAR2   Nombre del estado actual
 *   CreatedAt         TIMESTAMP  Fecha de creación
 *   UpdatedAt         TIMESTAMP  Fecha de última actualización
 */
CREATE OR REPLACE PROCEDURE SP_DOCUMENT_GET_ALL (
    p_ResultSet OUT SYS_REFCURSOR
)
AS
BEGIN
    OPEN p_ResultSet FOR
        SELECT
            d.DocumentId,
            d.OriginalFileName,
            d.ContentType,
            d.FileSizeKB,
            d.UploadedBy,
            s.Name AS Status,
            d.CreatedAt,
            d.UpdatedAt
        FROM DOCUMENTS d
        INNER JOIN PROCESS_STATUS s ON d.CurrentStatusId = s.StatusId
        ORDER BY d.CreatedAt DESC;
END;
/

/*
 * SP_DOCUMENT_GET_FULL
 * Retorna el detalle completo de un documento en tres cursores.
 *
 * IN  p_DocumentId  NUMBER        ID del documento a consultar
 * OUT p_Doc         SYS_REFCURSOR Todos los campos de DOCUMENTS más StatusName
 * OUT p_Versions    SYS_REFCURSOR Versiones del documento ordenadas por CreatedAt
 * OUT p_Fields      SYS_REFCURSOR Campos anonimizados de todas las versiones,
 *                                 ordenados por CreatedAt
 */
CREATE OR REPLACE PROCEDURE SP_DOCUMENT_GET_FULL (
    p_DocumentId IN  NUMBER,
    p_Doc        OUT SYS_REFCURSOR,
    p_Versions   OUT SYS_REFCURSOR,
    p_Fields     OUT SYS_REFCURSOR
)
AS
BEGIN
    OPEN p_Doc FOR
        SELECT d.*, s.Name AS StatusName
        FROM DOCUMENTS d
        INNER JOIN PROCESS_STATUS s ON d.CurrentStatusId = s.StatusId
        WHERE d.DocumentId = p_DocumentId;

    OPEN p_Versions FOR
        SELECT * FROM DOCUMENT_VERSIONS
        WHERE DocumentId = p_DocumentId
        ORDER BY CreatedAt;

    OPEN p_Fields FOR
        SELECT f.*
        FROM ANONYMIZED_FIELDS f
        INNER JOIN DOCUMENT_VERSIONS v ON f.VersionId = v.VersionId
        WHERE v.DocumentId = p_DocumentId
        ORDER BY f.CreatedAt;
END;
/

/*
 * SP_DOCUMENT_GET_BY_HASH
 * Busca un documento por su hash SHA-256.
 *
 * IN  p_Hash      VARCHAR2      Hash SHA-256 del archivo (64 chars hex)
 * OUT p_ResultSet SYS_REFCURSOR Todos los campos de DOCUMENTS más StatusName.
 *                               Retorna cero filas si el documento no existe.
 */
CREATE OR REPLACE PROCEDURE SP_DOCUMENT_GET_BY_HASH (
    p_Hash      IN  VARCHAR2,
    p_ResultSet OUT SYS_REFCURSOR
)
AS
BEGIN
    OPEN p_ResultSet FOR
        SELECT d.*, s.Name AS StatusName
        FROM DOCUMENTS d
        INNER JOIN PROCESS_STATUS s ON d.CurrentStatusId = s.StatusId
        WHERE d.FileHash = p_Hash;
END;
/

/*
 * SP_METRICS_SUMMARY
 * Retorna los indicadores globales del sistema en una sola fila.
 *
 * OUT p_ResultSet  SYS_REFCURSOR  Una fila con:
 *   TotalDocuments  NUMBER  Total de documentos registrados
 *   TotalAnonymized NUMBER  Documentos con StatusId = 3 (ANONYMIZED)
 *   TotalFailed     NUMBER  Documentos con StatusId = 4 (FAILED)
 *   TotalSizeKB     NUMBER  Suma del tamaño de todos los documentos en KB
 */
CREATE OR REPLACE PROCEDURE SP_METRICS_SUMMARY (
    p_ResultSet OUT SYS_REFCURSOR
)
AS
BEGIN
    OPEN p_ResultSet FOR
        SELECT
            COUNT(*)                                               AS TotalDocuments,
            SUM(CASE WHEN CurrentStatusId = 3 THEN 1 ELSE 0 END)  AS TotalAnonymized,
            SUM(CASE WHEN CurrentStatusId = 4 THEN 1 ELSE 0 END)  AS TotalFailed,
            COALESCE(SUM(FileSizeKB), 0)                           AS TotalSizeKB
        FROM DOCUMENTS;
END;
/

/*
 * SP_METRICS_DOCUMENTS_BY_STATUS
 * Retorna el conteo de documentos agrupado por estado.
 * Incluye todos los estados aunque no tengan documentos asociados.
 *
 * OUT p_ResultSet  SYS_REFCURSOR  Una fila por estado:
 *   Status  VARCHAR2  Nombre del estado
 *   Total   NUMBER    Cantidad de documentos en ese estado
 */
CREATE OR REPLACE PROCEDURE SP_METRICS_DOCUMENTS_BY_STATUS (
    p_ResultSet OUT SYS_REFCURSOR
)
AS
BEGIN
    OPEN p_ResultSet FOR
        SELECT s.Name AS Status, COUNT(d.DocumentId) AS Total
        FROM PROCESS_STATUS s
        LEFT JOIN DOCUMENTS d ON d.CurrentStatusId = s.StatusId
        GROUP BY s.Name
        ORDER BY s.Name;
END;
/

/*
 * SP_METRICS_DOCUMENTS_BY_MONTH
 * Retorna el conteo de documentos agrupado por mes de creación.
 * Ordenado del mes más reciente al más antiguo.
 *
 * OUT p_ResultSet  SYS_REFCURSOR  Una fila por mes con actividad:
 *   Month  VARCHAR2  Período en formato 'YYYY-MM'
 *   Total  NUMBER    Cantidad de documentos creados en ese mes
 */
CREATE OR REPLACE PROCEDURE SP_METRICS_DOCUMENTS_BY_MONTH (
    p_ResultSet OUT SYS_REFCURSOR
)
AS
BEGIN
    OPEN p_ResultSet FOR
        SELECT
            TO_CHAR(CreatedAt, 'YYYY-MM') AS Month,
            COUNT(*)                       AS Total
        FROM DOCUMENTS
        GROUP BY TO_CHAR(CreatedAt, 'YYYY-MM')
        ORDER BY Month DESC;
END;
/

/*
 * SP_METRICS_DOCUMENTS_BY_USER
 * Retorna el conteo de documentos agrupado por usuario subidor.
 * Ordenado de mayor a menor volumen.
 *
 * OUT p_ResultSet  SYS_REFCURSOR  Una fila por usuario:
 *   Usuario  VARCHAR2  Username del subidor; 'Desconocido' si UploadedBy es NULL
 *   Total    NUMBER    Cantidad de documentos subidos
 */
CREATE OR REPLACE PROCEDURE SP_METRICS_DOCUMENTS_BY_USER (
    p_ResultSet OUT SYS_REFCURSOR
)
AS
BEGIN
    OPEN p_ResultSet FOR
        SELECT
            COALESCE(UploadedBy, 'Desconocido') AS Usuario,
            COUNT(*)                             AS Total
        FROM DOCUMENTS
        GROUP BY UploadedBy
        ORDER BY Total DESC;
END;
/

COMMIT;


-- =============================================
-- VERIFICACIÓN FINAL
-- =============================================
SELECT 'PROCESS_STATUS'    AS Tabla, COUNT(*) AS Registros FROM PROCESS_STATUS   UNION ALL
SELECT 'ROLES',                       COUNT(*)              FROM ROLES             UNION ALL
SELECT 'USERS',                       COUNT(*)              FROM USERS             UNION ALL
SELECT 'DOCUMENTS',                   COUNT(*)              FROM DOCUMENTS         UNION ALL
SELECT 'DOCUMENT_VERSIONS',           COUNT(*)              FROM DOCUMENT_VERSIONS UNION ALL
SELECT 'ANONYMIZED_FIELDS',           COUNT(*)              FROM ANONYMIZED_FIELDS;