using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Security.Cryptography;

namespace GlobalUtil
{
    //AES 128/192/256 bit CBC/CFB/CTS/ECB/OFB
    public partial class Crypt
    {
        /// <summary>
        /// Create an encrypted data stream using the specified key and mode and IV
        /// Remember to FlushFinalBlock() when finished encryption
        /// </summary>
        /// <param name="srcData">Source Data Stream</param>
        /// <param name="key">AES Key (128 or 192 or 256bit only)</param>
        /// <param name="mode">Cipher Mode</param>
        /// <param name="IV">AES Initial Vector(IV) (128bit only)</param>
        /// <returns>Encrypted Stream</returns>
        public static CryptoStream AES_StreamEncrypt(Stream srcData, byte[] key, CipherMode mode, byte[] IV)
        {
            if (key.Length != 32 && key.Length != 24 && key.Length != 16) throw new ArgumentException("Key length mismatch! support aes-128, aes-192, aes-256 only");
            var rm = new RijndaelManaged();
            rm.Key = key;
            rm.Mode = mode;
            rm.Padding = PaddingMode.PKCS7;
            //rm.KeySize = key.Length * 8;
            if (IV != null && IV.Length != 16) throw new ArgumentException("IV only support 128 bit(16 bytes length)");
            rm.IV = IV;

            var encrypt_stream = new CryptoStream(srcData, rm.CreateEncryptor(), CryptoStreamMode.Write);
            return encrypt_stream;
        }
        /// <summary>
        /// Create a decrypted data stream using the specified key and mode and IV
        /// </summary>
        /// <param name="encData">Encrypted Data Stream</param>
        /// <param name="key">AES Key (128 or 192 or 256bit only)</param>
        /// <param name="mode">Cipher Mode</param>
        /// <param name="IV">AES Initial Vector(IV) (128bit only)</param>
        /// <returns>Decrypted Stream</returns>
        public static CryptoStream AES_StreamDecrypt(Stream encData, byte[] key, CipherMode mode, byte[] IV)
        {
            if (key.Length != 32 && key.Length != 24 && key.Length != 16) throw new ArgumentException("Key length mismatch! support aes-128, aes-192, aes-256 only");
            var rm = new RijndaelManaged();
            rm.Key = key;
            rm.Mode = mode;
            rm.Padding = PaddingMode.PKCS7;
            //rm.KeySize = key.Length * 8;
            if (IV != null && IV.Length != 16) throw new ArgumentException("IV only support 128 bit(16 bytes length)");
            if (IV != null) rm.IV = IV;
            //if (rm.IV == null)
            //{
            //    rm.IV = util.ReadBytes(encData, 16);
            //}

            var decrypt_stream = new CryptoStream(encData, rm.CreateDecryptor(), CryptoStreamMode.Read);
            return decrypt_stream;
        }
    }
}
