namespace Anonimizador___API.Application.Common
{
    /// <summary>
    /// Represents a reconstructed text line from a PDF.
    /// </summary>
    public class PdfLineInfo
    {
        public int PageNumber { get; set; }

        public string Text { get; set; } = string.Empty;

        public List<PdfWordInfo> Words { get; set; } = new();
    }
}