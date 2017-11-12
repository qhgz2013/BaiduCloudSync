//common-file-encrypt.cs
//
// 用于对文件进行加密的类
// 加密为固定RSA+可变AES 或者是固定AES
//
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Security.Cryptography;
namespace GlobalUtil
{
    public class FileEncrypt
    {
        //对小姐姐爱得深沉
        public const byte FLG_STATIC_KEY = 0x2b;
        public const byte FLG_DYNAMIC_KEY = 0xa2;
        /// <summary>
        /// 将文件利用RSA+AES进行动态加密
        /// </summary>
        /// <param name="inputFile">输入的文件路径</param>
        /// <param name="outputFile">输出的文件路径</param>
        /// <param name="rsaPublic">RSA公钥</param>
        /// <param name="SHA1">文件的SHA1值（可选，用于解密的校验）</param>
        public static void EncryptFile(string inputFile, string outputFile, byte[] rsaPublic, string SHA1 = null)
        {
            if (rsaPublic == null) throw new ArgumentNullException("rsaPublic");
            var fs_in = new FileStream(inputFile, FileMode.Open, FileAccess.Read, FileShare.Read);
            var fs_out = new FileStream(outputFile, FileMode.Create, FileAccess.Write, FileShare.None);
            var rsa = new RSACryptoServiceProvider();
            rsa.ImportCspBlob(rsaPublic);
            byte[] sha1_value = rsa.Encrypt(new byte[20], false);
            if (!string.IsNullOrEmpty(SHA1)) sha1_value = rsa.Encrypt(util.Hex(SHA1), false);
            var rnd = new Random();
            var aesKey = new byte[32];
            rnd.NextBytes(aesKey);
            var aesIV = new byte[16];
            rnd.NextBytes(aesIV);
            var aes_key_value = rsa.Encrypt(aesKey, false);
            var aes_iv_value = rsa.Encrypt(aesIV, false);

            fs_out.WriteByte(FLG_DYNAMIC_KEY);
            fs_out.Write(sha1_value, 0, sha1_value.Length);
            fs_out.Write(aes_key_value, 0, aes_key_value.Length);
            fs_out.Write(aes_iv_value, 0, aes_iv_value.Length);
            fs_out.Write(new byte[2], 0, 2); //preserved for latter usage

            fs_out.Flush();
            var encrypted_stream = Crypt.AES_StreamEncrypt(fs_out, aesKey, CipherMode.CFB, aesIV);
            int nread = 0;
            const int buffer_size = 4096;
            var buffer = new byte[buffer_size];
            do
            {
                nread = fs_in.Read(buffer, 0, buffer_size);
                encrypted_stream.Write(buffer, 0, nread);
            } while (nread != 0);
            encrypted_stream.FlushFinalBlock();
            fs_in.Close();
            encrypted_stream.Close();
        }
        /// <summary>
        /// 将文件利用AES进行静态加密
        /// </summary>
        /// <param name="inputFile">输入的文件路径</param>
        /// <param name="outputFile">输出的文件路径</param>
        /// <param name="aesKey">AES密钥</param>
        /// <param name="aesIV">AES初始向量</param>
        /// <param name="SHA1">文件的SHA1值</param>
        public static void EncryptFile(string inputFile, string outputFile, byte[] aesKey, byte[] aesIV, string SHA1 = null)
        {
            var fs_in = new FileStream(inputFile, FileMode.Open, FileAccess.Read, FileShare.Read);
            var fs_out = new FileStream(outputFile, FileMode.Create, FileAccess.Write, FileShare.None);
            if (aesKey == null)
                throw new ArgumentNullException("aesKey");
            if (aesIV == null)
                throw new ArgumentNullException("aesIV");
            if (!string.IsNullOrEmpty(SHA1) && SHA1.Length != 40)
                throw new ArgumentException("Invalid SHA1 Checksum");

            fs_out.WriteByte(FLG_STATIC_KEY);
            fs_out.Write(new byte[2], 0, 2); //preserved for latter usage
            fs_out.Flush();

            var encrypted_stream = Crypt.AES_StreamEncrypt(fs_out, aesKey, CipherMode.CFB, aesIV);
            var sha1_value = new byte[20];
            if (!string.IsNullOrEmpty(SHA1)) sha1_value = util.Hex(SHA1);
            encrypted_stream.Write(sha1_value, 0, 20);

            int nread = 0;
            const int buffer_size = 4096;
            var buffer = new byte[buffer_size];
            do
            {
                nread = fs_in.Read(buffer, 0, buffer_size);
                encrypted_stream.Write(buffer, 0, nread);
            } while (nread != 0);
            encrypted_stream.FlushFinalBlock();
            fs_in.Close();
            encrypted_stream.Close();
        }
        /// <summary>
        /// 将文件利用RSA+AES进行动态解密
        /// </summary>
        /// <param name="inputFile">输入的文件路径</param>
        /// <param name="outputFile">输出的文件路径</param>
        /// <param name="rsaPrivate">RSA密钥</param>
        public static void DecryptFile(string inputFile, string outputFile, byte[] rsaPrivate)
        {
            if (rsaPrivate == null) throw new ArgumentNullException("rsaPrivate");
            var fs_in = new FileStream(inputFile, FileMode.Open, FileAccess.Read, FileShare.Read);
            var fs_out = new FileStream(outputFile, FileMode.Create, FileAccess.Write, FileShare.None);
            var rsa = new RSACryptoServiceProvider();
            rsa.ImportCspBlob(rsaPrivate);

            int type = fs_in.ReadByte();
            if (type != FLG_DYNAMIC_KEY)
            {
                fs_in.Close();
                fs_out.Close();
                throw new InvalidDataException("格式错误：该文件不是采用动态加密的文件");
            }
            var sha1_value = util.ReadBytes(fs_in, rsa.KeySize / 8);
            var aes_key_value = util.ReadBytes(fs_in, rsa.KeySize / 8);
            var aes_iv_value = util.ReadBytes(fs_in, rsa.KeySize / 8);
            var SHA1 = rsa.Decrypt(sha1_value, false);
            var aesKey = rsa.Decrypt(aes_key_value, false);
            var aesIV = rsa.Decrypt(aes_iv_value, false);
            var preserved = util.ReadBytes(fs_in, 2); //preserved for latter usage

            var decrypted_stream = Crypt.AES_StreamDecrypt(fs_in, aesKey, CipherMode.CFB, aesIV);
            int nread = 0;
            const int buffer_size = 4096;
            var buffer = new byte[buffer_size];
            var sha1 = new SHA1CryptoServiceProvider();
            do
            {
                nread = decrypted_stream.Read(buffer, 0, buffer_size);
                fs_out.Write(buffer, 0, nread);
                sha1.TransformBlock(buffer, 0, nread, buffer, 0);
            } while (nread != 0);
            sha1.TransformFinalBlock(buffer, 0, 0);
            var cur_sha1 = sha1.Hash;
            fs_out.Close();
            decrypted_stream.Close();
            var sha1_empty = new byte[20];
            if (util.Hex(cur_sha1) != util.Hex(SHA1) && util.Hex(SHA1) != util.Hex(sha1_empty))
            {
                throw new InvalidDataException("SHA1检验不匹配：解密失败");
            }
        }
        /// <summary>
        /// 将文件利用AES进行静态解密
        /// </summary>
        /// <param name="inputFile">输入的文件路径</param>
        /// <param name="outputFile">输出的文件路径</param>
        /// <param name="aesKey">AES密钥</param>
        /// <param name="aesIV">AES初始向量</param>
        public static void DecryptFile(string inputFile, string outputFile, byte[] aesKey, byte[] aesIV)
        {
            if (aesKey == null) throw new ArgumentNullException("aesKey");
            if (aesIV == null) throw new ArgumentNullException("aesIV");
            var fs_in = new FileStream(inputFile, FileMode.Open, FileAccess.Read, FileShare.Read);
            var fs_out = new FileStream(outputFile, FileMode.Create, FileAccess.Write, FileShare.None);

            int type = fs_in.ReadByte();
            if (type != FLG_STATIC_KEY)
            {
                fs_in.Close();
                fs_out.Close();
                throw new InvalidDataException("格式错误：该文件不是采用静态加密的文件");
            }
            var preserved = util.ReadBytes(fs_in, 2); //preserved for latter usage

            var decrypted_stream = Crypt.AES_StreamDecrypt(fs_in, aesKey, CipherMode.CFB, aesIV);
            int nread = 0;
            const int buffer_size = 4096;
            var buffer = new byte[buffer_size];
            var sha1 = new SHA1CryptoServiceProvider();
            byte[] SHA1 = util.ReadBytes(decrypted_stream, 20);
            do
            {
                nread = decrypted_stream.Read(buffer, 0, buffer_size);
                fs_out.Write(buffer, 0, nread);
                sha1.TransformBlock(buffer, 0, nread, buffer, 0);
            } while (nread != 0);
            sha1.TransformFinalBlock(buffer, 0, 0);
            var cur_sha1 = sha1.Hash;
            fs_out.Close();
            decrypted_stream.Close();
            var sha1_empty = new byte[20];
            if (util.Hex(cur_sha1) != util.Hex(SHA1) && util.Hex(SHA1) != util.Hex(sha1_empty))
            {
                throw new InvalidDataException("SHA1检验不匹配：解密失败");
            }
        }
    }
}
