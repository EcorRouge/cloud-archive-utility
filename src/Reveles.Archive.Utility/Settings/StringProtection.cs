﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Reveles.Archive.Utility.Settings
{
    public class StringProtection
    {
        private const string ENCRYPTION_KEY = "rEvEle%$3a^2021!Encrptn$Key@h!@#";

        public static string EncryptString(string text)
        {
            var key = Encoding.UTF8.GetBytes(ENCRYPTION_KEY);

            using (var aesAlg = Aes.Create())
            {
                using (var encryptor = aesAlg.CreateEncryptor(key, aesAlg.IV))
                {
                    using (var msEncrypt = new MemoryStream())
                    {
                        using (var csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
                        using (var swEncrypt = new StreamWriter(csEncrypt))
                        {
                            swEncrypt.Write(text);
                        }

                        var iv = aesAlg.IV;

                        var encryptedContent = msEncrypt.ToArray();

                        var result = new byte[iv.Length + encryptedContent.Length];

                        Buffer.BlockCopy(iv, 0, result, 0, iv.Length);
                        Buffer.BlockCopy(encryptedContent, 0, result, iv.Length, encryptedContent.Length);

                        return Convert.ToBase64String(result);
                    }
                }
            }
        }

        public static string DecryptString(string encryptedText)
        {
            var encryptedMessageBytes = Convert.FromBase64String(encryptedText);

            var iv = new byte[16];
            var encryptedTextBytes = new byte[encryptedMessageBytes.Length - iv.Length];

            Buffer.BlockCopy(encryptedMessageBytes, 0, iv, 0, iv.Length);
            Buffer.BlockCopy(encryptedMessageBytes, iv.Length, encryptedTextBytes, 0, encryptedTextBytes.Length);

            var key = Encoding.UTF8.GetBytes(ENCRYPTION_KEY);

            using (var aesAlg = Aes.Create())
            {
                using (var decryptor = aesAlg.CreateDecryptor(key, iv))
                {
                    string result;
                    using (var msDecrypt = new MemoryStream(encryptedTextBytes))
                    {
                        using (var csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
                        {
                            using (var srDecrypt = new StreamReader(csDecrypt))
                            {
                                result = srDecrypt.ReadToEnd();
                            }
                        }
                    }

                    return result;
                }
            }
        }

    }
}
