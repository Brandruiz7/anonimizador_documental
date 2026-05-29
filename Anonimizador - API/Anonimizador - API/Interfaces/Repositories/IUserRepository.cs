using Anonimizador___API.Application.DTOs.Auth;

namespace Anonimizador___API.Interfaces.Repositories
{
    /// <summary>
    /// Contrato para el acceso a datos de usuarios del sistema.
    /// Su implementación concreta es <see cref="Infrastructure.Repositories.UserRepository"/>.
    ///
    /// La creación y gestión de usuarios se realiza directamente en BD por el administrador.
    /// Este repositorio solo expone consultas de solo lectura para autenticación.
    /// </summary>
    public interface IUserRepository
    {
        /// <summary>
        /// Busca un usuario por su nombre de usuario.
        /// </summary>
        /// <param name="username">Nombre de usuario a buscar.</param>
        /// <returns>
        /// Datos del usuario incluyendo el hash BCrypt si existe;
        /// null si el usuario no existe o está inactivo.
        /// </returns>
        Task<UserDto?> GetByUsernameAsync(string username);
    }
}