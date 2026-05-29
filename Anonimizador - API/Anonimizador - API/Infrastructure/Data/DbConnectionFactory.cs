using Oracle.ManagedDataAccess.Client;
using System.Data;

namespace Anonimizador___API.Infrastructure.Data
{
    /// <summary>
    /// Fábrica de conexiones a la base de datos Oracle XE 21c.
    /// Centraliza la creación de conexiones para desacoplar la configuración
    /// del resto del sistema — los repositorios no conocen el motor de BD.
    ///
    /// La cadena de conexión se lee desde la sección ConnectionStrings:DefaultConnection
    /// del appsettings.json o de las variables de entorno en producción.
    /// Formato: User Id=usuario;Password=clave;Data Source=host:puerto/servicio
    /// </summary>
    public class DbConnectionFactory
    {
        private readonly IConfiguration _config;

        /// <summary>
        /// Inicializa la fábrica con la configuración de la aplicación.
        /// </summary>
        public DbConnectionFactory(IConfiguration config)
        {
            _config = config;
        }

        /// <summary>
        /// Crea y retorna una nueva conexión a Oracle lista para usar.
        /// La conexión se retorna sin abrir — Dapper la abre automáticamente
        /// al ejecutar el primer comando.
        /// </summary>
        /// <returns>Conexión Oracle configurada con la cadena de la sección DefaultConnection.</returns>
        /// <exception cref="InvalidOperationException">
        /// Si la cadena de conexión DefaultConnection no está configurada.
        /// </exception>
        public IDbConnection CreateConnection()
        {
            var connectionString = _config.GetConnectionString("DefaultConnection");

            if (string.IsNullOrWhiteSpace(connectionString))
                throw new InvalidOperationException(
                    "La cadena de conexión 'DefaultConnection' no está configurada. " +
                    "Verificá appsettings.json o las variables de entorno.");

            return new OracleConnection(connectionString);
        }
    }
}