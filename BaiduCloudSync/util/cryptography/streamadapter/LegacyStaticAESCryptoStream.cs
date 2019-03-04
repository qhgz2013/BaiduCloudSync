using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace GlobalUtil.cryptography.streamadapter
{
    /// <summary>
    /// 提供仅兼容v1.0加密文件的解密数据流
    /// </summary>
    [Obsolete("This class implemented LegacyStaticAES encryption method, which is no longer maintain and use")]
    public sealed class LegacyStaticAESCryptoStream : Stream
    {
        private hash.SHA1 _sha1_hash;
        private Stream _decryptor_stream;
        private readonly byte[] _aes_key;
        private readonly byte[] _aes_iv;
        private readonly byte[] _sha1_checksum;
        public LegacyStaticAESCryptoStream(Stream upstream, CryptoStreamMode mode, byte[] aes_key, byte[] aes_iv)
        {
            if (mode == CryptoStreamMode.Write)
                throw new NotSupportedException("Write mode of LegacyDynamicAES is no longer supported");
            if (upstream == null)
                throw new ArgumentNullException("upstream");
            if (!upstream.CanRead)
                throw new ArgumentException("upstream is not readable");
            if (aes_key == null || aes_key.Length == 0)
                throw new ArgumentNullException("aes_key");
            if (aes_iv == null || aes_iv.Length == 0)
                throw new ArgumentNullException("aes_iv");
            _aes_key = aes_key;
            _aes_iv = aes_iv;

            long? original_position = null;
            try
            {
                original_position = upstream.Position;
            }
            catch (Exception) { }

            try
            {
                // offset / length [data type] - description

                // 0 / 1 [byte] - file marker (constant value: 0x2b)
                var file_marker = Util.ReadBytes(upstream, 1);
                if (file_marker == null || file_marker.Length == 0)
                    throw new FormatException("unexpected end of stream");
                if (file_marker[0] != 0x2b)
                    throw new FormatException($"incorrect file marker, expected {0x2b} but got {file_marker[0]}");

                // 1 / 2 [ushort] - preserved area, constant 0, added in protocol rev 1.
                var preserved = Util.ReadBytes(upstream, 2);
                if (preserved == null || preserved.Length < 2)
                    throw new FormatException("unexpected end of stream");
                if (preserved[0] != 0 || preserved[1] != 0)
                    Tracer.GlobalTracer.TraceWarning("Preserve field should be 0");

                _decryptor_stream = Crypto.AES_StreamDecrypt(upstream, _aes_key, CipherMode.CFB, _aes_iv);
                // 3 / 20 [byte[20]] - encrypted sha1
                _sha1_checksum = Util.ReadBytes(_decryptor_stream, 20);

                // 23 / * [byte array] - data
            }
            catch (Exception)
            {
                if (original_position != null && upstream.CanSeek)
                    upstream.Seek(original_position.Value, SeekOrigin.Begin);
            }
            _sha1_hash = new hash.SHA1();
        }
        public override bool CanRead => _decryptor_stream.CanRead;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int readed_bytes = _decryptor_stream.Read(buffer, offset, count);
            if (readed_bytes == 0)
            {
                _sha1_hash.TransformFinalBlock(buffer, 0, 0);
                var sha1_expected = Util.Hex(_sha1_checksum);
                var sha1_got = Util.Hex(_sha1_hash.Hash);

                if (sha1_expected != Util.Hex(hash.SHA1.Empty) && sha1_expected != sha1_got)
                    Tracer.GlobalTracer.TraceWarning($"SHA1 inconsistency detected while decrypting data: expected {sha1_expected}, but got {sha1_got}");
            }
            else
                _sha1_hash.TransformBlock(buffer, offset, count);
            return readed_bytes;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }
    }
}
