namespace Anonimizador___API.Application.DTOs.Metrics
{
    /// <summary>
    /// Resumen general de métricas para las tarjetas del dashboard.
    /// Retornado por SP_METRICS_SUMMARY.
    /// </summary>
    public class MetricsSummaryDto
    {
        /// <summary>Total de documentos registrados en el sistema.</summary>
        public int TotalDocuments { get; set; }

        /// <summary>Documentos procesados durante el mes actual.</summary>
        public int ThisMonth { get; set; }

        /// <summary>Cantidad de usuarios distintos que han subido documentos.</summary>
        public int ActiveUsers { get; set; }
    }

    /// <summary>
    /// Documentos procesados por mes para el gráfico de línea del dashboard.
    /// Retornado por SP_METRICS_DOCUMENTS_BY_MONTH.
    /// </summary>
    public class DocumentsByMonthDto
    {
        /// <summary>Mes en formato YYYY-MM. Ejemplo: "2026-05".</summary>
        public string Month { get; set; } = string.Empty;

        /// <summary>Número del mes (1-12).</summary>
        public int MonthNumber { get; set; }

        /// <summary>Año correspondiente.</summary>
        public int Year { get; set; }

        /// <summary>Total de documentos procesados ese mes.</summary>
        public int Total { get; set; }
    }

    /// <summary>
    /// Documentos agrupados por estado para el gráfico de dona del dashboard.
    /// Retornado por SP_METRICS_DOCUMENTS_BY_STATUS.
    /// </summary>
    public class DocumentsByStatusDto
    {
        /// <summary>
        /// Nombre del estado.
        /// Valores posibles: UPLOADED, PROCESSING, ANONYMIZED, FAILED.
        /// </summary>
        public string Status { get; set; } = string.Empty;

        /// <summary>Total de documentos en ese estado.</summary>
        public int Total { get; set; }
    }

    /// <summary>
    /// Documentos agrupados por usuario para el gráfico de dona del dashboard.
    /// Retornado por SP_METRICS_DOCUMENTS_BY_USER.
    /// </summary>
    public class DocumentsByUserDto
    {
        /// <summary>
        /// Nombre de usuario que subió los documentos.
        /// Puede ser "Desconocido" si el campo UploadedBy estaba vacío en BD.
        /// </summary>
        public string Usuario { get; set; } = string.Empty;

        /// <summary>Total de documentos subidos por ese usuario.</summary>
        public int Total { get; set; }
    }

    /// <summary>
    /// Respuesta completa de métricas para el dashboard.
    /// Agrupa todos los datos necesarios para gráficos y tarjetas en una sola respuesta.
    /// Construida en DocumentRepository.GetMetricsAsync() a partir de cuatro SPs separados.
    /// </summary>
    public class MetricsResponseDto
    {
        /// <summary>Resumen general con totales para las tarjetas superiores.</summary>
        public MetricsSummaryDto Summary { get; set; } = new();

        /// <summary>Documentos por mes para el gráfico de línea.</summary>
        public List<DocumentsByMonthDto> ByMonth { get; set; } = new();

        /// <summary>Documentos por estado para el gráfico de dona.</summary>
        public List<DocumentsByStatusDto> ByStatus { get; set; } = new();

        /// <summary>Documentos por usuario para el gráfico de dona.</summary>
        public List<DocumentsByUserDto> ByUser { get; set; } = new();
    }
}