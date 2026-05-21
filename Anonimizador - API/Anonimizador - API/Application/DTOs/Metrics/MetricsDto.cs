namespace Anonimizador___API.Application.DTOs.Metrics
{
    /// <summary>
    /// Resumen general de métricas para las tarjetas del dashboard.
    /// </summary>
    public class MetricsSummaryDto
    {
        /// <summary>Total de documentos procesados.</summary>
        public int TotalDocuments { get; set; }

        /// <summary>Documentos procesados durante el mes actual.</summary>
        public int ThisMonth { get; set; }

        /// <summary>Cantidad de usuarios distintos que han subido documentos.</summary>
        public int ActiveUsers { get; set; }
    }

    /// <summary>
    /// Documentos procesados por mes para el gráfico de línea.
    /// </summary>
    public class DocumentsByMonthDto
    {
        /// <summary>Nombre del mes en español. Ejemplo: "ene 2026".</summary>
        public string Month { get; set; } = string.Empty;

        /// <summary>Número del mes (1-12).</summary>
        public int MonthNumber { get; set; }

        /// <summary>Año correspondiente.</summary>
        public int Year { get; set; }

        /// <summary>Total de documentos procesados ese mes.</summary>
        public int Total { get; set; }
    }

    /// <summary>
    /// Documentos agrupados por estado para el gráfico de dona.
    /// </summary>
    public class DocumentsByStatusDto
    {
        /// <summary>Nombre del estado (UPLOADED, ANONYMIZED, FAILED).</summary>
        public string Status { get; set; } = string.Empty;

        /// <summary>Total de documentos en ese estado.</summary>
        public int Total { get; set; }
    }

    /// <summary>
    /// Documentos agrupados por usuario para el gráfico de dona.
    /// </summary>
    public class DocumentsByUserDto
    {
        /// <summary>Nombre de usuario.</summary>
        public string Username { get; set; } = string.Empty;

        /// <summary>Total de documentos subidos por ese usuario.</summary>
        public int Total { get; set; }
    }

    /// <summary>
    /// Respuesta completa de métricas para el dashboard.
    /// Agrupa todos los datos necesarios para los gráficos y tarjetas.
    /// </summary>
    public class MetricsResponseDto
    {
        /// <summary>Resumen general con totales.</summary>
        public MetricsSummaryDto Summary { get; set; } = new();

        /// <summary>Documentos por mes para el gráfico de línea.</summary>
        public List<DocumentsByMonthDto> ByMonth { get; set; } = new();

        /// <summary>Documentos por estado para el gráfico de dona.</summary>
        public List<DocumentsByStatusDto> ByStatus { get; set; } = new();

        /// <summary>Documentos por usuario para el gráfico de dona.</summary>
        public List<DocumentsByUserDto> ByUser { get; set; } = new();
    }
}