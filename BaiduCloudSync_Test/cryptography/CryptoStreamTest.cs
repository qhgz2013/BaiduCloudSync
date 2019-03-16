using System;
using System.IO;
using System.Security.Cryptography;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BaiduCloudSync_Test.cryptography
{
    [TestClass]
    public class CryptoStreamTest
    {
        [TestMethod]
        public void LegacyDynamicAESTest()
        {
            var test_file_in = Guid.NewGuid().ToString();
            var test_file_out = Guid.NewGuid().ToString();
            var rnd = new Random();
            var data = new byte[16384 + 1];
            rnd.NextBytes(data);
            var f_out = new FileStream(test_file_in, FileMode.Create, FileAccess.Write);
            f_out.Write(data, 0, data.Length);
            f_out.Close();
            f_out.Dispose();
            
            GlobalUtil.cryptography.Crypto.RSA_CreateKey(out var rsa_public, out var rsa_private, 4096);
            var sha1 = new GlobalUtil.hash.SHA1();
            sha1.TransformFinalBlock(data, 0, data.Length);
            var str_sha1 = GlobalUtil.Util.Hex(sha1.Hash);

#pragma warning disable CS0618
            FileEncrypt.EncryptFile(test_file_in, test_file_out, rsa_public, str_sha1);

            var f_in = new FileStream(test_file_out, FileMode.Open, FileAccess.Read);
            var decrypt_f_in = GlobalUtil.cryptography.CryptoStreamFactory.CreateCryptoStream(f_in, GlobalUtil.cryptography.EncryptionType.LegacyDynamicAES, CryptoStreamMode.Read, rsa_private);
#pragma warning restore

            try
            {
                var data_2 = GlobalUtil.Util.ReadBytes(decrypt_f_in, 16384 + 1);
                Assert.AreEqual(data.Length, data_2.Length);
                for (int i = 0; i < data.Length; i++)
                {
                    Assert.AreEqual(data[i], data_2[i]);
                }
            }
            finally
            {
                f_in.Close();
                f_in.Dispose();
            }

            File.Delete(test_file_in);
            File.Delete(test_file_out);
        }

        [TestMethod]
        public void LegacyStaticAESTest()
        {
            var test_file_in = Guid.NewGuid().ToString();
            var test_file_out = Guid.NewGuid().ToString();
            var rnd = new Random();
            var data = new byte[16384 + 1];
            rnd.NextBytes(data);
            var f_out = new FileStream(test_file_in, FileMode.Create, FileAccess.Write);
            f_out.Write(data, 0, data.Length);
            f_out.Close();
            f_out.Dispose();

            byte[] aes_key = new byte[32], aes_iv = new byte[16];
            rnd.NextBytes(aes_key);
            rnd.NextBytes(aes_iv);
            var sha1 = new GlobalUtil.hash.SHA1();
            sha1.TransformFinalBlock(data, 0, data.Length);
            var str_sha1 = GlobalUtil.Util.Hex(sha1.Hash);

#pragma warning disable CS0618
            FileEncrypt.EncryptFile(test_file_in, test_file_out, aes_key, aes_iv, str_sha1);

            var f_in = new FileStream(test_file_out, FileMode.Open, FileAccess.Read);
            var decrypt_f_in = GlobalUtil.cryptography.CryptoStreamFactory.CreateCryptoStream(f_in, GlobalUtil.cryptography.EncryptionType.LegacyStaticAES, CryptoStreamMode.Read, aes_key, aes_iv);
#pragma warning restore

            try
            {
                var data_2 = GlobalUtil.Util.ReadBytes(decrypt_f_in, 16384 + 1);
                Assert.AreEqual(data.Length, data_2.Length);
                for (int i = 0; i < data.Length; i++)
                {
                    Assert.AreEqual(data[i], data_2[i]);
                }
            }
            finally
            {
                f_in.Close();
                f_in.Dispose();
            }

            File.Delete(test_file_in);
            File.Delete(test_file_out);
        }

        [TestMethod]
        public void DynamicAESTest()
        {
            var data = new byte[65538];
            var random = new Random();
            random.NextBytes(data);
            var rsa = GlobalUtil.cryptography.Crypto.RSA_CreateKey(out var public_key, out var private_key);
            var stream_encrypt_read = new MemoryStream();
            var stream_encrypt_write = GlobalUtil.cryptography.CryptoStreamFactory.CreateCryptoStream(stream_encrypt_read, GlobalUtil.cryptography.EncryptionType.DynamicAES, CryptoStreamMode.Write, public_key)
                as GlobalUtil.cryptography.streamadapter.DynamicAESCryptoStream;
            stream_encrypt_write.Write(data, 0, data.Length);
            stream_encrypt_write.FlushFinalBlock();
            stream_encrypt_read.Seek(0, SeekOrigin.Begin);

            var stream_decrypt_read = GlobalUtil.cryptography.CryptoStreamFactory.CreateCryptoStream(stream_encrypt_read, GlobalUtil.cryptography.EncryptionType.DynamicAES, CryptoStreamMode.Read, private_key);

            var data_new = GlobalUtil.Util.ReadBytes(stream_decrypt_read, data.Length);
            Assert.AreEqual(data.Length, data_new.Length);
            for (int i = 0; i < data.Length; i++)
            {
                Assert.AreEqual(data[i], data_new[i]);
            }
        }
        
    }
}
