using Anonimizador___API.Application.DTOs.Documents;
using Anonimizador___API.Application.DTOs.Metrics;
using Anonimizador___API.Infrastructure.Data;
using Anonimizador___API.Interfaces.Repositories;
using Dapper;
using System.Data;

namespace Anonimizador___API.Infrastructure.Repositories
{
    /// <summary>
    /// Repositorio de acceso a datos de documentos, versiones y auditoría.
    /// Ejecuta procedimientos almacenados en SQL Server mediante Dapper.
    /// </summary>
    public class DocumentRepository : IDocumentRepository
    {
        private readonly DbConnectionFactory _factory;

        /// <summary>
        /// Inicializa el repositorio con la fábrica de conexiones.
        /// </summary>
        public DocumentRepository(DbConnectionFactory factory)
        {
            _factory = factory;
        }

        /// <inheritdoc />
        public async Task<int> InsertDocumentProcessAsync(
            string fileName,
            string contentType,
            long fileSizeKb,
            string hash,
            string uploadedBy)
        {
            using var connection = _factory.CreateConnection();

            return await connection.ExecuteScalarAsync<int>(
                "SP_DOCUMENT_PROCESS_INSERT",
                new
                {
                    FileName = fileName,
                    ContentType = contentType,
                    FileSizeKb = fileSizeKb,
                    Hash = hash,
                    UploadedBy = uploadedBy
                },
                commandType: CommandType.StoredProcedure);
        }

        /// <inheritdoc />
        public async Task<int> InsertDocumentVersionAsync(
            int documentId,
            string versionType,
            string fileHash)
        {
            using var connection = _factory.CreateConnection();

            return await connection.ExecuteScalarAsync<int>(
                "SP_DOCUMENT_VERSION_INSERT",
                new
                {
                    DocumentId = documentId,
                    VersionType = versionType,
                    FilePath = "IN_MEMORY",
                    FileHash = fileHash
                },
                commandType: CommandType.StoredProcedure);
        }

        /// <inheritdoc />
        public async Task InsertAuditFieldAsync(
            int versionId,
            string fieldType,
            string originalValue,
            string anonymizedValue)
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
                },
                commandType: CommandType.StoredProcedure);
        }

        /// <inheritdoc />
        public async Task UpdateProcessStatusAsync(int documentId, int statusId)
        {
            using var connection = _factory.CreateConnection();

            await connection.ExecuteAsync(
                "SP_DOCUMENT_PROCESS_UPDATE_STATUS",
                new
                {
                    DocumentId = documentId,
                    StatusId = statusId
                },
                commandType: CommandType.StoredProcedure);
        }

        /// <inheritdoc />
        public async Task<IEnumerable<DocumentSummaryDto>> GetAllDocumentsAsync()
        {
            using var connection = _factory.CreateConnection();

            return await connection.QueryAsync<DocumentSummaryDto>(
                "SP_DOCUMENT_GET_ALL",
                commandType: CommandType.StoredProcedure);
        }

        /// <inheritdoc />
        public async Task<MetricsResponseDto> GetMetricsAsync()
        {
            using var connection = _factory.CreateConnection();

            var summary = await connection.QueryFirstOrDefaultAsync<MetricsSummaryDto>(
                "SP_METRICS_SUMMARY",
                commandType: CommandType.StoredProcedure);

            var byMonth = await connection.QueryAsync<DocumentsByMonthDto>(
                "SP_METRICS_DOCUMENTS_BY_MONTH",
                commandType: CommandType.StoredProcedure);

            var byStatus = await connection.QueryAsync<DocumentsByStatusDto>(
                "SP_METRICS_DOCUMENTS_BY_STATUS",
                commandType: CommandType.StoredProcedure);

            var byUser = await connection.QueryAsync<DocumentsByUserDto>(
                "SP_METRICS_DOCUMENTS_BY_USER",
                commandType: CommandType.StoredProcedure);

            return new MetricsResponseDto
            {
                Summary = summary ?? new MetricsSummaryDto(),
                ByMonth = byMonth.ToList(),
                ByStatus = byStatus.ToList(),
                ByUser = byUser.ToList()
            };
        }
    }
}