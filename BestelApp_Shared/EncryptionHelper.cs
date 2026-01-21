using System.Security.Cryptography;
using System.Text;
using System.IO;

namespace BestelApp_Shared
{
    public static class EncryptionHelper
    {
        // ⚠️ In productie moeten keys veilig opgeslagen worden (bv. KeyVault / User Secrets)
        // Voor deze demo is hardcoded OK

        // 32 bytes key (256-bit)
        private static readonly byte[] Key =
            Convert.FromBase64String("q7w1E9Z/8JqW3zT4r5Y7uI9oP2aS4dF6gH8jK1l3zX0=");

        // 16 bytes IV (128-bit)
        private static readonly byte[] IV =
            Convert.FromBase64String("9sX2k5v8yRn4qWe1tZa3oQ==");

        public static string Encrypt(string plainText)
        {
            if (string.IsNullOrEmpty(plainText))
                return plainText;

            using (Aes aes = Aes.Create())
            {
                aes.Key = Key;
                aes.IV = IV;

                ICryptoTransform encryptor = aes.CreateEncryptor();

                using (MemoryStream ms = new MemoryStream())
                {
                    using (CryptoStream cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                    using (StreamWriter sw = new StreamWriter(cs))
                    {
                        sw.Write(plainText);
                    }

                    return Convert.ToBase64String(ms.ToArray());
                }
            }
        }

        public static string Decrypt(string cipherText)
        {
            if (string.IsNullOrEmpty(cipherText))
                return cipherText;

            try
            {
                byte[] cipherBytes = Convert.FromBase64String(cipherText);

                using (Aes aes = Aes.Create())
                {
                    aes.Key = Key;
                    aes.IV = IV;

                    ICryptoTransform decryptor = aes.CreateDecryptor();

                    using (MemoryStream ms = new MemoryStream(cipherBytes))
                    using (CryptoStream cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read))
                    using (StreamReader sr = new StreamReader(cs))
                    {
                        return sr.ReadToEnd();
                    }
                }
            }
            catch
            {
                // Niet encrypted of fout → originele tekst teruggeven
                return cipherText;
            }
        }
    }
}
