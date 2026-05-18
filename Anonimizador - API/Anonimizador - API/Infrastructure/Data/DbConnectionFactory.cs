using Microsoft.Data.SqlClient;
using System.Data;

namespace Anonimizador___API.Infrastructure.Data
{
    /// <summary>
    /// Fábrica de conexiones a la base de datos.
    /// 
    /// Esta clase centraliza la creación de conexiones SQL,
    /// permitiendo desacoplar la configuración del resto del sistema.
    /// 
    /// </summary>
    public class DbConnectionFactory
    {
        private readonly IConfiguration _config;

        /// <summary>
        /// Constructor que recibe la configuración de la aplicación.
        /// </summary>
        /// <param name="config">Configuración general (appsettings.json)</param>
        public DbConnectionFactory(IConfiguration config)
        {
            _config = config;
        }

        /// <summary>
        /// Crea una nueva conexión a la base de datos.
        /// 
        /// Nota:
        /// La conexión se retorna sin abrir. El consumidor es responsable
        /// de abrirla y cerrarla (Dapper lo maneja automáticamente).
        /// </summary>
        /// <returns>Instancia de conexión a SQL Server</returns>
        /// <exception cref="InvalidOperationException">
        /// Se lanza si la cadena de conexión no está configurada.
        /// </exception>
        public IDbConnection CreateConnection()
        {
            var connectionString = _config.GetConnectionString("DefaultConnection");

            if (string.IsNullOrWhiteSpace(connectionString))
                throw new InvalidOperationException("Database connection string is not configured.");

            return new SqlConnection(connectionString);
        }
    }
}