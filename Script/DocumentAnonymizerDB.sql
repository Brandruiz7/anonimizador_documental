/*****************************************************************************************
 Project    : DocumentAnonymizerDB
 Description: Document anonymization system with full traceability.
              Supports versioning, audit logging, anonymization tracking
              and error management.

              Core features:
              - Duplicate detection via file hash
              - Document versioning (ORIGINAL / ANONYMIZED)
              - Field-level anonymization tracking
              - Process status lifecycle
              - Error logging for observability

 Author     : Ruiz
 Date       : 2026
*****************************************************************************************/

USE DocumentAnonymizerDB;
GO

-------------------------------------------------------------------
-- DROPS (SAFE RE-RUN)
-- Orden: hijo -> padre para respetar FK
-------------------------------------------------------------------

IF OBJECT_ID('SP_ANONYMIZED_FIELD_INSERT',        'P') IS NOT NULL DROP PROCEDURE SP_ANONYMIZED_FIELD_INSERT;
IF OBJECT_ID('SP_DOCUMENT_VERSION_INSERT',        'P') IS NOT NULL DROP PROCEDURE SP_DOCUMENT_VERSION_INSERT;
IF OBJECT_ID('SP_DOCUMENT_PROCESS_UPDATE_STATUS', 'P') IS NOT NULL DROP PROCEDURE SP_DOCUMENT_PROCESS_UPDATE_STATUS;
IF OBJECT_ID('SP_DOCUMENT_PROCESS_INSERT',        'P') IS NOT NULL DROP PROCEDURE SP_DOCUMENT_PROCESS_INSERT;
IF OBJECT_ID('SP_DOCUMENT_UPDATE_STATUS',         'P') IS NOT NULL DROP PROCEDURE SP_DOCUMENT_UPDATE_STATUS;
IF OBJECT_ID('SP_DOCUMENT_INSERT',                'P') IS NOT NULL DROP PROCEDURE SP_DOCUMENT_INSERT;
IF OBJECT_ID('SP_DOCUMENT_GET_FULL',              'P') IS NOT NULL DROP PROCEDURE SP_DOCUMENT_GET_FULL;
IF OBJECT_ID('SP_DOCUMENT_GET_BY_HASH',           'P') IS NOT NULL DROP PROCEDURE SP_DOCUMENT_GET_BY_HASH;
IF OBJECT_ID('SP_USER_GET_BY_USERNAME',           'P') IS NOT NULL DROP PROCEDURE SP_USER_GET_BY_USERNAME;
GO

IF OBJECT_ID('ANONYMIZED_FIELDS',  'U') IS NOT NULL DROP TABLE ANONYMIZED_FIELDS;
IF OBJECT_ID('PROCESS_ERRORS',     'U') IS NOT NULL DROP TABLE PROCESS_ERRORS;
IF OBJECT_ID('DOCUMENT_VERSIONS',  'U') IS NOT NULL DROP TABLE DOCUMENT_VERSIONS;
IF OBJECT_ID('DOCUMENTS',          'U') IS NOT NULL DROP TABLE DOCUMENTS;
IF OBJECT_ID('PROCESS_STATUS',     'U') IS NOT NULL DROP TABLE PROCESS_STATUS;
IF OBJECT_ID('USERS',              'U') IS NOT NULL DROP TABLE USERS;
IF OBJECT_ID('ROLES',              'U') IS NOT NULL DROP TABLE ROLES;
GO

-------------------------------------------------------------------
-- TABLES
-------------------------------------------------------------------

-- =============================================
-- TABLE: PROCESS_STATUS
-- Catálogo de estados del documento.
--
-- 1 = UPLOADED
-- 2 = PROCESSING
-- 3 = ANONYMIZED
-- 4 = FAILED
-- =============================================
CREATE TABLE PROCESS_STATUS (
    StatusId INT          PRIMARY KEY,
    Name     NVARCHAR(50) NOT NULL
);
GO

