using System.Security.Cryptography;
using System.Text;

namespace JobPortal.Helpers
{
    public static class PasswordHasher
    {
        public static string Hash(string password)
        {
            using (var sha256 = SHA256.Create())
            {
                var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
                return Convert.ToBase64String(hashedBytes);
            }
        }

        public static bool Verify(string password, string hash)
        {
            var hashOfInput = Hash(password);
            return hashOfInput == hash;
        }
    }
}
