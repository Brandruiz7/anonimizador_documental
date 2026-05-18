using Dapper;
using Anonimizador___API.Application.DTOs;
using Anonimizador___API.Infrastructure.Data;
using Anonimizador___API.Interfaces.Repositories;
using System.Data;

namespace Anonimizador___API.Infrastructure.Repositories
{
    /// <summary>
    /// Acceso a datos de usuarios.
    /// </summary>
    public class UserRepository : IUserRepository
    {
        private readonly DbConnectionFactory _factory;

        public UserRepository(DbConnectionFactory factory)
        {
            _factory = factory;
        }

        /// <summary>
        /// Busca un usuario por su username.
        /// Retorna null si no existe.
        /// </summary>
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