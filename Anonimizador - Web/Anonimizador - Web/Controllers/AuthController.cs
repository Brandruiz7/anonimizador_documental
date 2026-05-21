using Anonimizador___Web.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

namespace Anonimizador___Web.Controllers
{
    /// <summary>
    /// Controlador de autenticación.
    /// Gestiona el inicio y cierre de sesión mediante cookies cifradas.
    /// </summary>
    [Route("auth")]
    public class AuthController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<AuthController> _logger;

        /// <summary>
        /// Inicializa el controlador con sus dependencias.
        /// </summary>
        public AuthController(
            IConfiguration configuration,
            ILogger<AuthController> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        /// <summary>
        /// Muestra el formulario de login.
        /// Redirige al inicio si el usuario ya está autenticado.
        /// </summary>
        [HttpGet("login")]
        public IActionResult Login(string? returnUrl = null)
        {
            if (User.Identity?.IsAuthenticated == true)
                return RedirectToAction("Index", "Home");

            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        /// <summary>
        /// Procesa las credenciales del formulario de login.
        /// Si son válidas, genera una cookie de sesión con el JWT.
        /// </summary>
        [HttpPost("login")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(
            LoginViewModel model,
            string? returnUrl = null)
        {
            if (!ModelState.IsValid)
                return View(model);

            try
            {
                var apiUrl = _configuration["ApiSettings:BaseUrl"];

                var handler = new HttpClientHandler
                {
                    ServerCertificateCustomValidationCallback =
                        HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
                };

                using var client = new HttpClient(handler);

                var body = JsonSerializer.Serialize(new
                {
                    username = model.Username,
                    password = model.Password
                });

                var response = await client.PostAsync(
                    $"{apiUrl}/api/auth/login",
                    new StringContent(body, Encoding.UTF8, "application/json"));

                if (!response.IsSuccessStatusCode)
                {
                    ModelState.AddModelError("", "Usuario o contraseña incorrectos.");
                    return View(model);
                }

                var json = await response.Content.ReadAsStringAsync();
                var loginResponse = JsonSerializer.Deserialize<JsonElement>(json);

                var token = loginResponse.GetProperty("token").GetString()!;
                var username = loginResponse.GetProperty("username").GetString()!;
                var fullName = loginResponse.GetProperty("fullName").GetString() ?? string.Empty;
                var role = loginResponse.GetProperty("role").GetString()!;

                var claims = new List<Claim>
                {
                    new(ClaimTypes.Name, username),
                    new(ClaimTypes.GivenName,  fullName),
                    new(ClaimTypes.Role, role),
                    new("jwt_token",     token)
                };

                var identity = new ClaimsIdentity(
                    claims, CookieAuthenticationDefaults.AuthenticationScheme);

                var principal = new ClaimsPrincipal(identity);

                await HttpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    principal,
                    new AuthenticationProperties
                    {
                        IsPersistent = false,
                        ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(30)
                    });

                _logger.LogInformation(
                    "Login exitoso: {Username} | Rol: {Role}", username, role);

                if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                    return Redirect(returnUrl);

                return RedirectToAction("Index", "Home");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error durante el login");
                ModelState.AddModelError("", "Error de conexión con el servicio.");
                return View(model);
            }
        }

        /// <summary>
        /// Cierra la sesión del usuario eliminando la cookie de autenticación.
        /// </summary>
        [HttpGet("logout")]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(
                CookieAuthenticationDefaults.AuthenticationScheme);

            return RedirectToAction("Login");
        }

        /// <summary>
        /// Renueva la sesión del usuario disparando el mecanismo de
        /// sliding expiration de la cookie. Usado por el timer del cliente.
        /// </summary>
        [HttpPost("renew")]
        [Authorize]
        public IActionResult Renew() => Ok();
    }
}