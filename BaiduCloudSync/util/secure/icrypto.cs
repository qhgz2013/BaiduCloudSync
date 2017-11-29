using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace GlobalUtil
{
    /// <summary>
    /// 定义接口，实现文件的加密的解密
    /// </summary>
    public interface ICrypto
    {
        bool EnableCrypto { get; set; }
        byte[] RSAPublicKey { get; }
        byte[] RSAPrivateKey { get; }
        bool IsDynamicEncryption { get; }
        bool IsStaticEncryption { get; }
        byte[] AESKey { get; }
        byte[] AESIV { get; }
        void LoadKey(string path);
        void SaveKey(string path);
    }
}
