using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace GlobalUtil
{
    public class KeyManager
    {
        private byte[] _aesIv;
        private byte[] _aesKey;
        private byte[] _rsaPublic;
        private byte[] _rsaPrivate;
        private bool _enableEncryption;
        private bool _hasRsaKey;
        private bool _hasAesKey;
        private bool _encryptionType;
        /// <summary>
        /// AES加密的初始向量(IV)
        /// </summary>
        public byte[] AESIV
        {
            get
            {
                return _aesIv;
            }
        }
        /// <summary>
        /// AES密钥
        /// </summary>
        public byte[] AESKey
        {
            get
            {
                return _aesKey;
            }
        }
        /// <summary>
        /// 是否开启加密（需要至少拥有AES或者RSA其中一个密钥）
        /// </summary>
        public bool EnableCrypto
        {
            get
            {
                return _enableEncryption;
            }

            set
            {
                if (_hasAesKey || _hasRsaKey)
                    _enableEncryption = value;
                else
                    _enableEncryption = false;
            }
        }

        /// <summary>
        /// 是否为动态加密
        /// </summary>
        public bool IsDynamicEncryption
        {
            get
            {
                return _hasRsaKey && _encryptionType;
            }
            set
            {
                if (_hasRsaKey && value)
                    _encryptionType = true;
                else if (_hasAesKey && !value)
                    _encryptionType = false;
            }
        }

        /// <summary>
        /// 是否为静态加密
        /// </summary>
        public bool IsStaticEncryption
        {
            get
            {
                return _hasAesKey && !_enableEncryption;
            }
            set
            {
                if (_hasRsaKey && value)
                    _encryptionType = true;
                else if (_hasAesKey && !value)
                    _encryptionType = false;
            }
        }

        /// <summary>
        /// RSA私钥
        /// </summary>
        public byte[] RSAPrivateKey
        {
            get
            {
                return _rsaPrivate;
            }
        }

        /// <summary>
        /// RSA公钥
        /// </summary>
        public byte[] RSAPublicKey
        {
            get
            {
                return _rsaPublic;
            }
        }

        /// <summary>
        /// 读取密钥文件
        /// </summary>
        /// <param name="path">密钥路径</param>
        public void LoadKey(string path)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentNullException("path");
            if (!File.Exists(path)) throw new InvalidDataException("File not exists");
            if (path.EndsWith(".pem"))
            {
                //rsa pem file
                var file_data = File.ReadAllText(path);
                try
                {
                    var rsa_data = Crypto.RSA_ImportPEMPrivateKey(file_data);
                    _rsaPrivate = rsa_data;
                    var rsa = new System.Security.Cryptography.RSACryptoServiceProvider();
                    rsa.ImportCspBlob(rsa_data);
                    _rsaPublic = rsa.ExportCspBlob(false);
                    _hasRsaKey = true;
                }
                catch (Exception)
                {

                }
            }
            else
            {
                //aes file data
                var file_data = File.ReadAllText(path);
                if (file_data.Length == 96)
                {
                    try
                    {
                        var array = util.Hex(file_data);
                        _aesKey = new byte[32];
                        _aesIv = new byte[16];
                        Array.Copy(array, 0, _aesKey, 0, 32);
                        Array.Copy(array, 32, _aesIv, 0, 16);
                        _hasAesKey = true;
                    }
                    catch (Exception)
                    {

                    }
                }

            }
        }

        /// <summary>
        /// 写入密钥文件
        /// </summary>
        /// <param name="path">密钥路径</param>
        public void SaveKey(string path)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentNullException("path");
            if (_hasRsaKey)
            {
                var rsa_data = Crypto.RSA_ExportPEMPrivateKey(_rsaPrivate);
                File.WriteAllText(path, rsa_data);
            }
            else if (_hasAesKey)
            {
                var aes_data = new byte[48];
                Array.Copy(_aesKey, 0, aes_data, 0, 32);
                Array.Copy(_aesIv, 0, aes_data, 32, 16);
                File.WriteAllText(path, util.Hex(aes_data));
            }
        }

        /// <summary>
        /// 新建密钥数据（会覆盖掉原密钥）
        /// </summary>
        /// <param name="genRsaKey">生成RSA密钥为true，AES密钥为false</param>
        public void CreateKey(bool genRsaKey = true)
        {
            if (genRsaKey)
            {
                Crypto.RSA_CreateKey(out _rsaPublic, out _rsaPrivate, 2048);
                _hasRsaKey = true;
                if (!_hasAesKey)
                    _encryptionType = true;
            }
            else
            {
                var rnd = new Random();
                _aesKey = new byte[32];
                _aesIv = new byte[16];
                rnd.NextBytes(_aesKey);
                rnd.NextBytes(_aesIv);
                _hasAesKey = true;
                if (!_hasRsaKey)
                    _encryptionType = true;
            }
        }
    }
}
