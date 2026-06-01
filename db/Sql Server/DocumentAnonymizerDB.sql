/*****************************************************************************************
 Proyecto    : DocumentAnonymizerDB
 Descripción : Sistema de anonimización documental con trazabilidad completa.
               Soporta versionado, auditoría, seguimiento de campos anonimizados
               y gestión de errores.

               Funcionalidades principales:
               - Registro de documentos procesados
               - Versionado de documentos (ORIGINAL / ANONYMIZED)
               - Auditoría a nivel de campo anonimizado
               - Ciclo de vida del estado del proceso
               - Registro centralizado de errores

 Autor       : Ruiz
 Fecha       : 2026
*****************************************************************************************/

USE DocumentAnonymizerDB;
GO

-- =============================================
-- LIMPIEZA SEGURA PARA RE-EJECUCIÓN
-- Orden: hijo → padre para respetar FK
-- =============================================

IF OBJECT_ID('SP_ANONYMIZED_FIELD_INSERT',        'P') IS NOT NULL DROP PROCEDURE SP_ANONYMIZED_FIELD_INSERT;
IF OBJECT_ID('SP_DOCUMENT_VERSION_INSERT',        'P') IS NOT NULL DROP PROCEDURE SP_DOCUMENT_VERSION_INSERT;
IF OBJECT_ID('SP_DOCUMENT_PROCESS_UPDATE_STATUS', 'P') IS NOT NULL DROP PROCEDURE SP_DOCUMENT_PROCESS_UPDATE_STATUS;
IF OBJECT_ID('SP_DOCUMENT_PROCESS_INSERT',        'P') IS NOT NULL DROP PROCEDURE SP_DOCUMENT_PROCESS_INSERT;
IF OBJECT_ID('SP_DOCUMENT_GET_ALL',               'P') IS NOT NULL DROP PROCEDURE SP_DOCUMENT_GET_ALL;
IF OBJECT_ID('SP_DOCUMENT_GET_FULL',              'P') IS NOT NULL DROP PROCEDURE SP_DOCUMENT_GET_FULL;
IF OBJECT_ID('SP_DOCUMENT_GET_BY_HASH',           'P') IS NOT NULL DROP PROCEDURE SP_DOCUMENT_GET_BY_HASH;
IF OBJECT_ID('SP_USER_GET_BY_USERNAME',           'P') IS NOT NULL DROP PROCEDURE SP_USER_GET_BY_USERNAME;
IF OBJECT_ID('SP_METRICS_DOCUMENTS_BY_MONTH',     'P') IS NOT NULL DROP PROCEDURE SP_METRICS_DOCUMENTS_BY_MONTH;
IF OBJECT_ID('SP_METRICS_DOCUMENTS_BY_STATUS',    'P') IS NOT NULL DROP PROCEDURE SP_METRICS_DOCUMENTS_BY_STATUS;
IF OBJECT_ID('SP_METRICS_DOCUMENTS_BY_USER',      'P') IS NOT NULL DROP PROCEDURE SP_METRICS_DOCUMENTS_BY_USER;
IF OBJECT_ID('SP_METRICS_SUMMARY',                'P') IS NOT NULL DROP PROCEDURE SP_METRICS_SUMMARY;
GO

IF OBJECT_ID('ANONYMIZED_FIELDS',  'U') IS NOT NULL DROP TABLE ANONYMIZED_FIELDS;
IF OBJECT_ID('PROCESS_ERRORS',     'U') IS NOT NULL DROP TABLE PROCESS_ERRORS;
IF OBJECT_ID('DOCUMENT_VERSIONS',  'U') IS NOT NULL DROP TABLE DOCUMENT_VERSIONS;
IF OBJECT_ID('DOCUMENTS',          'U') IS NOT NULL DROP TABLE DOCUMENTS;
IF OBJECT_ID('PROCESS_STATUS',     'U') IS NOT NULL DROP TABLE PROCESS_STATUS;
IF OBJECT_ID('USERS',              'U') IS NOT NULL DROP TABLE USERS;
IF OBJECT_ID('ROLES',              'U') IS NOT NULL DROP TABLE ROLES;
GO

