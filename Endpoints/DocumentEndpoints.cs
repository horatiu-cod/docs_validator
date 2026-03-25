using System.Security.Claims;
using DocsValidator.Data;
using DocsValidator.Models;
using DocsValidator.Services;
using Microsoft.EntityFrameworkCore;

namespace DocsValidator.Endpoints;

public static class DocumentEndpoints
{
    public static void MapDocumentEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/documents").WithName("Documents");

        // Upload document
        group.MapPost("/upload", UploadDocument)
            .WithName("UploadDocument")
            .Accepts<IFormFile>("multipart/form-data")
            .RequireAuthorization();

        // Download document
        group.MapGet("/{documentId}/download", DownloadDocument)
            .WithName("DownloadDocument")
            .RequireAuthorization();

        // Get document details
        group.MapGet("/{documentId}", GetDocument)
            .WithName("GetDocument")
            .RequireAuthorization();

        // List user documents
        group.MapGet("/", ListUserDocuments)
            .WithName("ListUserDocuments")
            .RequireAuthorization();

        // Get document validation status
        group.MapGet("/{documentId}/validation-status", GetValidationStatus)
            .WithName("GetValidationStatus")
            .RequireAuthorization();
    }

    private static async Task<IResult> UploadDocument(
        HttpContext httpContext,
        IFormCollection form,
        IDocumentStorageService documentStorageService,
        IWorkflowService workflowService,
        IClamAVService clamAVService,
        IDigitalSignatureValidationService signatureService)
    {
        var userId = Guid.Parse(httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty);
        var file = form.Files[0];

        if (file == null || file.Length == 0)
            return Results.BadRequest("No file provided");

        if (file.Length > 100 * 1024 * 1024) // 100MB limit
            return Results.BadRequest("File size exceeds 100MB limit");

        try
        {
            // Upload document
            using var stream = file.OpenReadStream();
            var document = await documentStorageService.UploadDocumentAsync(
                stream,
                file.FileName,
                userId,
                hasDigitalSignature: bool.TryParse(form["hasDigitalSignature"], out var sig) && sig,
                signatureOwner: form["signatureOwner"].ToString()
            );

            // Initiate workflow
            var workflow = await workflowService.InitiateWorkflowAsync(document.Id, userId);

            // Add validation step
            await workflowService.AddValidationStepAsync(workflow.Id);

            // Scan with ClamAV
            var fileContent = await File.ReadAllBytesAsync(document.FilePath);
            var (isClean, details) = await clamAVService.ScanFileAsync(fileContent, document.StoredFileName);
            await documentStorageService.UpdateDocumentValidationAsync(document.Id, isClean, details ?? "No details");

            if (!isClean)
            {
                await workflowService.RejectWorkflowAsync(workflow.Id, "File failed ClamAV scan");
                return Results.BadRequest(new { error = "File failed security scan", details });
            }

            // Validate digital signature if present
            if (document.HasDigitalSignature && !string.IsNullOrEmpty(document.SignatureOwner))
            {
                var (isValidSignature, owner) = await signatureService.ValidateDigitalSignatureAsync(
                    fileContent,
                    document.SignatureOwner
                );

                if (!isValidSignature)
                {
                    await workflowService.RejectWorkflowAsync(workflow.Id, "Digital signature validation failed");
                    return Results.BadRequest("Digital signature validation failed");
                }
            }

            return Results.Created($"/api/documents/{document.Id}", new
            {
                document.Id,
                document.OriginalFileName,
                document.StoredFileName,
                workflowId = workflow.Id,
                status = workflow.Status.ToString()
            });
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            
            return Results.StatusCode(500);
        }
    }

    private static async Task<IResult> DownloadDocument(
        Guid documentId,
        HttpContext httpContext,
        IDocumentStorageService documentStorageService,
        IAuthorizationService authorizationService)
    {
        var userId = Guid.Parse(httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty);

        // Check authorization
        var canAccess = await authorizationService.CanAccessDocumentAsync(userId, documentId);
        if (!canAccess)
            return Results.Forbid();

        try
        {
            var (fileContent, fileName) = await documentStorageService.DownloadDocumentAsync(documentId);
            return Results.File(fileContent, "application/pdf", fileName);
        }
        catch (InvalidOperationException)
        {
            return Results.NotFound();
        }
    }

    private static async Task<IResult> GetDocument(
        Guid documentId,
        HttpContext httpContext,
        IDocumentStorageService documentStorageService,
        IAuthorizationService authorizationService)
    {
        var userId = Guid.Parse(httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty);

        var canAccess = await authorizationService.CanAccessDocumentAsync(userId, documentId);
        if (!canAccess)
            return Results.Forbid();

        var document = await documentStorageService.GetDocumentAsync(documentId);
        if (document == null)
            return Results.NotFound();

        return Results.Ok(new
        {
            document.Id,
            document.OriginalFileName,
            document.StoredFileName,
            document.FileSize,
            document.UploadedAt,
            document.HasDigitalSignature,
            document.SignatureOwner,
            document.IsCleanAccordingToClamAV,
            document.ClamAVScanDate
        });
    }

    private static async Task<IResult> ListUserDocuments(
        HttpContext httpContext,
        IDocumentStorageService documentStorageService)
    {
        var userId = Guid.Parse(httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty);

        var documents = await documentStorageService.GetUserDocumentsAsync(userId);
        return Results.Ok(documents.Select(d => new
        {
            d.Id,
            d.OriginalFileName,
            d.StoredFileName,
            d.FileSize,
            d.UploadedAt,
            d.IsCleanAccordingToClamAV
        }));
    }

    private static async Task<IResult> GetValidationStatus(
        Guid documentId,
        HttpContext httpContext,
        IDocumentStorageService documentStorageService,
        IAuthorizationService authorizationService)
    {
        var userId = Guid.Parse(httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty);

        var canAccess = await authorizationService.CanAccessDocumentAsync(userId, documentId);
        if (!canAccess)
            return Results.Forbid();

        var document = await documentStorageService.GetDocumentAsync(documentId);
        if (document == null)
            return Results.NotFound();

        return Results.Ok(new
        {
            document.Id,
            document.IsCleanAccordingToClamAV,
            document.ClamAVScanResult,
            document.ClamAVScanDate,
            document.HasDigitalSignature
        });
    }
}
