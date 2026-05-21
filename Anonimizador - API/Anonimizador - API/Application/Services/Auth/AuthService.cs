using Anonimizador___API.Application.DTOs.Auth;
using Anonimizador___API.Interfaces.Repositories;
using Anonimizador___API.Interfaces.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Anonimizador___API.Application.Services.Auth
{
    /// <summary>
    /// Servicio de autenticación.
    /// Valida credenciales contra la base de datos y genera tokens JWT.
    /// </summary>
    public class AuthService : IAuthService
    {
        private readonly IUserRepository _userRepository;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AuthService> _logger;

        /// <summary>
        /// Inicializa el servicio de autenticación con sus dependencias.
        /// </summary>
        public AuthService(
            IUserRepository userRepository,
            IConfiguration configuration,
            ILogger<AuthService> logger)
        {
            _userRepository = userRepository;
            _configuration = configuration;
            _logger = logger;
        }

        /// <inheritdoc />
        public async Task<LoginResponseDto> LoginAsync(LoginRequestDto request)
        {
            _logger.LogInformation(
                "Intento de login para usuario: {Username}", request.Username);

            // 1. Buscar usuario activo en BD
            var user = await _userRepository.GetByUsernameAsync(request.Username);

            if (user == null || !user.IsActive)
                throw new UnauthorizedAccessException("Credenciales inválidas.");

            // 2. Verificar contraseña con BCrypt
            var passwordValid = BCrypt.Net.BCrypt.Verify(
                request.Password,
                user.PasswordHash);

            if (!passwordValid)
                throw new UnauthorizedAccessException("Credenciales inválidas.");

            _logger.LogInformation(
                "Login exitoso: {Username} | Rol: {Role}",
                user.Username,
                user.RoleName);

            // 3. Generar y retornar el token JWT
            return GenerateToken(user);
        }

        /// <summary>
        /// Genera un token JWT firmado con los claims del usuario autenticado.
        /// </summary>
        /// <param name="user">Datos del usuario autenticado.</param>
        /// <returns>Respuesta con el token JWT y sus metadatos.</returns>
        private LoginResponseDto GenerateToken(UserDto user)
        {
            var key = _configuration["Jwt:Key"]!;
            var issuer = _configuration["Jwt:Issuer"]!;
            var audience = _configuration["Jwt:Audience"]!;
            var hours = int.Parse(_configuration["Jwt:ExpirationHours"]!);

            var securityKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(key));

            var credentials = new SigningCredentials(
                securityKey,
                SecurityAlgorithms.HmacSha256);

            var expiresAt = DateTime.UtcNow.AddHours(hours);

            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub,        user.UserId.ToString()),
                new Claim(JwtRegisteredClaimNames.UniqueName, user.Username),
                new Claim(ClaimTypes.Role,                    user.RoleName),
                new Claim(ClaimTypes.GivenName,               user.FullName ?? string.Empty),
                new Claim(JwtRegisteredClaimNames.Jti,        Guid.NewGuid().ToString())
            };

            var token = new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                claims: claims,
                expires: expiresAt,
                signingCredentials: credentials);

            return new LoginResponseDto
            {
                Token = new JwtSecurityTokenHandler().WriteToken(token),
                Username = user.Username,
                FullName = user.FullName ?? string.Empty,
                Role = user.RoleName,
                ExpiresAt = expiresAt
            };
        }
    }
}