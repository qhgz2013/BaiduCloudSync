using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Security.Cryptography;

namespace GlobalUtil.cryptography
{
    //RSA 1024/2048/3072/4096/... bit
    public partial class Crypto
    {
        /// <summary>
        /// 随机生成一对RSA的公钥和私钥
        /// </summary>
        /// <param name="publicKey">公钥</param>
        /// <param name="privateKey">私钥</param>
        /// <param name="keysize">密钥大小（默认为1024bit）</param>
        /// <returns>生成是否成功</returns>
        public static bool RSA_CreateKey(out RSAParameters publicKey, out RSAParameters privateKey, int keysize = 1024)
        {
            try
            {
                var rsa = new RSACryptoServiceProvider(keysize);
                publicKey = rsa.ExportParameters(false);
                privateKey = rsa.ExportParameters(true);
                return true;
            }
            catch (Exception)
            {
                publicKey = new RSAParameters();
                privateKey = new RSAParameters();
                return false;
            }
        }
        /// <summary>
        /// 将RSA公钥参数转换为PEM字符串
        /// </summary>
        /// <param name="publicKey">RSA公钥参数</param>
        /// <returns>PEM字符串</returns>
        public static string RSA_ExportPEMPublicKey(RSAParameters publicKey)
        {
            //https://stackoverflow.com/questions/23734792/c-sharp-export-private-public-rsa-key-from-rsacryptoserviceprovider-to-pem-strin
            var sb = new StringBuilder();
            using (var stream = new MemoryStream())
            {
                stream.WriteByte(0x30); //SEQUENCE
                using (var ms = new MemoryStream())
                {
                    _encode_integer_big_edian(ms, publicKey.Modulus);
                    _encode_integer_big_edian(ms, publicKey.Exponent);
                    var len = (int)ms.Length;
                    _encode_length(stream, len);
                    stream.Write(ms.GetBuffer(), 0, len);
                }
                var base64 = Convert.ToBase64String(stream.GetBuffer(), 0, (int)stream.Length).ToCharArray();
                sb.AppendLine("-----BEGIN PUBLIC KEY-----");
                for (int i = 0; i < base64.Length; i += 64)
                {
                    sb.AppendLine(new string(base64, i, Math.Min(64, base64.Length - i)));
                }
                sb.AppendLine("-----END PUBLIC KEY-----");
            }
            return sb.ToString();
        }
        /// <summary>
        /// 将RSA私钥参数转换为PEM字符串
        /// </summary>
        /// <param name="privateKey">RSA公钥参数</param>
        /// <returns>PEM字符串</returns>
        public static string RSA_ExportPEMPrivateKey(RSAParameters privateKey)
        {
            var rsa = new RSACryptoServiceProvider();
            rsa.ImportParameters(privateKey);
            var param = rsa.ExportParameters(true);
            if (rsa.PublicOnly) throw new ArgumentException("the key data is not a private key");
            var sb = new StringBuilder();
            using (var stream = new MemoryStream())
            {
                stream.WriteByte(0x30); //SEQUENCE
                using (var ms = new MemoryStream())
                {
                    _encode_integer_big_edian(ms, new byte[] { 0 }); //Version
                    _encode_integer_big_edian(ms, param.Modulus);
                    _encode_integer_big_edian(ms, param.Exponent);
                    _encode_integer_big_edian(ms, param.D);
                    _encode_integer_big_edian(ms, param.P);
                    _encode_integer_big_edian(ms, param.Q);
                    _encode_integer_big_edian(ms, param.DP);
                    _encode_integer_big_edian(ms, param.DQ);
                    _encode_integer_big_edian(ms, param.InverseQ);
                    var len = (int)ms.Length;
                    _encode_length(stream, len);
                    stream.Write(ms.GetBuffer(), 0, len);
                }
                var base64 = Convert.ToBase64String(stream.GetBuffer(), 0, (int)stream.Length).ToCharArray();
                sb.AppendLine("-----BEGIN RSA PRIVATE KEY-----");
                for (int i = 0; i < base64.Length; i += 64)
                {
                    sb.AppendLine(new string(base64, i, Math.Min(64, base64.Length - i)));
                }
                sb.AppendLine("-----END RSA PRIVATE KEY-----");
            }
            return sb.ToString();
        }
        /// <summary>
        /// 从PEM key中导入RSA公钥参数
        /// </summary>
        /// <param name="publicPEMKey">PEM字符串</param>
        /// <returns>RSA公钥参数</returns>
        public static RSAParameters RSA_ImportPEMPublicKey(string publicPEMKey)
        {
            const string header = "-----BEGIN PUBLIC KEY-----";
            const string header2 = "-----BEGIN RSA PUBLIC KEY-----";
            const string footer = "-----END PUBLIC KEY-----";
            const string footer2 = "-----END RSA PUBLIC KEY-----";
            const string private_key_header = "-----BEGIN RSA PRIVATE KEY-----";
            const string private_key_footer = "-----END RSA PRIVATE KEY-----";

            publicPEMKey = publicPEMKey.Replace("\r", "").Replace("\n", "");
            bool use_2 = false;
            bool use_private = false;
            int start = publicPEMKey.IndexOf(header);
            if (start == -1) { start = publicPEMKey.IndexOf(header2); use_2 = true; }
            //falling to using private key to export
            if (start == -1) { use_2 = false; use_private = true; start = publicPEMKey.IndexOf(private_key_header); }
            if (start == -1) { throw new ArgumentException("Expected " + header); }
            start += (use_2) ? header2.Length : (use_private ? private_key_header.Length : header.Length);

            int end = publicPEMKey.IndexOf(use_2 ? footer2 : (use_private ? private_key_footer : footer));
            if (end == -1) throw new ArgumentException("Expected " + (use_2 ? footer2 : (use_private ? private_key_footer : footer)));

            var base64 = publicPEMKey.Substring(start, (end - start));
            var data = Convert.FromBase64String(base64);

            try
            {
                return DerParser.ParseDERPublicKeyPKCS8(data);
            }
            catch
            {
                try
                {
                    return DerParser.ParseDERPublicKeyPKCS1(data);
                }
                catch { }
            }
            throw new ArgumentException("could not parse RSA PEM key");
        }
        /// <summary>
        /// 从PEM key中导入RSA私钥参数
        /// </summary>
        /// <param name="privatePEMKey">PEM字符串</param>
        /// <returns>RSA私钥参数</returns>
        public static RSAParameters RSA_ImportPEMPrivateKey(string privatePEMKey)
        {
            const string header = "-----BEGIN RSA PRIVATE KEY-----"; //pkcs#1 format
            const string footer = "-----END RSA PRIVATE KEY-----";
            const string header2 = "-----BEGIN PRIVATE KEY-----"; //pkcs#8 format
            const string footer2 = "-----END PRIVATE KEY-----";
            privatePEMKey = privatePEMKey.Replace("\r", "").Replace("\n", "");

            bool use_2 = false;
            int start = privatePEMKey.IndexOf(header);
            if (start == -1) { start = privatePEMKey.IndexOf(header2); use_2 = true; }
            int end = privatePEMKey.IndexOf(use_2 ? footer2 : footer);
            if (end == -1) throw new ArgumentException("Expected " + (use_2 ? footer2 : footer));
            start += use_2 ? header2.Length : header.Length;

            var base64 = privatePEMKey.Substring(start, (end - start));
            var data = Convert.FromBase64String(base64);
            return use_2 ? DerParser.ParseDERPrivateKeyPKCS8(data): DerParser.ParseDERPrivateKeyPKCS1(data);
            
        }

