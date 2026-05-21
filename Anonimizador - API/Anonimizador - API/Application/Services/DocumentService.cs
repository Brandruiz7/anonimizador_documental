using Anonimizador___API.Application.DTOs;
using Anonimizador___API.Interfaces.Repositories;
using Anonimizador___API.Interfaces.Services;
using DocumentFormat.OpenXml.Packaging;
using System.Security.Cryptography;

namespace Anonimizador___API.Application.Services
{
    /// <summary>
    /// Service responsible for document upload and anonymization processing.
    /// </summary>
    public class DocumentService : IDocumentService
    {
        private readonly IDocumentRepository _repository;
        private readonly IEnumerable<IDocumentProcessor> _documentProcessors;
        private readonly ILogger<DocumentService> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="DocumentService"/> class.
        /// </summary>
        public DocumentService(
            IDocumentRepository repository,
            IEnumerable<IDocumentProcessor> documentProcessors,
            ILogger<DocumentService> logger)
        {
            _repository = repository;
            _documentProcessors = documentProcessors;
            _logger = logger;
        }

        /// <summary>
        /// Secure streaming version for document anonymization.
        /// Supports DOCX and PDF files.
        /// </summary>
        public async Task<(Stream FileStream, string FileName, string ContentType)> UploadStreamAsync(
            UploadDocumentRequestDto request)
        {
            try
            {
                _logger.LogInformation(
                    "Starting streaming anonymization process");

                // =========================
                // 1. VALIDATIONS
                // =========================

                var file = request.File
                    ?? throw new Exception("File is null");

                if (file.Length == 0)
                {
                    throw new Exception("File is empty");
                }

                var extension =
                    Path.GetExtension(file.FileName)
                        .ToLowerInvariant();

                var allowedExtensions =
                    new[] { ".docx", ".pdf" };

                if (!allowedExtensions.Contains(extension))
                {
                    throw new Exception(
                        "Only DOCX and PDF files are supported");
                }

                var allowedContentTypes = new[]
                {
                    "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                    "application/pdf"
                };

                if (!allowedContentTypes.Contains(file.ContentType))
                {
                    throw new Exception("Invalid content type");
                }

                if (file.Length > 104857600)
                {
                    throw new Exception(
                        "File exceeds maximum allowed size");
                }

                // =========================
                // 2. LOAD FILE INTO MEMORY
                // =========================

                var workStream = new MemoryStream();

                await file.OpenReadStream()
                    .CopyToAsync(workStream);

                workStream.Position = 0;

                _logger.LogInformation(
                    "File loaded into work stream");

                // =========================
                // 3. VALIDATE DOCUMENT
                // =========================

                try
                {
                    if (extension == ".docx")
                    {
                        using var doc =
                            WordprocessingDocument.Open(
                                workStream,
                                false);
                    }
                    else if (extension == ".pdf")
                    {
                        workStream.Position = 0;

                        using var reader =
                            new StreamReader(
                                workStream,
                                leaveOpen: true);

                        var header = new char[5];

                        await reader.ReadBlockAsync(
                            header,
                            0,
                            5);

                        var pdfHeader =
                            new string(header);

                        if (!pdfHeader.StartsWith("%PDF"))
                        {
                            throw new Exception(
                                "Invalid PDF document");
                        }
                    }

                    workStream.Position = 0;
                }
                catch
                {
                    await workStream.DisposeAsync();

                    throw new Exception(
                        $"Invalid document format for extension {extension}");
                }

                // =========================
                // 4. ORIGINAL HASH
                // =========================

                string originalHash;

                using (var sha256 = SHA256.Create())
                {
                    workStream.Position = 0;

                    var hashBytes =
                        await sha256.ComputeHashAsync(
                            workStream);

                    originalHash =
                        BitConverter
                            .ToString(hashBytes)
                            .Replace("-", "");

                    workStream.Position = 0;
                }

                // =========================
                // 5. REGISTER PROCESS
                // =========================

                var processId =
                    await _repository
                        .InsertDocumentProcessAsync(
                            file.FileName,
                            file.ContentType,
                            file.Length / 1024,
                            originalHash,
                            request.UploadedBy);

                // =========================
                // 6. BUILD TARGETS
                // =========================

                if (request.Persons == null ||
                    request.Persons.Count == 0)
                {
                    throw new Exception(
                        "At least one person must be provided.");
                }

                var targets = new List<AnonymizationTargetDto>();

                for (int i = 0; i < request.Persons.Count; i++)
                {
                    var p = request.Persons[i];

                    if (string.IsNullOrWhiteSpace(p.FullName) &&
                        string.IsNullOrWhiteSpace(p.Identification) &&
                        string.IsNullOrWhiteSpace(p.Email) &&
                        string.IsNullOrWhiteSpace(p.PhoneNumber) &&
                        string.IsNullOrWhiteSpace(p.Position) &&
                        string.IsNullOrWhiteSpace(p.Address))
                        continue;

                    // Target principal con índice real
                    targets.Add(new AnonymizationTargetDto
                    {
                        PersonIndex = i,
                        FullName = p.FullName,
                        Identification = p.Identification,
                        Email = p.Email,
                        PhoneNumber = p.PhoneNumber,
                        Position = p.Position,
                        Address = p.Address
                    });

                    // Variaciones — mismo índice de persona
                    foreach (var variation in p.NameVariations)
                    {
                        if (!string.IsNullOrWhiteSpace(variation))
                        {
                            targets.Add(new AnonymizationTargetDto
                            {
                                PersonIndex = i, // ← mismo índice que la persona principal
                                FullName = variation
                            });
                        }
                    }
                }

                // LOG TEMPORAL
                _logger.LogWarning("Targets built: {Count}", targets.Count);
                foreach (var t in targets)
                {
                    _logger.LogWarning(
                        "Target: PersonIndex={Idx} | FullName={Name}",
                        t.PersonIndex,
                        t.FullName ?? "null");
                }

                if (targets.Count == 0)
                {
                    throw new Exception(
                        "No anonymization targets provided.");
                }

                // =========================
                // 7. PROCESS DOCUMENT
                // =========================

                workStream.Position = 0;

                var fileBytes =
                    workStream.ToArray();

                await workStream.DisposeAsync();

                var processor =
                    _documentProcessors
                        .FirstOrDefault(
                            p => p.CanProcess(extension));

                if (processor == null)
                {
                    throw new Exception(
                        $"No processor available for extension {extension}");
                }

                var anonymizationResult =
                    await processor.ProcessAsync(
                        fileBytes,
                        targets);

                Array.Clear(
                    fileBytes,
                    0,
                    fileBytes.Length);

                // =========================
                // 8. HASH + VERSION
                // =========================

                string anonymizedHash;

                using (var sha256 = SHA256.Create())
                {
                    var hashBytes =
                        sha256.ComputeHash(
                            anonymizationResult.FileBytes);

                    anonymizedHash =
                        BitConverter
                            .ToString(hashBytes)
                            .Replace("-", "");
                }

                var versionId =
                    await _repository
                        .InsertDocumentVersionAsync(
                            processId,
                            "ANONYMIZED",
                            anonymizedHash);

                // =========================
                // 9. AUDIT
                // =========================

                foreach (var field in anonymizationResult.AuditFields)
                {
                    await _repository
                        .InsertAuditFieldAsync(
                            versionId,
                            field.FieldType,
                            field.OriginalValue,
                            field.AnonymizedValue);
                }

                // =========================
                // 10. UPDATE STATUS
                // =========================

                await _repository
                    .UpdateProcessStatusAsync(
                        processId,
                        3);

                // =========================
                // 11. RETURN STREAM
                // =========================

                var resultStream =
                    new MemoryStream(
                        anonymizationResult.FileBytes);

                resultStream.Position = 0;

                return (
                    resultStream,
                    $"ANONYMIZED_{file.FileName}",
                    file.ContentType
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error during streaming anonymization");

                throw;
            }
        }

        public async Task<IEnumerable<DocumentSummaryDto>>
            GetAllDocumentsAsync()
        {
            return await _repository
                .GetAllDocumentsAsync();
        }

        public async Task<MetricsResponseDto>
            GetMetricsAsync()
        {
            return await _repository
                .GetMetricsAsync();
        }
    }
}