-- =============================================
-- TABLAS
-- =============================================

-- =============================================
-- TABLA: PROCESS_STATUS
-- Catálogo de estados del documento.
-- 1 = UPLOADED | 2 = PROCESSING
-- 3 = ANONYMIZED | 4 = FAILED
-- =============================================
CREATE TABLE PROCESS_STATUS (
    StatusId INT          PRIMARY KEY,
    Name     NVARCHAR(50) NOT NULL
);
GO

-- =============================================
-- TABLA: DOCUMENTS
-- Entidad principal del sistema.
-- =============================================
CREATE TABLE DOCUMENTS (
    DocumentId           INT           IDENTITY(1,1) PRIMARY KEY,
    OriginalFileName     NVARCHAR(255) NOT NULL,
    ContentType          NVARCHAR(100) NOT NULL,
    FileSizeKB           BIGINT        NOT NULL,
    FileHash             NVARCHAR(256) NOT NULL,
    UploadedBy           NVARCHAR(100) NOT NULL,
    CreatedAt            DATETIME2     NOT NULL DEFAULT SYSDATETIME(),
    IsProcessed          BIT           NOT NULL DEFAULT 0,
    ProcessingDurationMs INT           NULL,
    CurrentStatusId      INT           NOT NULL,

    CONSTRAINT FK_DOCUMENTS_STATUS
        FOREIGN KEY (CurrentStatusId) REFERENCES PROCESS_STATUS(StatusId)
);
GO

-- =============================================
-- TABLA: DOCUMENT_VERSIONS
-- Versiones de cada documento procesado.
-- Tipos válidos: ORIGINAL / ANONYMIZED
-- =============================================
CREATE TABLE DOCUMENT_VERSIONS (
    VersionId   INT           IDENTITY(1,1) PRIMARY KEY,
    DocumentId  INT           NOT NULL,
    VersionType NVARCHAR(50)  NOT NULL,
    FilePath    NVARCHAR(500) NOT NULL,
    FileHash    NVARCHAR(256) NOT NULL,
    GeneratedAt DATETIME2     NOT NULL DEFAULT SYSDATETIME(),

    CONSTRAINT FK_DOCUMENT_VERSIONS_DOCUMENTS
        FOREIGN KEY (DocumentId) REFERENCES DOCUMENTS(DocumentId),

    CONSTRAINT CK_VERSION_TYPE
        CHECK (VersionType IN ('ORIGINAL', 'ANONYMIZED'))
);
GO

-- =============================================
-- TABLA: ANONYMIZED_FIELDS
-- Registro de cada dato sensible anonimizado.
-- DetectionMethod: REGEX / AI
-- =============================================
CREATE TABLE ANONYMIZED_FIELDS (
    FieldId         INT           IDENTITY(1,1) PRIMARY KEY,
    VersionId       INT           NOT NULL,
    FieldType       NVARCHAR(100) NOT NULL,
    OriginalValue   NVARCHAR(MAX) NULL,
    AnonymizedValue NVARCHAR(MAX) NOT NULL,
    ConfidenceScore DECIMAL(5,2)  NULL,
    DetectionMethod NVARCHAR(50)  NOT NULL,
    CreatedAt       DATETIME2     NOT NULL DEFAULT SYSDATETIME(),

    CONSTRAINT FK_FIELDS_VERSION
        FOREIGN KEY (VersionId) REFERENCES DOCUMENT_VERSIONS(VersionId),

    CONSTRAINT CK_DETECTION_METHOD
        CHECK (DetectionMethod IN ('REGEX', 'AI'))
);
GO

-- =============================================
-- TABLA: PROCESS_ERRORS
-- Registro centralizado de errores del sistema.
-- =============================================
CREATE TABLE PROCESS_ERRORS (
    ErrorId    INT           IDENTITY(1,1) PRIMARY KEY,
    DocumentId INT           NULL,
    Message    NVARCHAR(MAX) NULL,
    StackTrace NVARCHAR(MAX) NULL,
    CreatedAt  DATETIME2     NOT NULL DEFAULT SYSDATETIME(),

    CONSTRAINT FK_ERRORS_DOCUMENT
        FOREIGN KEY (DocumentId) REFERENCES DOCUMENTS(DocumentId)
);
GO

