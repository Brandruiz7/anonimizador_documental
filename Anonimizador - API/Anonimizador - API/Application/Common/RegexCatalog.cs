using System.Text.RegularExpressions;

namespace Anonimizador___API.Application.Common
{
    /// <summary>
    /// Catálogo centralizado de expresiones regulares para detección de datos sensibles.
    /// Compiladas en tiempo de inicio para mayor rendimiento.
    /// </summary>
    public static class RegexCatalog
    {
        /// <summary>
        /// Detecta nombres completos con al menos dos palabras capitalizadas.
        /// Soporta caracteres especiales del español (tildes, ñ).
        /// </summary>
        public static readonly Regex FullName =
            new(
                @"\b[A-ZÁÉÍÓÚÑ][a-záéíóúñ]+ [A-ZÁÉÍÓÚÑ][a-záéíóúñ]+\b",
                RegexOptions.Compiled);

        /// <summary>
        /// Detecta números de cédula costarricense en formato X-XXXX-XXXX.
        /// </summary>
        public static readonly Regex CostaRicaId =
            new(
                @"\b\d{1}-\d{4}-\d{4}\b",
                RegexOptions.Compiled);

        /// <summary>
        /// Detecta correos electrónicos en formato estándar.
        /// </summary>
        public static readonly Regex Email =
            new(
                @"[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}",
                RegexOptions.Compiled);

        /// <summary>
        /// Detecta números de teléfono costarricenses en formato XXXX-XXXX.
        /// </summary>
        public static readonly Regex Phone =
            new(
                @"\b\d{4}-\d{4}\b",
                RegexOptions.Compiled);
    }
}