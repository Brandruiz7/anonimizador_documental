using Anonimizador___API.Application.DTOs.Auth;
using Anonimizador___API.Infrastructure.Data;
using Anonimizador___API.Interfaces.Repositories;
using Dapper;
using System.Data;

namespace Anonimizador___API.Infrastructure.Repositories
{
    /// <summary>
    /// Repositorio de acceso a datos de usuarios del sistema.
    /// </summary>
    public class UserRepository : IUserRepository
    {
        private readonly DbConnectionFactory _factory;

        /// <summary>
        /// Inicializa el repositorio con la fábrica de conexiones.
        /// </summary>
        public UserRepository(DbConnectionFactory factory)
        {
            _factory = factory;
        }

        /// <inheritdoc />
        public async Task<UserDto?> GetByUsernameAsync(string username)
        {
            using var connection = _factory.CreateConnection();

            return await connection.QueryFirstOrDefaultAsync<UserDto>(
                "SP_USER_GET_BY_USERNAME",
                new { Username = username },
                commandType: CommandType.StoredProcedure);
        }
    }
}