-- =============================================
-- TABLA: ROLES
-- =============================================
CREATE TABLE ROLES (
    RoleId   INT          IDENTITY(1,1) PRIMARY KEY,
    RoleName NVARCHAR(50) NOT NULL UNIQUE
);
GO

-- =============================================
-- TABLA: USERS
-- Contraseñas almacenadas como BCrypt hash.
-- Los usuarios los crea el administrador directamente en BD.
-- =============================================
CREATE TABLE USERS (
    UserId       INT            IDENTITY(1,1) PRIMARY KEY,
    Username     NVARCHAR(100)  NOT NULL UNIQUE,
    PasswordHash NVARCHAR(256)  NOT NULL,
    FullName     NVARCHAR(200)  NULL,        
    RoleId       INT            NOT NULL REFERENCES ROLES(RoleId),
    IsActive     BIT            NOT NULL DEFAULT 1,
    CreatedAt    DATETIME2      NOT NULL DEFAULT SYSDATETIME()
);

-- =============================================
-- ÍNDICES
-- =============================================

CREATE INDEX IX_DOCUMENTS_Status         ON DOCUMENTS        (CurrentStatusId);
CREATE INDEX IX_DOCUMENTS_CreatedAt      ON DOCUMENTS        (CreatedAt DESC);
CREATE INDEX IX_DOCUMENTS_Status_Created ON DOCUMENTS        (CurrentStatusId, CreatedAt DESC);
CREATE INDEX IX_VERSIONS_DocumentId      ON DOCUMENT_VERSIONS(DocumentId);
CREATE INDEX IX_FIELDS_VersionId         ON ANONYMIZED_FIELDS(VersionId);
CREATE INDEX IX_FIELDS_Type              ON ANONYMIZED_FIELDS(FieldType);
GO

-- =============================================
-- DATOS: Catálogo de estados del proceso
-- =============================================

INSERT INTO PROCESS_STATUS (StatusId, Name) VALUES
    (1, 'UPLOADED'),
    (2, 'PROCESSING'),
    (3, 'ANONYMIZED'),
    (4, 'FAILED');
GO

-- =============================================
-- STORED PROCEDURES
-- =============================================

-- =============================================
-- SP: SP_DOCUMENT_PROCESS_INSERT
-- Inserta un documento en estado PROCESSING (2).
-- Retorna: DocumentId generado.
-- =============================================
CREATE OR ALTER PROCEDURE SP_DOCUMENT_PROCESS_INSERT
(
    @FileName    NVARCHAR(255),
    @ContentType NVARCHAR(100),
    @FileSizeKb  BIGINT,
    @Hash        NVARCHAR(256),
    @UploadedBy  NVARCHAR(100)
)
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRY
        INSERT INTO DOCUMENTS (
            OriginalFileName,
            ContentType,
            FileSizeKB,
            FileHash,
            UploadedBy,
            CurrentStatusId)
        VALUES (
            @FileName,
            @ContentType,
            @FileSizeKb,
            @Hash,
            @UploadedBy,
            2);

        SELECT CAST(SCOPE_IDENTITY() AS INT);
    END TRY
    BEGIN CATCH
        INSERT INTO PROCESS_ERRORS (Message, StackTrace)
        VALUES (ERROR_MESSAGE(), ERROR_PROCEDURE());
        THROW;
    END CATCH
END;
GO

-- =============================================
-- SP: SP_DOCUMENT_PROCESS_UPDATE_STATUS
-- Actualiza el estado del proceso de anonimización.
-- Si estado = 3 (ANONYMIZED) → IsProcessed = 1.
-- =============================================
CREATE OR ALTER PROCEDURE SP_DOCUMENT_PROCESS_UPDATE_STATUS
(
    @DocumentId INT,
    @StatusId   INT
)
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRY
        UPDATE DOCUMENTS
        SET CurrentStatusId = @StatusId,
            IsProcessed     = CASE WHEN @StatusId = 3 THEN 1 ELSE 0 END
        WHERE DocumentId = @DocumentId;
    END TRY
    BEGIN CATCH
        INSERT INTO PROCESS_ERRORS (DocumentId, Message, StackTrace)
        VALUES (@DocumentId, ERROR_MESSAGE(), ERROR_PROCEDURE());
        THROW;
    END CATCH
