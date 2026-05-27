using Oracle.ManagedDataAccess.Client;
using System.Data;

namespace Anonimizador___API.Infrastructure.Data
{
    /// <summary>
    /// Fábrica de conexiones a la base de datos Oracle.
    /// Centraliza la creación de conexiones para desacoplar
    /// la configuración del resto del sistema.
    /// </summary>
    public class DbConnectionFactory
    {
        private readonly IConfiguration _config;

        public DbConnectionFactory(IConfiguration config)
        {
            _config = config;
        }

        /// <summary>
        /// Crea una nueva conexión a Oracle.
        /// La conexión se retorna sin abrir — Dapper la maneja automáticamente.
        /// </summary>
        public IDbConnection CreateConnection()
        {
            var connectionString = _config.GetConnectionString("DefaultConnection");

            if (string.IsNullOrWhiteSpace(connectionString))
                throw new InvalidOperationException(
                    "La cadena de conexión 'DefaultConnection' no está configurada.");

            return new OracleConnection(connectionString);
        }
    }
}