-- =============================================
-- TABLE: DOCUMENTS
-- Entidad principal del sistema.
-- Identificada de forma única por su hash.
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
-- TABLE: DOCUMENT_VERSIONS
-- Versiones de cada documento.
-- Tipos: ORIGINAL / ANONYMIZED
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
-- TABLE: ANONYMIZED_FIELDS
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
-- TABLE: PROCESS_ERRORS
-- Registro centralizado de errores.
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
-- TABLE: ROLES
-- =============================================
CREATE TABLE ROLES (
    RoleId   INT          IDENTITY(1,1) PRIMARY KEY,
    RoleName NVARCHAR(50) NOT NULL UNIQUE
);
GO

-- =============================================
-- TABLE: USERS
-- Contraseñas almacenadas como BCrypt hash.
-- Los usuarios los crea el admin directamente en BD.
-- =============================================
CREATE TABLE USERS (
    UserId       INT            IDENTITY(1,1) PRIMARY KEY,
    Username     NVARCHAR(100)  NOT NULL UNIQUE,
    PasswordHash NVARCHAR(256)  NOT NULL,
    RoleId       INT            NOT NULL REFERENCES ROLES(RoleId),
    IsActive     BIT            NOT NULL DEFAULT 1,
    CreatedAt    DATETIME2      NOT NULL DEFAULT SYSDATETIME()
);
GO

-------------------------------------------------------------------
-- INDEXES
-------------------------------------------------------------------

CREATE UNIQUE INDEX UX_DOCUMENTS_FileHash      ON DOCUMENTS        (FileHash);
CREATE INDEX IX_DOCUMENTS_Status               ON DOCUMENTS        (CurrentStatusId);
CREATE INDEX IX_DOCUMENTS_CreatedAt            ON DOCUMENTS        (CreatedAt DESC);
CREATE INDEX IX_DOCUMENTS_Status_Created       ON DOCUMENTS        (CurrentStatusId, CreatedAt DESC);
CREATE INDEX IX_VERSIONS_DocumentId            ON DOCUMENT_VERSIONS(DocumentId);
CREATE INDEX IX_FIELDS_VersionId               ON ANONYMIZED_FIELDS(VersionId);
CREATE INDEX IX_FIELDS_Type                    ON ANONYMIZED_FIELDS(FieldType);
GO

-------------------------------------------------------------------
-- DATA: Catálogo de estados
-------------------------------------------------------------------

INSERT INTO PROCESS_STATUS (StatusId, Name) VALUES
    (1, 'UPLOADED'),
    (2, 'PROCESSING'),
    (3, 'ANONYMIZED'),
    (4, 'FAILED');
GO

-------------------------------------------------------------------
-- STORED PROCEDURES
-------------------------------------------------------------------

-- =============================================
-- SP: SP_DOCUMENT_INSERT
-- Inserta un documento nuevo. Estado inicial: UPLOADED.
-- Retorna: DocumentId generado.
-- =============================================
CREATE OR ALTER PROCEDURE SP_DOCUMENT_INSERT
(
    @OriginalFileName NVARCHAR(255),
    @ContentType      NVARCHAR(100),
    @FileSizeKB       BIGINT,
    @FileHash         NVARCHAR(256),
    @UploadedBy       NVARCHAR(100)
)
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRY
        INSERT INTO DOCUMENTS (OriginalFileName, ContentType, FileSizeKB, FileHash, UploadedBy, CurrentStatusId)
        VALUES (@OriginalFileName, @ContentType, @FileSizeKB, @FileHash, @UploadedBy, 1);

        SELECT CAST(SCOPE_IDENTITY() AS INT) AS DocumentId;
    END TRY
    BEGIN CATCH
        INSERT INTO PROCESS_ERRORS (Message, StackTrace) VALUES (ERROR_MESSAGE(), ERROR_PROCEDURE());
        THROW;
    END CATCH
END;
GO

-- =============================================
-- SP: SP_DOCUMENT_UPDATE_STATUS
-- Actualiza el estado de un documento.
-- Si estado = 3 (ANONYMIZED) → IsProcessed = 1.
-- =============================================
CREATE OR ALTER PROCEDURE SP_DOCUMENT_UPDATE_STATUS
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
        INSERT INTO PROCESS_ERRORS (DocumentId, Message, StackTrace) VALUES (@DocumentId, ERROR_MESSAGE(), ERROR_PROCEDURE());
        THROW;
    END CATCH