END;
GO

-- =============================================
-- SP: SP_DOCUMENT_VERSION_INSERT
-- Registra una versión del documento (ORIGINAL o ANONYMIZED).
-- Retorna: VersionId generado.
-- =============================================
CREATE OR ALTER PROCEDURE SP_DOCUMENT_VERSION_INSERT
(
    @DocumentId  INT,
    @VersionType NVARCHAR(50),
    @FilePath    NVARCHAR(500),
    @FileHash    NVARCHAR(256)
)
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRY
        INSERT INTO DOCUMENT_VERSIONS (
            DocumentId,
            VersionType,
            FilePath,
            FileHash)
        VALUES (
            @DocumentId,
            @VersionType,
            @FilePath,
            @FileHash);

        SELECT CAST(SCOPE_IDENTITY() AS INT);
    END TRY
    BEGIN CATCH
        INSERT INTO PROCESS_ERRORS (DocumentId, Message, StackTrace)
        VALUES (@DocumentId, ERROR_MESSAGE(), ERROR_PROCEDURE());
        THROW;
    END CATCH
END;
GO

-- =============================================
-- SP: SP_ANONYMIZED_FIELD_INSERT
-- Registra un campo anonimizado para auditoría.
-- ConfidenceScore se hardcodea en 100.00.
-- =============================================
CREATE OR ALTER PROCEDURE SP_ANONYMIZED_FIELD_INSERT
(
    @VersionId       INT,
    @FieldType       NVARCHAR(100),
    @OriginalValue   NVARCHAR(MAX),
    @AnonymizedValue NVARCHAR(MAX),
    @DetectionMethod NVARCHAR(50)
)
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRY
        INSERT INTO ANONYMIZED_FIELDS (
            VersionId,
            FieldType,
            OriginalValue,
            AnonymizedValue,
            ConfidenceScore,
            DetectionMethod)
        VALUES (
            @VersionId,
            @FieldType,
            @OriginalValue,
            @AnonymizedValue,
            100.00,
            @DetectionMethod);
    END TRY
    BEGIN CATCH
        INSERT INTO PROCESS_ERRORS (Message, StackTrace)
        VALUES (ERROR_MESSAGE(), ERROR_PROCEDURE());
        THROW;
    END CATCH
END;
GO

-- =============================================
-- SP: SP_DOCUMENT_GET_BY_HASH
-- Busca un documento por su hash SHA256.
-- Retorna: DocumentId si existe.
-- =============================================
CREATE OR ALTER PROCEDURE SP_DOCUMENT_GET_BY_HASH
    @FileHash NVARCHAR(256)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP 1 DocumentId FROM DOCUMENTS WHERE FileHash = @FileHash;
END;
GO

-- =============================================
-- SP: SP_DOCUMENT_GET_FULL
-- Retorna metadata, versiones y campos anonimizados
-- de un documento. Útil para auditoría detallada.
-- =============================================
CREATE OR ALTER PROCEDURE SP_DOCUMENT_GET_FULL
(
    @DocumentId INT
)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT * FROM DOCUMENTS       WHERE DocumentId = @DocumentId;
    SELECT * FROM DOCUMENT_VERSIONS WHERE DocumentId = @DocumentId;

    SELECT AF.*
    FROM ANONYMIZED_FIELDS AF
    INNER JOIN DOCUMENT_VERSIONS DV ON AF.VersionId = DV.VersionId
    WHERE DV.DocumentId = @DocumentId;
END;
GO

-- =============================================
-- SP: SP_USER_GET_BY_USERNAME
-- Busca un usuario activo por nombre de usuario.
-- Retorna: datos del usuario y su rol.
-- =============================================
CREATE OR ALTER PROCEDURE SP_USER_GET_BY_USERNAME
    @Username NVARCHAR(100)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        u.UserId,
        u.Username,
        u.FullName,
        u.PasswordHash,
        u.IsActive,
        r.RoleName
    FROM USERS u
    INNER JOIN ROLES r ON u.RoleId = r.RoleId
    WHERE u.Username = @Username;