        #region private static functions for PEM encode / decode
        private static void _encode_length(Stream s, int len)
        {
            if (len < 0) throw new ArgumentOutOfRangeException("length", "Length must be non-negative");
            if (len < 0x80)
            {
                //short form
                s.WriteByte((byte)len);
            }
            else
            {
                //long form
                var temp = len;
                var bytesRequired = 0;
                while (temp > 0)
                {
                    temp >>= 8;
                    bytesRequired++;
                }
                s.WriteByte((byte)(bytesRequired | 0x80));
                for (int i = bytesRequired - 1; i >= 0; i--)
                {
                    s.WriteByte((byte)(len >> (8 * i) & 0xff));
                }
            }
        }
        private static void _encode_integer_big_edian(Stream s, byte[] value, bool force_unsigned = true)
        {
            s.WriteByte(0x02); //INTEGER
            var prefixZeros = 0;
            for (int i = 0; i < value.Length; i++)
            {
                if (value[i] != 0) break;
                prefixZeros++;
            }
            if (value.Length == prefixZeros)
            {
                _encode_length(s, 1);
                s.WriteByte(0);
            }
            else
            {
                if (force_unsigned && value[prefixZeros] > 0x7f)
                {
                    //add a prefix zero to force unsigned if the MSB is 1
                    _encode_length(s, value.Length - prefixZeros + 1);
                    s.WriteByte(0);
                }

                else
                {
                    _encode_length(s, value.Length - prefixZeros);
                }
                for (int i = prefixZeros; i < value.Length; i++)
                {
                    s.WriteByte(value[i]);
                }
            }
        }
        #endregion

        /// <summary>
        /// 用指定的RSA公钥加密字节数组
        /// </summary>
        /// <param name="srcData">原字节数组</param>
        /// <param name="publicKey">RSA公钥</param>
        /// <returns>加密后的字节数组</returns>
        public static byte[] RSA_ArrayEncrypt(byte[] srcData, RSAParameters publicKey)
        {
            var rsa = new RSACryptoServiceProvider();
            rsa.ImportParameters(publicKey);
            return rsa.Encrypt(srcData, false);
        }
        /// <summary>
        /// 用指定的RSA私钥解密字节数组
        /// </summary>
        /// <param name="encData">加密的字节数组</param>
        /// <param name="privateKey">RSA私钥</param>
        /// <returns>解密后的字节数组</returns>
        public static byte[] RSA_ArrayDecrypt(byte[] encData, RSAParameters privateKey)
        {
            var rsa = new RSACryptoServiceProvider();
            rsa.ImportParameters(privateKey);
            return rsa.Decrypt(encData, false);
        }
        /// <summary>
        /// 用指定的RSA公钥加密字符串
        /// </summary>
        /// <param name="srcData">原字符串</param>
        /// <param name="publicKey">RSA公钥</param>
        /// <param name="charset">字符串编码（默认为utf8）</param>
        /// <returns>加密后的字节数组</returns>
        public static byte[] RSA_StringEncrypt(string srcData, RSAParameters publicKey, Encoding charset = null)
        {
            if (charset == null) charset = Encoding.UTF8;
            var data = charset.GetBytes(srcData);
            return RSA_ArrayEncrypt(data, publicKey);
        }
        /// <summary>
        /// 用指定的RSA公钥解密字符串
        /// </summary>
        /// <param name="encData">加密后的数据</param>
        /// <param name="privateKey">RSA私钥</param>
        /// <param name="charset">字符串编码（默认为utf8）</param>
        /// <returns>解密后的字符串</returns>
        public static string RSA_StringDecrypt(byte[] encData, RSAParameters privateKey, Encoding charset = null)
        {
            if (charset == null) charset = Encoding.UTF8;
            var data = RSA_ArrayDecrypt(encData, privateKey);
            return charset.GetString(data);
        }
    }
}
