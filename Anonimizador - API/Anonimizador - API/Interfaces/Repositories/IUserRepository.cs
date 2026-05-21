using Anonimizador___API.Application.DTOs.Auth;

namespace Anonimizador___API.Interfaces.Repositories
{
    /// <summary>
    /// Contrato para el acceso a datos de usuarios del sistema.
    /// </summary>
    public interface IUserRepository
    {
        /// <summary>
        /// Busca un usuario activo por su nombre de usuario.
        /// </summary>
        /// <param name="username">Nombre de usuario a buscar.</param>
        /// <returns>Datos del usuario si existe y está activo; null en caso contrario.</returns>
        Task<UserDto?> GetByUsernameAsync(string username);
    }
}