namespace Anonimizador___Web.Models
{
    public class MetricsViewModel
    {
        public MetricsSummaryViewModel Summary { get; set; } = new();
        public List<DocumentsByMonthViewModel> ByMonth { get; set; } = new();
        public List<DocumentsByStatusViewModel> ByStatus { get; set; } = new();
        public List<DocumentsByUserViewModel> ByUser { get; set; } = new();
    }

    public class MetricsSummaryViewModel
    {
        public int TotalDocuments { get; set; }
        public int ThisMonth { get; set; }
        public int ActiveUsers { get; set; }
    }

    public class DocumentsByMonthViewModel
    {
        public string Month { get; set; } = string.Empty;
        public int MonthNumber { get; set; }
        public int Year { get; set; }
        public int Total { get; set; }
    }

    public class DocumentsByStatusViewModel
    {
        public string Status { get; set; } = string.Empty;
        public int Total { get; set; }
    }

    public class DocumentsByUserViewModel
    {
        public string Username { get; set; } = string.Empty;
        public int Total { get; set; }
    }
}