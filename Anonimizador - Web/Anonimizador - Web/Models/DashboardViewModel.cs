namespace Anonimizador___Web.Models
{
    public class DashboardViewModel
    {
        public List<DocumentSummaryViewModel> Documents { get; set; } = new();
        public MetricsViewModel Metrics { get; set; } = new();
    }

    public class DocumentSummaryViewModel
    {
        public int DocumentId { get; set; }
        public string OriginalFileName { get; set; } = string.Empty;
        public long FileSizeKB { get; set; }
        public string UploadedBy { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public string Status { get; set; } = string.Empty;
    }
}