END;
GO

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
        INSERT INTO DOCUMENTS (OriginalFileName, ContentType, FileSizeKB, FileHash, UploadedBy, CurrentStatusId)
        VALUES (@FileName, @ContentType, @FileSizeKb, @Hash, @UploadedBy, 2);

        SELECT CAST(SCOPE_IDENTITY() AS INT);
    END TRY
    BEGIN CATCH
        INSERT INTO PROCESS_ERRORS (Message, StackTrace) VALUES (ERROR_MESSAGE(), ERROR_PROCEDURE());
        THROW;
    END CATCH
END;
GO

-- =============================================
-- SP: SP_DOCUMENT_PROCESS_UPDATE_STATUS
-- Actualiza el estado del proceso de anonimización.
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
        INSERT INTO PROCESS_ERRORS (DocumentId, Message, StackTrace) VALUES (@DocumentId, ERROR_MESSAGE(), ERROR_PROCEDURE());
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
        INSERT INTO DOCUMENT_VERSIONS (DocumentId, VersionType, FilePath, FileHash)
        VALUES (@DocumentId, @VersionType, @FilePath, @FileHash);

        SELECT CAST(SCOPE_IDENTITY() AS INT);
    END TRY
    BEGIN CATCH
        INSERT INTO PROCESS_ERRORS (DocumentId, Message, StackTrace) VALUES (@DocumentId, ERROR_MESSAGE(), ERROR_PROCEDURE());
        THROW;
    END CATCH
END;
GO

-- =============================================
-- SP: SP_ANONYMIZED_FIELD_INSERT
-- Registra un campo anonimizado para auditoría.
-- ConfidenceScore se hardcodea en 100.00 para detección REGEX.
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
        INSERT INTO ANONYMIZED_FIELDS (VersionId, FieldType, OriginalValue, AnonymizedValue, ConfidenceScore, DetectionMethod)
        VALUES (@VersionId, @FieldType, @OriginalValue, @AnonymizedValue, 100.00, @DetectionMethod);
    END TRY
    BEGIN CATCH
        INSERT INTO PROCESS_ERRORS (Message, StackTrace) VALUES (ERROR_MESSAGE(), ERROR_PROCEDURE());
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
-- de un documento completo. Útil para auditoría.
-- =============================================
CREATE OR ALTER PROCEDURE SP_DOCUMENT_GET_FULL
(
    @DocumentId INT
)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT * FROM DOCUMENTS WHERE DocumentId = @DocumentId;

    SELECT * FROM DOCUMENT_VERSIONS WHERE DocumentId = @DocumentId;

    SELECT AF.*
    FROM ANONYMIZED_FIELDS AF
    INNER JOIN DOCUMENT_VERSIONS DV ON AF.VersionId = DV.VersionId
    WHERE DV.DocumentId = @DocumentId;
END;
GO

-- =============================================
-- SP: SP_USER_GET_BY_USERNAME
-- Busca un usuario activo por username.
-- Retorna: datos del usuario + rol.
-- =============================================
CREATE OR ALTER PROCEDURE SP_USER_GET_BY_USERNAME
    @Username NVARCHAR(100)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        u.UserId,
        u.Username,
        u.PasswordHash,
        u.IsActive,
        r.RoleName
    FROM USERS u
    INNER JOIN ROLES r ON u.RoleId = r.RoleId
    WHERE u.Username = @Username;
END;
GO

-------------------------------------------------------------------
-- DATA: Roles y usuario admin inicial
-- Password: Admin123!
-------------------------------------------------------------------

INSERT INTO ROLES (RoleName) VALUES ('Admin');
INSERT INTO ROLES (RoleName) VALUES ('Operator');
GO

-- IMPORTANTE: Actualiza el hash con el generado por el endpoint
-- GET /api/auth/generate-hash?password=TuPassword
INSERT INTO USERS (Username, PasswordHash, RoleId)
VALUES (
    'admin',
    '$2a$12$cnMg268Ym4KY.s7MlnlzyO8xujqYlMFOc78prb3q796Ldci/3wMxG',
    1
);
GO

SELECT * FROM ANONYMIZED_FIELDS ORDER BY CreatedAt DESC;
SELECT * FROM DOCUMENT_VERSIONS ORDER BY GeneratedAt DESC;
SELECT * FROM DOCUMENTS ORDER BY CreatedAt DESC;