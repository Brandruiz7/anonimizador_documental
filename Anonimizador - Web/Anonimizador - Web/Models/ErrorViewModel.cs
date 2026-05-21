namespace Anonimizador___Web.Models
{
    /// <summary>
    /// ViewModel para la vista de error genérico.
    /// </summary>
    public class ErrorViewModel
    {
        /// <summary>Identificador del request que generó el error.</summary>
        public string? RequestId { get; set; }

        /// <summary>Indica si debe mostrarse el RequestId en la vista.</summary>
        public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);
    }
}