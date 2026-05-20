using Anonimizador___API.Application.DTOs;
using Anonimizador___API.Infrastructure.Data;
using Anonimizador___API.Interfaces.Repositories;
using Dapper;
using System.Data;

namespace Anonimizador___API.Infrastructure.Repositories
{
    /// <summary>
    /// Repositorio encargado del acceso a datos de documentos.
    /// 
    /// Esta clase actúa como intermediario entre la capa de aplicación
    /// y la base de datos, ejecutando procedimientos almacenados
    /// relacionados con documentos y sus versiones.
    /// 
    /// </summary>
    public class DocumentRepository : IDocumentRepository
    {
        private readonly DbConnectionFactory _factory;

        /// <summary>
        /// Constructor del repositorio.
        /// </summary>
        /// <param name="factory">Fábrica de conexiones a base de datos</param>
        public DocumentRepository(DbConnectionFactory factory)
        {
            _factory = factory;
        }

        /// <summary>
        /// Inserta un nuevo documento en la base de datos.
        /// 
        /// Este método ejecuta el procedimiento almacenado SP_DOCUMENT_INSERT,
        /// el cual registra los metadatos principales del archivo.
        /// </summary>
        /// <param name="name">Nombre original del archivo</param>
        /// <param name="type">Tipo MIME del archivo</param>
        /// <param name="size">Tamaño en KB</param>
        /// <param name="hash">Hash SHA256 del archivo</param>
        /// <param name="user">Usuario que realiza la carga</param>
        /// <returns>Identificador del documento generado</returns>
        public async Task<int> InsertDocumentAsync(string name, string type, long size, string hash, string user)
        {
            using var connection = _factory.CreateConnection();

            var result = await connection.ExecuteScalarAsync<int>(
                "SP_DOCUMENT_INSERT",
                new
                {
                    OriginalFileName = name,
                    ContentType = type,
                    FileSizeKB = size,
                    FileHash = hash,
                    UploadedBy = user
                },
                commandType: CommandType.StoredProcedure
            );

            return result;
        }

        /// <summary>
        /// Inserta una nueva versión de un documento.
        /// 
        /// Las versiones permiten manejar múltiples estados del mismo archivo,
        /// como:
        /// - ORIGINAL
        /// - ANONYMIZED
        /// </summary>
        /// <param name="documentId">Identificador del documento</param>
        /// <param name="versionType">Tipo de versión (ej: ORIGINAL, ANONYMIZED)</param>
        /// <param name="path">Ruta física del archivo</param>
        /// <param name="hash">Hash del archivo</param>
        /// <returns>Identificador de la versión generada</returns>
        public async Task<int> InsertVersionAsync(int documentId, string versionType, string path, string hash)
        {
            using var connection = _factory.CreateConnection();

            var result = await connection.ExecuteScalarAsync<int>(
                "SP_DOCUMENT_VERSION_INSERT",
                new
                {
                    DocumentId = documentId,
                    VersionType = versionType,
                    FilePath = path,
                    FileHash = hash
                },
                commandType: CommandType.StoredProcedure
            );

            return result;
        }

        /// <summary>
        /// Obtiene un documento existente basado en su hash.
        /// 
        /// Se utiliza principalmente para detectar duplicados,
        /// evitando almacenar múltiples veces el mismo archivo.
        /// </summary>
        /// <param name="hash">Hash SHA256 del archivo</param>
        /// <returns>
        /// ID del documento si existe; 
        /// null si no se encuentra en la base de datos.
        /// </returns>
        public async Task<int?> GetDocumentByHashAsync(string hash)
        {
            using var connection = _factory.CreateConnection();

            var result = await connection.ExecuteScalarAsync<int?>(
                "SP_DOCUMENT_GET_BY_HASH",
                new { FileHash = hash },
                commandType: CommandType.StoredProcedure
            );

            return result;
        }

