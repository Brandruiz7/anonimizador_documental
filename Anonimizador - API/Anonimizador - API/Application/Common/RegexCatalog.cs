using System.Text.RegularExpressions;

namespace Anonimizador___API.Application.Common
{
    /// <summary>
    /// Catálogo centralizado de expresiones regulares para detección de datos sensibles.
    /// Todas las instancias están compiladas con <see cref="RegexOptions.Compiled"/>
    /// para mayor rendimiento — se compilan una sola vez al iniciar la aplicación.
    ///
    /// Estas expresiones son el primer motor de detección en el flujo híbrido.
    /// Sus resultados se complementan con la detección semántica de la IA.
    /// </summary>
    public static class RegexCatalog
    {
        /// <summary>
        /// Detecta nombres completos con al menos dos palabras capitalizadas.
        /// Soporta caracteres especiales del español (tildes, ñ).
        /// Limitación: puede generar falsos positivos con nombres de instituciones
        /// o inicios de oración. La IA complementa este caso.
        /// </summary>
        public static readonly Regex FullName =
            new(
                @"\b[A-ZÁÉÍÓÚÑ][a-záéíóúñ]+ [A-ZÁÉÍÓÚÑ][a-záéíóúñ]+\b",
                RegexOptions.Compiled);

        /// <summary>
        /// Detecta números de cédula costarricense en formato X-XXXX-XXXX.
        /// Cubre cédulas de 1 a 9 dígitos en el primer grupo.
        /// </summary>
        public static readonly Regex CostaRicaId =
            new(
                @"\b\d{1}-\d{4}-\d{4}\b",
                RegexOptions.Compiled);

        /// <summary>
        /// Detecta correos electrónicos en formato estándar RFC 5321.
        /// Cubre dominios de dos o más caracteres.
        /// </summary>
        public static readonly Regex Email =
            new(
                @"[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}",
                RegexOptions.Compiled);

        /// <summary>
        /// Detecta números de teléfono costarricenses en formato XXXX-XXXX.
        /// Limitación: puede coincidir con otros números de 8 dígitos con guion.
        /// La IA complementa para validar contexto semántico.
        /// </summary>
        public static readonly Regex Phone =
            new(
                @"\b\d{4}-\d{4}\b",
                RegexOptions.Compiled);
    }
}