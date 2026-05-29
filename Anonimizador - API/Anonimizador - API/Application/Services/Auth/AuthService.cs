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
    /// Valida credenciales contra la base de datos y genera tokens JWT firmados con HMAC-SHA256.
    /// 
    /// Flujo de autenticación:
    /// 1. Busca el usuario activo en BD por nombre de usuario
    /// 2. Verifica la contraseña contra el hash BCrypt almacenado
    /// 3. Genera un token JWT con claims de identidad y rol
    /// </summary>
    public class AuthService : IAuthService
    {
        private readonly IUserRepository _userRepository;
        private readonly ILogger<AuthService> _logger;

        // Configuración JWT leída una sola vez en el constructor
        private readonly string _jwtKey;
        private readonly string _jwtIssuer;
        private readonly string _jwtAudience;
        private readonly int _jwtExpirationHours;

        /// <summary>
        /// Inicializa el servicio y valida que la configuración JWT esté completa.
        /// Lanza <see cref="InvalidOperationException"/> si falta algún valor crítico.
        /// </summary>
        public AuthService(
            IUserRepository userRepository,
            IConfiguration configuration,
            ILogger<AuthService> logger)
        {
            _userRepository = userRepository;
            _logger = logger;

            // Validar configuración JWT al iniciar — falla rápido si falta algún valor
            _jwtKey = configuration["Jwt:Key"]
                ?? throw new InvalidOperationException("Jwt:Key no está configurado.");
            _jwtIssuer = configuration["Jwt:Issuer"]
                ?? throw new InvalidOperationException("Jwt:Issuer no está configurado.");
            _jwtAudience = configuration["Jwt:Audience"]
                ?? throw new InvalidOperationException("Jwt:Audience no está configurado.");
            _jwtExpirationHours = int.TryParse(configuration["Jwt:ExpirationHours"], out var h)
                ? h
                : throw new InvalidOperationException("Jwt:ExpirationHours no está configurado o no es un número válido.");
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
        /// El token incluye: ID de usuario, nombre, rol, nombre completo y JTI único.
        /// </summary>
        /// <param name="user">Datos del usuario autenticado desde la BD.</param>
        /// <returns>DTO con el token JWT, datos del usuario y fecha de expiración.</returns>
        private LoginResponseDto GenerateToken(UserDto user)
        {
            var securityKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(_jwtKey));

            var credentials = new SigningCredentials(
                securityKey,
                SecurityAlgorithms.HmacSha256);

            var expiresAt = DateTime.UtcNow.AddHours(_jwtExpirationHours);

            // Claims que viajan dentro del token — disponibles sin consultar la BD
            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub,        user.UserId.ToString()),
                new Claim(JwtRegisteredClaimNames.UniqueName, user.Username),
                new Claim(ClaimTypes.Role,                    user.RoleName),
                new Claim(ClaimTypes.GivenName,               user.FullName ?? string.Empty),
                new Claim(JwtRegisteredClaimNames.Jti,        Guid.NewGuid().ToString())
            };

            var token = new JwtSecurityToken(
                issuer: _jwtIssuer,
                audience: _jwtAudience,
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