using Anonimizador___API.Application.DTOs;

namespace Anonimizador___API.Interfaces.Repositories
{
    /// <summary>
    /// Contrato para acceso a datos de usuarios.
    /// </summary>
    public interface IUserRepository
    {
        Task<UserDto?> GetByUsernameAsync(string username);
    }
}