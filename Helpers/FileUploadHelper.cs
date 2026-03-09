namespace JobPortal.Helpers
{
    public static class FileUploadHelper
    {
        public const string RESUMES_FOLDER = "resumes";
        public const int MAX_FILE_SIZE = 5 * 1024 * 1024; // 5MB
        private static readonly string[] AllowedExtensions = { ".pdf", ".docx", ".txt" };

        public static (bool isValid, string? errorMessage) ValidateResume(IFormFile? resume)
        {
            if (resume == null || resume.Length == 0)
                return (false, "Please upload a resume file.");

            var fileExtension = Path.GetExtension(resume.FileName).ToLowerInvariant();
            if (!AllowedExtensions.Contains(fileExtension))
                return (false, "Only PDF, DOCX, and TXT files are allowed.");

            if (resume.Length > MAX_FILE_SIZE)
                return (false, "File size cannot exceed 5MB.");

            return (true, null);
        }

        public static async Task<(bool success, string? filePath, string? errorMessage)> SaveResumeAsync(
            IFormFile resume, 
            string webRootPath)
        {
            try
            {
                string uploadsFolder = Path.Combine(webRootPath, RESUMES_FOLDER);
                if (!Directory.Exists(uploadsFolder))
                    Directory.CreateDirectory(uploadsFolder);

                string uniqueFileName = Guid.NewGuid().ToString() + "_" + resume.FileName;
                string filePath = Path.Combine(uploadsFolder, uniqueFileName);

                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await resume.CopyToAsync(fileStream);
                }

                return (true, $"/{RESUMES_FOLDER}/{uniqueFileName}", null);
            }
            catch (Exception ex)
            {
                return (false, null, $"File upload error: {ex.Message}");
            }
        }

        public static bool DeleteResume(string resumePath, string webRootPath)
        {
            try
            {
                if (string.IsNullOrEmpty(resumePath))
                    return false;

                string filePath = Path.Combine(webRootPath, resumePath.TrimStart('/'));
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
    }
}
