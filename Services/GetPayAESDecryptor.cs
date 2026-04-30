using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Globalization;

namespace PaymentAPI.Services
{
    /// <summary>
    /// Small helper for AES-256/128 CBC encrypt/decrypt where external systems expect hex ciphertext and base64 key/iv.
    /// Methods validate inputs and throw clear ArgumentException/CryptographicException on failure.
    /// </summary>
    public static class GetPayAESDecryptor
    {
        /// <summary>
        /// Decrypts a HEX encoded ciphertext using AES CBC. Key/IV must be provided as base64 strings.
        /// </summary>
        /// <param name="hexCipherText">Ciphertext in hex (e.g. "A1B2C3...").</param>
        /// <param name="base64Key">Base64 encoded key (16/24/32 bytes after decode).</param>
        /// <param name="base64IV">Base64 encoded IV (16 bytes after decode).</param>
        /// <returns>UTF-8 plaintext string.</returns>
        public static string DecryptHex(string hexCipherText, string base64Key, string base64IV)
        {
            if (string.IsNullOrWhiteSpace(hexCipherText))
                throw new ArgumentException("hexCipherText is required", nameof(hexCipherText));

            byte[] cipherBytes = ConvertHexToBytes(hexCipherText);

            byte[] keyBytes = DecodeBase64Key(base64Key, nameof(base64Key));
            byte[] ivBytes = DecodeBase64IV(base64IV, nameof(base64IV));

            ValidateAesKeyIvLengths(keyBytes, ivBytes);

            try
            {
                using Aes aes = Aes.Create();
                aes.Key = keyBytes;
                aes.IV = ivBytes;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                using ICryptoTransform decryptor = aes.CreateDecryptor();
                byte[] plainBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
                return Encoding.UTF8.GetString(plainBytes);
            }
            catch (CryptographicException ex)
            {
                throw new CryptographicException("AES decryption failed - input may be corrupted or key/IV incorrect.", ex);
            }
        }

        /// <summary>
        /// Encrypts plaintext to AES CBC and returns ciphertext as uppercase hex string.
        /// Key/IV must be provided as base64 strings.
        /// </summary>
        /// <param name="plainText">UTF-8 input plaintext.</param>
        /// <param name="base64Key">Base64 encoded key (16/24/32 bytes after decode).</param>
        /// <param name="base64IV">Base64 encoded IV (16 bytes after decode).</param>
        /// <returns>Uppercase hex string of ciphertext.</returns>
        public static string EncryptHex(string plainText, string base64Key, string base64IV)
        {
            if (plainText is null)
                throw new ArgumentNullException(nameof(plainText));

            byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);

            byte[] keyBytes = DecodeBase64Key(base64Key, nameof(base64Key));
            byte[] ivBytes = DecodeBase64IV(base64IV, nameof(base64IV));

            ValidateAesKeyIvLengths(keyBytes, ivBytes);

            try
            {
                using Aes aes = Aes.Create();
                aes.Key = keyBytes;
                aes.IV = ivBytes;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                using ICryptoTransform encryptor = aes.CreateEncryptor();
                byte[] cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
                return ConvertBytesToHex(cipherBytes);
            }
            catch (CryptographicException ex)
            {
                throw new CryptographicException("AES encryption failed.", ex);
            }
        }

        private static byte[] DecodeBase64Key(string base64Key, string paramName)
        {
            if (string.IsNullOrWhiteSpace(base64Key))
                throw new ArgumentException("Base64 key is required", paramName);
            try
            {
                return Convert.FromBase64String(base64Key);
            }
            catch (FormatException ex)
            {
                throw new ArgumentException("Invalid base64 key", paramName, ex);
            }
        }

        private static byte[] DecodeBase64IV(string base64IV, string paramName)
        {
            if (string.IsNullOrWhiteSpace(base64IV))
                throw new ArgumentException("Base64 IV is required", paramName);
            try
            {
                return Convert.FromBase64String(base64IV);
            }
            catch (FormatException ex)
            {
                throw new ArgumentException("Invalid base64 IV", paramName, ex);
            }
        }

        private static void ValidateAesKeyIvLengths(byte[] keyBytes, byte[] ivBytes)
        {
            if (ivBytes == null || ivBytes.Length != 16)
                throw new ArgumentException("IV must be 16 bytes for AES CBC.", nameof(ivBytes));

            if (keyBytes == null || (keyBytes.Length != 16 && keyBytes.Length != 24 && keyBytes.Length != 32))
                throw new ArgumentException("Key must be 16, 24 or 32 bytes (AES-128/192/256).", nameof(keyBytes));
        }

        // Convert HEX → Bytes (validates even length and hex chars)
        private static byte[] ConvertHexToBytes(string hex)
        {
            if (string.IsNullOrWhiteSpace(hex))
                return Array.Empty<byte>();

            if ((hex.Length & 1) != 0)
                throw new FormatException("Hex string must have an even length.");

            int len = hex.Length / 2;
            byte[] bytes = new byte[len];

            for (int i = 0, j = 0; i < hex.Length; i += 2, j++)
            {
                string pair = hex.Substring(i, 2);
                if (!byte.TryParse(pair, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte b))
                    throw new FormatException($"Invalid hex pair '{pair}' at position {i}.");
                bytes[j] = b;
            }

            return bytes;
        }

        // Convert Bytes → HEX (uppercase)
        private static string ConvertBytesToHex(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
                return string.Empty;

            StringBuilder sb = new StringBuilder(bytes.Length * 2);
            foreach (byte b in bytes)
                sb.AppendFormat("{0:X2}", b);
            return sb.ToString();
        }
    }
}