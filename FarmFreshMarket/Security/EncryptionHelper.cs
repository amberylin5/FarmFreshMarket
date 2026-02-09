using System.Security.Cryptography;
using System.Text;

namespace FarmFreshMarket.Security
{
    public static class EncryptionHelper
    {
        // Base secret (can be any string)
        private static readonly string secretKey = "FarmFreshMarketSecretKey";

        // Always produces 32 bytes (256-bit key)
        private static byte[] GetAesKey()
        {
            using SHA256 sha256 = SHA256.Create();
            return sha256.ComputeHash(Encoding.UTF8.GetBytes(secretKey));
        }

        public static string Encrypt(string plainText)
        {
            using Aes aes = Aes.Create();
            aes.Key = GetAesKey();   // ✅ ALWAYS valid
            aes.GenerateIV();

            using var encryptor = aes.CreateEncryptor();
            byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
            byte[] cipherBytes = encryptor.TransformFinalBlock(
                plainBytes, 0, plainBytes.Length);

            return Convert.ToBase64String(aes.IV) + ":" +
                   Convert.ToBase64String(cipherBytes);
        }

        public static string Decrypt(string cipherText)
        {
            var parts = cipherText.Split(':');

            using Aes aes = Aes.Create();
            aes.Key = GetAesKey();   // ✅ SAME key
            aes.IV = Convert.FromBase64String(parts[0]);

            using var decryptor = aes.CreateDecryptor();
            byte[] cipherBytes = Convert.FromBase64String(parts[1]);
            byte[] plainBytes = decryptor.TransformFinalBlock(
                cipherBytes, 0, cipherBytes.Length);

            return Encoding.UTF8.GetString(plainBytes);
        }
    }
}
