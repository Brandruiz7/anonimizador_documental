using Microsoft.Data.SqlClient;
using System.Data;

namespace Anonimizador___API.Infrastructure.Data
{
    /// <summary>
    /// Fábrica de conexiones a la base de datos SQL Server.
    /// Centraliza la creación de conexiones para desacoplar
    /// la configuración del resto del sistema.
    /// </summary>
    public class DbConnectionFactory
    {
        private readonly IConfiguration _config;

        /// <summary>
        /// Inicializa la fábrica con la configuración de la aplicación.
        /// </summary>
        /// <param name="config">Configuración general (appsettings.json).</param>
        public DbConnectionFactory(IConfiguration config)
        {
            _config = config;
        }

        /// <summary>
        /// Crea una nueva conexión a la base de datos.
        /// La conexión se retorna sin abrir — Dapper la maneja automáticamente.
        /// </summary>
        /// <returns>Instancia de conexión a SQL Server lista para usar.</returns>
        /// <exception cref="InvalidOperationException">
        /// Se lanza si la cadena de conexión no está configurada en appsettings.json.
        /// </exception>
        public IDbConnection CreateConnection()
        {
            var connectionString = _config.GetConnectionString("DefaultConnection");

            if (string.IsNullOrWhiteSpace(connectionString))
                throw new InvalidOperationException(
                    "La cadena de conexión 'DefaultConnection' no está configurada.");

            return new SqlConnection(connectionString);
        }
    }
}