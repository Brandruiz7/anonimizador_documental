using Anonimizador___API.Application.DTOs.Documents;
using Anonimizador___API.Application.DTOs.Metrics;
using Anonimizador___API.Infrastructure.Data;
using Anonimizador___API.Interfaces.Repositories;
using Dapper;
using Oracle.ManagedDataAccess.Client;
using System.Data;

namespace Anonimizador___API.Infrastructure.Repositories
{
    /// <summary>
    /// Repositorio de acceso a datos de documentos, versiones y auditoría.
    /// Ejecuta procedimientos almacenados en Oracle mediante Dapper.
    ///
    /// Nota: Oracle retorna conjuntos de resultados mediante SYS_REFCURSOR —
    /// cada SP que devuelve filas requiere un parámetro OUT de tipo RefCursor.
    /// Los IDs generados se obtienen mediante parámetros OUT escalares.
    /// </summary>
    public class DocumentRepository : IDocumentRepository
    {
        private readonly DbConnectionFactory _factory;

        /// <summary>
        /// Inicializa el repositorio con la fábrica de conexiones Oracle.
        /// </summary>
        public DocumentRepository(DbConnectionFactory factory)
        {
            _factory = factory;
        }

        /// <inheritdoc />
        public async Task<int> InsertDocumentProcessAsync( string fileName, string contentType, long fileSizeKb, string hash, string uploadedBy)
        {
            using var connection = _factory.CreateConnection();
            connection.Open();

            var p = new OracleDynamicParameters();
            p.AddInput("p_FileName", fileName);
            p.AddInput("p_ContentType", contentType);
            p.AddInput("p_FileSizeKB", fileSizeKb, OracleDbType.Int64);
            p.AddInput("p_FileHash", hash);
            p.AddInput("p_UploadedBy", uploadedBy);
            p.AddOutput("p_DocumentId", OracleDbType.Int32);

            await connection.ExecuteAsync(
                "SP_DOCUMENT_PROCESS_INSERT", p,
                commandType: CommandType.StoredProcedure);

            return p.Get<int>("p_DocumentId");
        }

        /// <inheritdoc />
        public async Task<int> InsertDocumentVersionAsync( int documentId, string versionType, string fileHash)
        {
            using var connection = _factory.CreateConnection();
            connection.Open();

            var p = new OracleDynamicParameters();
            p.AddInput("p_DocumentId", documentId, OracleDbType.Int32);
            p.AddInput("p_VersionType", versionType);
            p.AddInput("p_FileHash", fileHash);
            p.AddOutput("p_VersionId", OracleDbType.Int32);

            await connection.ExecuteAsync(
                "SP_DOCUMENT_VERSION_INSERT", p,
                commandType: CommandType.StoredProcedure);

            return p.Get<int>("p_VersionId");
        }

        /// <inheritdoc />
        public async Task InsertAuditFieldAsync( int versionId, string fieldType, string originalValue, string anonymizedValue)
        {
            using var connection = _factory.CreateConnection();
            connection.Open();

            var p = new OracleDynamicParameters();
            p.AddInput("p_VersionId", versionId, OracleDbType.Int32);
            p.AddInput("p_FieldType", fieldType);
            p.AddInput("p_OriginalValue", originalValue);
            p.AddInput("p_AnonymizedValue", anonymizedValue);

            await connection.ExecuteAsync(
                "SP_ANONYMIZED_FIELD_INSERT", p,
                commandType: CommandType.StoredProcedure);
        }

        /// <inheritdoc />
        public async Task UpdateProcessStatusAsync(int documentId, int statusId)
        {
            using var connection = _factory.CreateConnection();
            connection.Open();

            var p = new OracleDynamicParameters();
            p.AddInput("p_DocumentId", documentId, OracleDbType.Int32);
            p.AddInput("p_StatusId", statusId, OracleDbType.Int32);

            await connection.ExecuteAsync(
                "SP_DOCUMENT_PROCESS_UPDATE_STATUS", p,
                commandType: CommandType.StoredProcedure);
        }

        /// <inheritdoc />
        public async Task<IEnumerable<DocumentSummaryDto>> GetAllDocumentsAsync()
        {
            using var connection = _factory.CreateConnection();
            connection.Open();

            var p = new OracleDynamicParameters();
            p.AddCursor("p_ResultSet");

            return await connection.QueryAsync<DocumentSummaryDto>(
                "SP_DOCUMENT_GET_ALL", p,
                commandType: CommandType.StoredProcedure);
        }

        /// <inheritdoc />
        public async Task<MetricsResponseDto> GetMetricsAsync()
        {
            using var connection = _factory.CreateConnection();
            connection.Open();

            // Resumen general — total, anonimizados, fallidos, tamaño
            var pSummary = new OracleDynamicParameters();
            pSummary.AddCursor("p_ResultSet");
            var summary = await connection.QueryFirstOrDefaultAsync<MetricsSummaryDto>(
                "SP_METRICS_SUMMARY", pSummary,
                commandType: CommandType.StoredProcedure);

            // Documentos agrupados por mes
            var pMonth = new OracleDynamicParameters();
            pMonth.AddCursor("p_ResultSet");
            var byMonth = await connection.QueryAsync<DocumentsByMonthDto>(
                "SP_METRICS_DOCUMENTS_BY_MONTH", pMonth,
                commandType: CommandType.StoredProcedure);

            // Documentos agrupados por estado
            var pStatus = new OracleDynamicParameters();
            pStatus.AddCursor("p_ResultSet");
            var byStatus = await connection.QueryAsync<DocumentsByStatusDto>(
                "SP_METRICS_DOCUMENTS_BY_STATUS", pStatus,
                commandType: CommandType.StoredProcedure);

            // Documentos agrupados por usuario
            var pUser = new OracleDynamicParameters();
            pUser.AddCursor("p_ResultSet");
            var byUser = await connection.QueryAsync<DocumentsByUserDto>(
                "SP_METRICS_DOCUMENTS_BY_USER", pUser,
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