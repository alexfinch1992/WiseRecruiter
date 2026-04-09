namespace JobPortal.Services.Interfaces
{
    /// <summary>
    /// Abstraction for file operations (upload, delete, validate).
    /// Enables swapping filesystem operations for cloud storage (S3, Azure Blob, etc.)
    /// </summary>
    public interface IFileUploadService
    {
        /// <summary>
        /// Validates a resume file before upload.
        /// </summary>
        (bool isValid, string? errorMessage) ValidateResume(IFormFile? file);

        /// <summary>
        /// Validates a general document file.
        /// </summary>
        (bool isValid, string? errorMessage) ValidateDocument(IFormFile? file);

        /// <summary>
        /// Uploads and stores a resume file.
        /// Returns path identifier for later retrieval/deletion.
        /// </summary>
        Task<(bool success, string? fileIdentifier, string? errorMessage)> UploadResumeAsync(IFormFile file);

        /// <summary>
        /// Uploads and stores a document file.
        /// </summary>
        Task<(bool success, string? fileIdentifier, string? errorMessage)> UploadDocumentAsync(IFormFile file);

        /// <summary>
        /// Deletes a resume by its stored identifier.
        /// </summary>
        Task<bool> DeleteResumeAsync(string? fileIdentifier);

        /// <summary>
        /// Deletes a document by its stored identifier.
        /// </summary>
        Task<bool> DeleteDocumentAsync(string? fileIdentifier);
    }
}
