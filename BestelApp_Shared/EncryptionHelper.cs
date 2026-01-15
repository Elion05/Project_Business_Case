using System.Security.Cryptography;
using System.Text;

namespace BestelApp_Shared
{
    public static class EncryptionHelper
    {
        // ⚠️ WAARSCHUWING: In productie moeten deze keys in een veilige omgeving (Azure KeyVault, User Secrets) staan!
        // NIET hardcoded in de code checken. Voor deze demo is dit voldoende.
        private static readonly string KeyConfig = "E546C8DF278CD5931069B522E695D4F2";

        // 32 bytes key (256-bit) generated for demo
        private static readonly byte[] Key = Convert.FromBase64String("q7w1E9Z/8JqW3zT4r5Y7uI9oP2aS4dF6gH8jK1l3zX0=");

        // 16 bytes IV (128-bit) generated for demo
        private static readonly byte[] IV = Convert.FromBase64String("9sX2k5v8yRn4qWe1tZa3oQ==");

        public static string Encrypt(string plainText)
        {
            if (string.IsNullOrEmpty(plainText))
                return plainText;

            using (Aes aesAlg = Aes.Create())
            {
                aesAlg.Key = Key;
                aesAlg.IV = IV;

                ICryptoTransform encryptor = aesAlg.CreateEncryptor(aesAlg.Key, aesAlg.IV);

                using (MemoryStream msEncrypt = new MemoryStream())
                {
                    using (CryptoStream csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
                    {
                        using (StreamWriter swEncrypt = new StreamWriter(csEncrypt))
                        {
                            swEncrypt.Write(plainText);
                        }
                    }
                    return Convert.ToBase64String(msEncrypt.ToArray());
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

                using (Aes aesAlg = Aes.Create())
                {
                    aesAlg.Key = Key;
                    aesAlg.IV = IV;

                    ICryptoTransform decryptor = aesAlg.CreateDecryptor(aesAlg.Key, aesAlg.IV);

                    using (MemoryStream msDecrypt = new MemoryStream(cipherBytes))
                    {
                        using (CryptoStream csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
                        {
                            using (StreamReader srDecrypt = new StreamReader(csDecrypt))
                            {
                                return srDecrypt.ReadToEnd();
                            }
                        }
                    }
                }
            }
            catch (FormatException)
            {
                // Waarschijnlijk geen base64 of geen encrypted string, return origineel zodat fallback werkt
                return cipherText;
            }
            catch (CryptographicException)
            {
                // Decryptie faalt (verkeerde key, of data corrupt/niet encrypted)
                return cipherText;
            }
        }
    }
}