        /// <summary>
        /// Registers a document anonymization process.
        /// </summary>
        /// <param name="fileName">Original file name.</param>
        /// <param name="contentType">Document content type.</param>
        /// <param name="fileSizeKb">Document size in KB.</param>
        /// <param name="hash">SHA256 document hash.</param>
        /// <param name="uploadedBy">User who uploaded the file.</param>
        /// <returns>Generated process identifier.</returns>
        public async Task<int> InsertDocumentProcessAsync(string fileName, string contentType, long fileSizeKb, string hash, string uploadedBy)
        {
            using var connection = _factory.CreateConnection();

            var parameters = new
            {
                FileName = fileName,
                ContentType = contentType,
                FileSizeKb = fileSizeKb,
                Hash = hash,
                UploadedBy = uploadedBy
            };

            return await connection.ExecuteScalarAsync<int>(
                "SP_DOCUMENT_PROCESS_INSERT",
                parameters,
                commandType: System.Data.CommandType.StoredProcedure);
        }

        /// <summary>
        /// Updates document processing status.
        /// </summary>
        /// <param name="documentId">Identificador del documento.</param>
        /// <param name="statusId">Identificador del estado.</param>
        public async Task UpdateProcessStatusAsync(int documentId, int statusId)
        {
            using var connection = _factory.CreateConnection();

            var parameters = new
            {
                DocumentId = documentId,
                StatusId = statusId
            };

            await connection.ExecuteAsync(
                "SP_DOCUMENT_PROCESS_UPDATE_STATUS",
                parameters,
                commandType: System.Data.CommandType.StoredProcedure);
        }

        public async Task<int> InsertDocumentVersionAsync( int documentId, string versionType, string fileHash)
        {
            using var connection = _factory.CreateConnection();

            var result = await connection.ExecuteScalarAsync<int>(
                "SP_DOCUMENT_VERSION_INSERT",
                new
                {
                    DocumentId = documentId,
                    VersionType = versionType,
                    FilePath = "IN_MEMORY",
                    FileHash = fileHash
                },
                commandType: CommandType.StoredProcedure);

            // LOG TEMPORAL
            Console.WriteLine($"InsertDocumentVersionAsync result: {result}");

            return result;
        }

        public async Task InsertAuditFieldAsync(int versionId, string fieldType, string originalValue, string anonymizedValue)
        {
            using var connection = _factory.CreateConnection();

            await connection.ExecuteAsync(
                "SP_ANONYMIZED_FIELD_INSERT",
                new
                {
                    VersionId = versionId,
                    FieldType = fieldType,
                    OriginalValue = originalValue,
                    AnonymizedValue = anonymizedValue,
                    DetectionMethod = "REGEX"
                    //ConfidenceScore = (decimal?)100.00
                },
                commandType: CommandType.StoredProcedure);
        }

        /// <summary>
        /// Retorna el historial de documentos procesados.
        /// </summary>
        public async Task<IEnumerable<DocumentSummaryDto>> GetAllDocumentsAsync()
        {
            using var connection = _factory.CreateConnection();

            return await connection.QueryAsync<DocumentSummaryDto>(
                "SP_DOCUMENT_GET_ALL",
                commandType: CommandType.StoredProcedure);
        }

        /// <summary>
        /// Retorna todas las métricas para el dashboard.
        /// </summary>
        public async Task<MetricsResponseDto> GetMetricsAsync()
        {
            using var connection = _factory.CreateConnection();

            // Ejecutamos los 4 SPs en paralelo para mayor rendimiento
            var summaryTask = connection.QueryFirstOrDefaultAsync<MetricsSummaryDto>(
                "SP_METRICS_SUMMARY",
                commandType: CommandType.StoredProcedure);

            var byMonthTask = connection.QueryAsync<DocumentsByMonthDto>(
                "SP_METRICS_DOCUMENTS_BY_MONTH",
                commandType: CommandType.StoredProcedure);

            var byStatusTask = connection.QueryAsync<DocumentsByStatusDto>(
                "SP_METRICS_DOCUMENTS_BY_STATUS",
                commandType: CommandType.StoredProcedure);

            var byUserTask = connection.QueryAsync<DocumentsByUserDto>(
                "SP_METRICS_DOCUMENTS_BY_USER",
                commandType: CommandType.StoredProcedure);

            await Task.WhenAll(summaryTask, byMonthTask, byStatusTask, byUserTask);

            return new MetricsResponseDto
            {
                Summary = await summaryTask ?? new MetricsSummaryDto(),
                ByMonth = (await byMonthTask).ToList(),
                ByStatus = (await byStatusTask).ToList(),
                ByUser = (await byUserTask).ToList()
            };
        }
    }
}