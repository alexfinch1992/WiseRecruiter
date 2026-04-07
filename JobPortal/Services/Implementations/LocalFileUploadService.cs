using JobPortal.Helpers;
using JobPortal.Services.Interfaces;

namespace JobPortal.Services.Implementations
{
    public class LocalFileUploadService : IFileUploadService
    {
        private const string RESUMES_FOLDER = "Resumes";
        private const string DOCUMENTS_FOLDER = "Documents";
        private const int MAX_FILE_SIZE = 5 * 1024 * 1024; // 5MB
        private static readonly string[] AllowedExtensions = { ".pdf", ".doc", ".docx" };

        // PDF: starts with "%PDF"
        private static readonly byte[] PdfSignature = { 0x25, 0x50, 0x44, 0x46 };
        // ZIP (used by DOCX): starts with "PK" (0x50, 0x4B)
        private static readonly byte[] ZipSignature = { 0x50, 0x4B };
        // DOC (legacy OLE2 compound document): starts with 0xD0CF11E0A1B11AE1
        private static readonly byte[] DocSignature = { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 };

        private readonly string _storagePath;

        public LocalFileUploadService(IConfiguration configuration, IWebHostEnvironment environment)
        {
            _storagePath = configuration["FileStorage:BasePath"]
                ?? Path.Combine(environment.ContentRootPath, "App_Data");
        }

        public (bool isValid, string? errorMessage) ValidateResume(IFormFile? file)
        {
            return ValidateFile(file, "resume");
        }

        public (bool isValid, string? errorMessage) ValidateDocument(IFormFile? file)
        {
            return ValidateFile(file, "document");
        }

        public async Task<FileUploadResult> UploadResumeAsync(IFormFile file)
        {
            return await SaveFileAsync(file, RESUMES_FOLDER);
        }

        public async Task<FileUploadResult> UploadDocumentAsync(IFormFile file)
        {
            return await SaveFileAsync(file, DOCUMENTS_FOLDER);
        }

        public Task<bool> DeleteResumeAsync(string? storedFileName)
        {
            return Task.FromResult(DeleteFile(storedFileName, RESUMES_FOLDER));
        }

        public Task<bool> DeleteDocumentAsync(string? storedFileName)
        {
            return Task.FromResult(DeleteFile(storedFileName, DOCUMENTS_FOLDER));
        }

        public string? GetResumePhysicalPath(string? storedFileName)
        {
            return GetPhysicalPath(storedFileName, RESUMES_FOLDER);
        }

        public string? GetDocumentPhysicalPath(string? storedFileName)
        {
            return GetPhysicalPath(storedFileName, DOCUMENTS_FOLDER);
        }

        // ===== Private implementation =====

        private (bool isValid, string? errorMessage) ValidateFile(IFormFile? file, string label)
        {
            if (file == null || file.Length == 0)
                return (false, $"Please upload a {label} file.");

            if (file.Length > MAX_FILE_SIZE)
                return (false, "File size cannot exceed 5MB.");

            var safeFileName = Path.GetFileName(file.FileName);
            var extension = Path.GetExtension(safeFileName).ToLowerInvariant();

            if (!AllowedExtensions.Contains(extension))
                return (false, "Only PDF, DOC, and DOCX files are allowed.");

            if (!IsValidFileSignature(file.OpenReadStream(), extension))
                return (false, $"File content does not match the expected format for {extension} files.");

            return (true, null);
        }

        /// <summary>
        /// Validates file content by checking magic bytes. Resets stream position after reading.
        /// </summary>
        private static bool IsValidFileSignature(Stream fileStream, string extension)
        {
            try
            {
                var originalPosition = fileStream.Position;

                switch (extension)
                {
                    case ".pdf":
                    {
                        var header = new byte[PdfSignature.Length];
                        var bytesRead = fileStream.Read(header, 0, header.Length);
                        fileStream.Position = originalPosition;
                        return bytesRead == PdfSignature.Length && header.AsSpan().SequenceEqual(PdfSignature);
                    }
                    case ".docx":
                    {
                        var header = new byte[ZipSignature.Length];
                        var bytesRead = fileStream.Read(header, 0, header.Length);
                        fileStream.Position = originalPosition;
                        return bytesRead == ZipSignature.Length && header.AsSpan().SequenceEqual(ZipSignature);
                    }
                    case ".doc":
                    {
                        var header = new byte[DocSignature.Length];
                        var bytesRead = fileStream.Read(header, 0, header.Length);
                        fileStream.Position = originalPosition;
                        return bytesRead == DocSignature.Length && header.AsSpan().SequenceEqual(DocSignature);
                    }
                    default:
                        return false;
                }
            }
            catch
            {
                return false;
            }
        }

        private async Task<FileUploadResult> SaveFileAsync(IFormFile file, string subfolder)
        {
            try
            {
                var uploadsFolder = Path.Combine(_storagePath, subfolder);
                if (!Directory.Exists(uploadsFolder))
                    Directory.CreateDirectory(uploadsFolder);

                var safeOriginalName = Path.GetFileName(file.FileName);
                var extension = Path.GetExtension(safeOriginalName).ToLowerInvariant();
                var storedFileName = Guid.NewGuid().ToString() + extension;
                var filePath = Path.Combine(uploadsFolder, storedFileName);

                // Path traversal guard
                var resolvedPath = Path.GetFullPath(filePath);
                var resolvedFolder = Path.GetFullPath(uploadsFolder);
                if (!resolvedPath.StartsWith(resolvedFolder, StringComparison.OrdinalIgnoreCase))
                    return new FileUploadResult { Success = false, ErrorMessage = "Invalid file path." };

                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(fileStream);
                }

                return new FileUploadResult
                {
                    Success = true,
                    StoredFileName = storedFileName,
                    OriginalFileName = safeOriginalName,
                    ContentType = file.ContentType,
                    UploadedAt = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                return new FileUploadResult { Success = false, ErrorMessage = $"File upload error: {ex.Message}" };
            }
        }

        private bool DeleteFile(string? storedFileName, string subfolder)
        {
            try
            {
                if (string.IsNullOrEmpty(storedFileName))
                    return false;

                var safeName = Path.GetFileName(storedFileName);
                var folderPath = Path.Combine(_storagePath, subfolder);
                var filePath = Path.Combine(folderPath, safeName);

                var resolvedPath = Path.GetFullPath(filePath);
                var resolvedFolder = Path.GetFullPath(folderPath);
                if (!resolvedPath.StartsWith(resolvedFolder, StringComparison.OrdinalIgnoreCase))
                    return false;

                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    return true;
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        private string? GetPhysicalPath(string? storedFileName, string subfolder)
        {
            if (string.IsNullOrEmpty(storedFileName))
                return null;

            var safeName = Path.GetFileName(storedFileName);
            var folderPath = Path.Combine(_storagePath, subfolder);
            var filePath = Path.Combine(folderPath, safeName);

            var resolvedPath = Path.GetFullPath(filePath);
            var resolvedFolder = Path.GetFullPath(folderPath);
            if (!resolvedPath.StartsWith(resolvedFolder, StringComparison.OrdinalIgnoreCase))
                return null;

            return File.Exists(filePath) ? filePath : null;
        }
    }
}
