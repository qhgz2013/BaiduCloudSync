using GlobalUtil.cryptography.streamadapter;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace GlobalUtil.cryptography
{
    public static class CryptoStreamFactory
    {
        public static Stream CreateCryptoStream(Stream upstream, EncryptionType encryption_type, CryptoStreamMode stream_mode, RSAParameters rsa_parameters)
        {
            switch (encryption_type)
            {
                case EncryptionType.DynamicAES:
                    throw new NotImplementedException();
                case EncryptionType.StaticAES:
                    throw new NotSupportedException("could not create a static aes stream by using rsa parameters");
#pragma warning disable CS0618
                case EncryptionType.LegacyDynamicAES:
                    return new LegacyDynamicAESCryptoStream(upstream, stream_mode, rsa_parameters);
                case EncryptionType.LegacyStaticAES:
                    throw new NotSupportedException("could not create a static aes stream by using rsa parameters");
#pragma warning restore
                default:
                    throw new NotImplementedException($"No implemented stream adapter found for type {encryption_type.ToString()}");
            }
        }

        public static Stream CreateCryptoStream(Stream upstream, EncryptionType encryption_type, CryptoStreamMode stream_mode, byte[] aes_key, byte[] aes_iv)
        {
            switch (encryption_type)
            {
                case EncryptionType.DynamicAES:
                    throw new NotSupportedException("could not create a dynamic aes stream by using aes key");
                case EncryptionType.StaticAES:
                    throw new NotImplementedException();
#pragma warning disable CS0618
                case EncryptionType.LegacyDynamicAES:
                    throw new NotSupportedException("could not create a dynamic aes stream by using aes key");
                case EncryptionType.LegacyStaticAES:
                    return new LegacyStaticAESCryptoStream(upstream, stream_mode, aes_key, aes_iv);
#pragma warning restore
                default:
                    throw new NotImplementedException($"No implemented stream adapter found for type {encryption_type.ToString()}");
            }
        }
    }
}
