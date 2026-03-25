using DocsValidator.Data;
using DocsValidator.Models;
using Microsoft.EntityFrameworkCore;

namespace DocsValidator.Services;

public interface IDocumentStorageService
{
    Task<Document> UploadDocumentAsync(Stream fileStream, string fileName, Guid uploadedById, bool hasDigitalSignature, string? signatureOwner);
    Task<(byte[], string)> DownloadDocumentAsync(Guid documentId);
    Task<Document?> GetDocumentAsync(Guid documentId);
    Task<List<Document>> GetUserDocumentsAsync(Guid userId);
    Task UpdateDocumentValidationAsync(Guid documentId, bool isClean, string scanResult);
}

public class DocumentStorageService : IDocumentStorageService
{
    private readonly ApplicationDbContext _context;
    private readonly IFileValidationService _fileValidationService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DocumentStorageService> _logger;

    public DocumentStorageService(
        ApplicationDbContext context,
        IFileValidationService fileValidationService,
        IConfiguration configuration,
        ILogger<DocumentStorageService> logger)
    {
        _context = context;
        _fileValidationService = fileValidationService;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<Document> UploadDocumentAsync(
        Stream fileStream,
        string fileName,
        Guid uploadedById,
        bool hasDigitalSignature,
        string? signatureOwner)
    {
        // Validate file extension
        if (!_fileValidationService.IsValidPdfExtension(fileName))
            throw new InvalidOperationException("Only PDF files are allowed");

        // Create storage directory if it doesn't exist
        var storagePath = _configuration["FileStorage:Path"] ?? throw new InvalidOperationException("FileStorage:Path not configured");
        var uploadDir = Path.Combine(storagePath, "uploads");
        Directory.CreateDirectory(uploadDir);

        // Generate secure filename
        var secureFileName = _fileValidationService.GenerateSecureFileName(fileName);
        var filePath = Path.Combine(uploadDir, secureFileName);

        // Read file content
        using (var memoryStream = new MemoryStream())
        {
            await fileStream.CopyToAsync(memoryStream);
            var fileContent = memoryStream.ToArray();

            // Save file to disk
            await System.IO.File.WriteAllBytesAsync(filePath, fileContent);

            // Create document record
            var document = new Document
            {
                Id = Guid.NewGuid(),
                UploadedById = uploadedById,
                OriginalFileName = fileName,
                StoredFileName = secureFileName,
                FilePath = filePath,
                FileSize = fileContent.Length,
                FileHash = _fileValidationService.CalculateFileHash(fileContent),
                HasDigitalSignature = hasDigitalSignature,
                SignatureOwner = signatureOwner,
                UploadedAt = DateTime.UtcNow,
                IsCleanAccordingToClamAV = false // Will be updated after scanning
            };

            _context.Documents.Add(document);
            await _context.SaveChangesAsync();

            _logger.LogInformation($"Document {document.Id} uploaded: {fileName} -> {secureFileName}");
            return document;
        }
    }

    public async Task<(byte[], string)> DownloadDocumentAsync(Guid documentId)
    {
        var document = await _context.Documents.FirstOrDefaultAsync(d => d.Id == documentId);
        if (document == null)
            throw new InvalidOperationException("Document not found");

        if (!System.IO.File.Exists(document.FilePath))
            throw new InvalidOperationException("File not found on disk");

        var fileContent = await System.IO.File.ReadAllBytesAsync(document.FilePath);
        return (fileContent, document.OriginalFileName);
    }

    public async Task<Document?> GetDocumentAsync(Guid documentId)
    {
        return await _context.Documents
            .Include(d => d.UploadedBy)
            .FirstOrDefaultAsync(d => d.Id == documentId);
    }

    public async Task<List<Document>> GetUserDocumentsAsync(Guid userId)
    {
        return await _context.Documents
            .Where(d => d.UploadedById == userId)
            .Include(d => d.UploadedBy)
            .ToListAsync();
    }

    public async Task UpdateDocumentValidationAsync(Guid documentId, bool isClean, string scanResult)
    {
        var document = await _context.Documents.FirstOrDefaultAsync(d => d.Id == documentId);
        if (document == null)
            throw new InvalidOperationException("Document not found");

        document.IsCleanAccordingToClamAV = isClean;
        document.ClamAVScanResult = scanResult;
        document.ClamAVScanDate = DateTime.UtcNow;

        _context.Documents.Update(document);
        await _context.SaveChangesAsync();

        _logger.LogInformation($"Document {documentId} validation updated: Clean={isClean}");
    }
}
