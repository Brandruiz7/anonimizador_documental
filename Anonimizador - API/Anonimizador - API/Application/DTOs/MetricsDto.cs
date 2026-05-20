namespace Anonimizador___API.Application.DTOs
{
    /// <summary>
    /// Resumen general de métricas para las tarjetas del dashboard.
    /// </summary>
    public class MetricsSummaryDto
    {
        public int TotalDocuments { get; set; }
        public int ThisMonth { get; set; }
        public int ActiveUsers { get; set; }
    }

    /// <summary>
    /// Documentos procesados por mes para el gráfico de línea.
    /// </summary>
    public class DocumentsByMonthDto
    {
        public string Month { get; set; } = string.Empty;
        public int MonthNumber { get; set; }
        public int Year { get; set; }
        public int Total { get; set; }
    }

    /// <summary>
    /// Documentos agrupados por estado para el gráfico de dona.
    /// </summary>
    public class DocumentsByStatusDto
    {
        public string Status { get; set; } = string.Empty;
        public int Total { get; set; }
    }

    /// <summary>
    /// Documentos agrupados por usuario para el gráfico de dona.
    /// </summary>
    public class DocumentsByUserDto
    {
        public string Username { get; set; } = string.Empty;
        public int Total { get; set; }
    }

    /// <summary>
    /// Respuesta completa de métricas para el dashboard.
    /// </summary>
    public class MetricsResponseDto
    {
        public MetricsSummaryDto Summary { get; set; } = new();
        public List<DocumentsByMonthDto> ByMonth { get; set; } = new();
        public List<DocumentsByStatusDto> ByStatus { get; set; } = new();
        public List<DocumentsByUserDto> ByUser { get; set; } = new();
    }
}