END;
GO

-- =============================================
-- SP: SP_DOCUMENT_GET_ALL
-- Retorna el historial de documentos procesados
-- con metadata básica para el dashboard.
-- =============================================
CREATE OR ALTER PROCEDURE SP_DOCUMENT_GET_ALL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        d.DocumentId,
        d.OriginalFileName,
        d.FileSizeKB,
        d.UploadedBy,
        d.CreatedAt,
        ps.Name AS Status
    FROM DOCUMENTS d
    INNER JOIN PROCESS_STATUS ps ON d.CurrentStatusId = ps.StatusId
    ORDER BY d.CreatedAt DESC;
END;
GO

-- =============================================
-- SP: SP_METRICS_DOCUMENTS_BY_MONTH
-- Documentos procesados por mes (últimos 6 meses).
-- =============================================
CREATE OR ALTER PROCEDURE SP_METRICS_DOCUMENTS_BY_MONTH
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        FORMAT(CreatedAt, 'MMM yyyy', 'es-CR') AS Month,
        YEAR(CreatedAt)                         AS Year,
        MONTH(CreatedAt)                        AS MonthNumber,
        COUNT(*)                                AS Total
    FROM DOCUMENTS
    WHERE CreatedAt >= DATEADD(MONTH, -5, DATEFROMPARTS(YEAR(GETDATE()), MONTH(GETDATE()), 1))
    GROUP BY
        FORMAT(CreatedAt, 'MMM yyyy', 'es-CR'),
        YEAR(CreatedAt),
        MONTH(CreatedAt)
    ORDER BY Year, MonthNumber;
END;
GO

-- =============================================
-- SP: SP_METRICS_DOCUMENTS_BY_STATUS
-- Documentos agrupados por estado.
-- =============================================
CREATE OR ALTER PROCEDURE SP_METRICS_DOCUMENTS_BY_STATUS
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        ps.Name             AS Status,
        COUNT(d.DocumentId) AS Total
    FROM PROCESS_STATUS ps
    LEFT JOIN DOCUMENTS d ON d.CurrentStatusId = ps.StatusId
    GROUP BY ps.StatusId, ps.Name
    ORDER BY ps.StatusId;
END;
GO

-- =============================================
-- SP: SP_METRICS_DOCUMENTS_BY_USER
-- Documentos agrupados por usuario.
-- =============================================
CREATE OR ALTER PROCEDURE SP_METRICS_DOCUMENTS_BY_USER
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        UploadedBy AS Username,
        COUNT(*)   AS Total
    FROM DOCUMENTS
    GROUP BY UploadedBy
    ORDER BY Total DESC;
END;
GO

-- =============================================
-- SP: SP_METRICS_SUMMARY
-- Resumen general: total de documentos,
-- procesados este mes y usuarios activos.
-- =============================================
CREATE OR ALTER PROCEDURE SP_METRICS_SUMMARY
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        COUNT(*) AS TotalDocuments,
        SUM(CASE
            WHEN MONTH(CreatedAt) = MONTH(GETDATE())
             AND YEAR(CreatedAt)  = YEAR(GETDATE())
            THEN 1 ELSE 0 END) AS ThisMonth,
        COUNT(DISTINCT UploadedBy) AS ActiveUsers
    FROM DOCUMENTS;
END;
GO

-- =============================================
-- DATOS: Roles y usuario administrador inicial
-- Contraseña por defecto: Admin123!
-- IMPORTANTE: Cambiar en producción
-- =============================================

INSERT INTO ROLES (RoleName) VALUES ('Admin');
INSERT INTO ROLES (RoleName) VALUES ('Operator');
GO

INSERT INTO USERS (Username, PasswordHash, RoleId)
VALUES (
    'admin',
    '$2a$12$cnMg268Ym4KY.s7MlnlzyO8xujqYlMFOc78prb3q796Ldci/3wMxG',
    1
);
GO