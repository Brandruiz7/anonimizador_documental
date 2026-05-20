using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Anonimizador___Web.Controllers
{
    /// <summary>
    /// Controlador encargado de recibir el formulario del Web
    /// y enviarlo a la API para su procesamiento.
    /// </summary>
    [Authorize]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    [Route("upload")]
    public class UploadController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<UploadController> _logger;

        public UploadController(
            IConfiguration configuration,
            ILogger<UploadController> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        /// <summary>
        /// Recibe el formulario multipart, construye el request hacia la API
        /// con múltiples personas y retorna el documento anonimizado.
        /// </summary>
        [HttpPost("process")]
        [IgnoreAntiforgeryToken]
        [RequestSizeLimit(104857600)]
        [RequestFormLimits(MultipartBodyLengthLimit = 104857600)]
        public async Task<IActionResult> Process()
        {
            try
            {
                var form = await Request.ReadFormAsync();

                // Validar archivo
                var file = form.Files.GetFile("documento");
                if (file == null || file.Length == 0)
                    return BadRequest("No file received");

                var apiUrl = _configuration["ApiSettings:BaseUrl"];
                var token = User.FindFirst("jwt_token")?.Value
                    ?? _configuration["ApiSettings:Token"];

                var handler = new HttpClientHandler
                {
                    ServerCertificateCustomValidationCallback =
                        HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
                };

                using var client = new HttpClient(handler);
                client.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

                using var content = new MultipartFormDataContent();

                // Archivo
                var fileContent = new StreamContent(file.OpenReadStream());
                fileContent.Headers.ContentType =
                    new System.Net.Http.Headers.MediaTypeHeaderValue(file.ContentType);
                content.Add(fileContent, "File", file.FileName);

                // UploadedBy
                var uploadedBy = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value
                    ?? form["UploadedBy"].ToString()
                    ?? "unknown";

                content.Add(new StringContent(uploadedBy), "UploadedBy");

                // Personas — leemos todos los índices del form
                // El front envía: Persons[0].FullName, Persons[1].Email, etc.
                var personIndex = 0;
                while (form.ContainsKey($"Persons[{personIndex}].FullName") ||
                       form.ContainsKey($"Persons[{personIndex}].Identification") ||
                       form.ContainsKey($"Persons[{personIndex}].Email") ||
                       form.ContainsKey($"Persons[{personIndex}].PhoneNumber") ||
                       form.ContainsKey($"Persons[{personIndex}].Position") ||
                       form.ContainsKey($"Persons[{personIndex}].Address"))
                {
                    var prefix = $"Persons[{personIndex}]";

                    content.Add(
                        new StringContent(form[$"{prefix}.FullName"].ToString() ?? ""),
                        $"Persons[{personIndex}].FullName");
                    content.Add(
                        new StringContent(form[$"{prefix}.Identification"].ToString() ?? ""),
                        $"Persons[{personIndex}].Identification");
                    content.Add(
                        new StringContent(form[$"{prefix}.Email"].ToString() ?? ""),
                        $"Persons[{personIndex}].Email");
                    content.Add(
                        new StringContent(form[$"{prefix}.PhoneNumber"].ToString() ?? ""),
                        $"Persons[{personIndex}].PhoneNumber");
                    content.Add(
                        new StringContent(form[$"{prefix}.Position"].ToString() ?? ""),
                        $"Persons[{personIndex}].Position");
                    content.Add(
                        new StringContent(form[$"{prefix}.Address"].ToString() ?? ""),
                        $"Persons[{personIndex}].Address");

                    personIndex++;
                }

                if (personIndex == 0)
                    return BadRequest("At least one person must be provided");

                _logger.LogInformation(
                    "Calling API: {Url} | Persons: {Count}",
                    $"{apiUrl}/api/documents/upload",
                    personIndex);

                var response = await client.PostAsync(
                    $"{apiUrl}/api/documents/upload", content);

                if (!response.IsSuccessStatusCode)
                {
                    var errorBody = await response.Content.ReadAsStringAsync();
                    _logger.LogError("API error: {Status} {Body}",
                        response.StatusCode, errorBody);
                    return StatusCode((int)response.StatusCode, errorBody);
                }

                var fileBytes = await response.Content.ReadAsByteArrayAsync();
                var fileName = $"ANONYMIZED_{file.FileName}";

                return File(fileBytes,
                    "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                    fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Upload.Process: {Message}", ex.Message);
                return StatusCode(500, ex.Message);
            }
        }
    }
}