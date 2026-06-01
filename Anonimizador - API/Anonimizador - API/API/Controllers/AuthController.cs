using Anonimizador___API.Application.DTOs.Auth;
using Anonimizador___API.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Anonimizador___API.API.Controllers
{
    /// <summary>
    /// Controlador de autenticación.
    /// Expone el endpoint de login y generación de tokens JWT.
    ///
    /// Seguridad aplicada:
    /// - Rate limiting: máximo 10 intentos por minuto por IP (política "login")
    /// - Las credenciales inválidas retornan siempre el mismo mensaje para evitar
    ///   que un atacante distinga entre usuario inexistente y contraseña incorrecta
    /// </summary>
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;

        /// <summary>
        /// Inicializa el controlador con el servicio de autenticación.
        /// </summary>
        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }

        /// <summary>
        /// Autentica un usuario y retorna un token JWT firmado.
        /// El token incluye claims de identidad, rol y expiración.
        /// Debe incluirse en el header Authorization: Bearer {token} en requests posteriores.
        /// </summary>
        /// <param name="request">Credenciales de acceso (usuario y contraseña).</param>
        /// <returns>Token JWT, datos del usuario autenticado y fecha de expiración.</returns>
        /// <response code="200">Login exitoso — retorna token y datos del usuario.</response>
        /// <response code="400">Request inválido — campos requeridos faltantes.</response>
        /// <response code="401">Credenciales inválidas o usuario inactivo.</response>
        /// <response code="429">Demasiados intentos — esperá 1 minuto.</response>
        [HttpPost("login")]
        [EnableRateLimiting("login")]
        [ProducesResponseType(typeof(LoginResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
        public async Task<IActionResult> Login([FromBody] LoginRequestDto request)
        {
            // ModelState valida automáticamente los DataAnnotations del DTO
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var response = await _authService.LoginAsync(request);
            return Ok(response);
        }

        /// <summary>
        /// Genera un hash BCrypt para una contraseña en texto plano.
        /// Usar para crear o actualizar la columna PasswordHash en la tabla USERS.
        /// </summary>
        /// <param name="password">Contraseña en texto plano a hashear.</param>
        /// <returns>Hash BCrypt listo para insertar en la base de datos.</returns>
        /// <response code="200">Hash generado correctamente.</response>
        /// <response code="400">El parámetro password está vacío.</response>
        [HttpGet("generate-hash")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public IActionResult GenerateHash([FromQuery] string password)
        {
            if (string.IsNullOrWhiteSpace(password))
                return BadRequest(new { error = "El parámetro password es requerido." });

            return Ok(new { hash = BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12) });
        }
    }
}