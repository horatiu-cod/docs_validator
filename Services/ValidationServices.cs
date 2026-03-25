using System.Security.Cryptography;

namespace DocsValidator.Services;

public interface IFileValidationService
{
    bool IsValidPdfExtension(string fileName);
    string GenerateSecureFileName(string originalFileName);
    string CalculateFileHash(byte[] fileContent);
}

public class FileValidationService : IFileValidationService
{
    private const string AllowedExtension = ".pdf";

    public bool IsValidPdfExtension(string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        return extension == AllowedExtension;
    }

    public string GenerateSecureFileName(string originalFileName)
    {
        var extension = Path.GetExtension(originalFileName).ToLowerInvariant();
        if (extension != AllowedExtension)
            throw new InvalidOperationException("Invalid file extension. Only PDF files are allowed.");

        return $"{Guid.NewGuid():N}{extension}";
    }

    /// <summary>Returns a base-64 encoded SHA-256 hash of the file content.</summary>
    public string CalculateFileHash(byte[] fileContent)
    {
        // SHA256.HashData is a one-shot, allocation-efficient static method (available since .NET 5)
        var hashBytes = SHA256.HashData(fileContent);
        return Convert.ToBase64String(hashBytes);
    }
}

public interface IDigitalSignatureValidationService
{
    Task<(bool IsValid, string? SignatureOwner)> ValidateDigitalSignatureAsync(byte[] fileContent, string expectedOwner);
}

public class DigitalSignatureValidationService : IDigitalSignatureValidationService
{
    public async Task<(bool IsValid, string? SignatureOwner)> ValidateDigitalSignatureAsync(byte[] fileContent, string expectedOwner)
    {
        // Placeholder – a real implementation would:
        // 1. Parse the PDF signature dictionary
        // 2. Extract the certificate
        // 3. Validate the signature cryptographically
        // 4. Return the certificate owner name
        await Task.CompletedTask;
        return (true, expectedOwner);
    }
}

public interface IClamAVService
{
    Task<(bool IsClean, string? Details)> ScanFileAsync(byte[] fileContent, string fileName);
}

public class ClamAVService : IClamAVService
{
    private readonly IConfiguration _configuration;
    private readonly HttpClient _httpClient;
    private readonly ILogger<ClamAVService> _logger;

    public ClamAVService(IConfiguration configuration, HttpClient httpClient, ILogger<ClamAVService> logger)
    {
        _configuration = configuration;
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<(bool IsClean, string? Details)> ScanFileAsync(byte[] fileContent, string fileName)
    {
        try
        {
            var clamAvUrl = _configuration["ClamAV:Url"];
            if (string.IsNullOrEmpty(clamAvUrl))
            {
                _logger.LogWarning("ClamAV URL not configured. Skipping scan for {FileName}", fileName);
                return (true, "ClamAV not configured");
            }

            using var content = new MultipartFormDataContent();
            content.Add(new ByteArrayContent(fileContent), "file", fileName);

            var response = await _httpClient.PostAsync($"{clamAvUrl}/scan", content);
            var responseContent = await response.Content.ReadAsStringAsync();

            // Parse ClamAV response (CLEAN or INFECTED message)
            var isClean = responseContent.Contains("CLEAN", StringComparison.OrdinalIgnoreCase);
            return (isClean, responseContent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scanning file {FileName} with ClamAV", fileName);
            // Fail secure – mark as potentially unclean if scanning fails
            return (false, $"Scan error: {ex.Message}");
        }
    }
}
