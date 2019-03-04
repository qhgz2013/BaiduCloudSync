using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Security.Cryptography;

namespace GlobalUtil.cryptography
{
    //AES 128/192/256 bit CBC/CFB/CTS/ECB/OFB
    public partial class Crypto
    {
        /// <summary>
        /// 使用指定的AES key和初始向量（IV）创建一个加密的数据流
        /// 在完成加密后记得调用 FlushFinalBlock()
        /// </summary>
        /// <param name="srcData">原数据流</param>
        /// <param name="key">AES Key（仅支持128/192/256 bit大小）</param>
        /// <param name="mode">加密模式</param>
        /// <param name="IV">AES初始向量（IV）（仅支持128bit）</param>
        /// <returns>加密的数据流</returns>
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
        /// 使用指定的AES key和初始向量（IV）创建一个解密的数据流
        /// </summary>
        /// <param name="encData">加密过的数据流</param>
        /// <param name="key">AES Key（仅支持128/192/256 bit大小）</param>
        /// <param name="mode">加密模式</param>
        /// <param name="IV">AES初始向量（IV）（仅支持128bit）</param>
        /// <returns>解密的数据流</returns>
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
