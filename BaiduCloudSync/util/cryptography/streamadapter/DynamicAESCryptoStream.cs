using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace GlobalUtil.cryptography.streamadapter
{
    /* FILE LAYOUT
     * 
     * OFFSET | DATA LENGTH (in bytes) [DATA TYPE] - DESCRIPTION
     * 
     * META DATA (no encryption)
     * 
     * 0 | 4 [string] - File descriptor : constant "BCSD", Baidu Cloud Secure Data
     * 4 | 2 [ushort] - Version : constant 0x0001 (ver 1)
     * 6 | 2 [ushort] - Revision : current value 0x0001 (rev 1)
     * 8 | 4 [int] - Preserved field, constant 0x0
     * 12 | 4 [int] - Key flags
     * 
     * Key flags:
     * 0000 0000 | 0000 0000 | 0000 0000 | 0000 xxxx
     *                                          ||||- is key encrypted using RSA
     *                                          |||- has AES iv SHA1 checksum
     *                                          ||- has AES key SHA1 checksum
     *                                          |- has data SHA1 checksum
     * <base offset> = 16
     * if (is key encrypted using RSA)
     *     <base offset> | 4 [int] - key length (in bytes)
     *     <base offset> + 4 | <key length> [RSA encrypted data] - RSA encrypted AES key
     *     <base offset> + 4 + <key length> | <key length> [RSA encrypted data] - RSA encrypted AES IV
     *     <base offset> = <base offset> + 4 + <key length> * 2
     *     
     * if (has AES key checksum)
     *     <base offset> | 20 [SHA1 checksum] - AES key checksum
     *     <base offset> = <base offset> + 20
     * 
     * if (has AES iv checksum)
     *     <base offset> | 20 [SHA1 checksum] - AES iv checksum
     *     <base offset> = <base offset> + 20
     * 
     * if (has data SHA1 checksum)
     *     <base offset> | 4 [int] - data block size (in bytes), the SHA1 checksum will be written to file after written a data block
     *     <base offset> = <base offset> + 4
     * 
     * ENCRYPTION DATA (the CryptoStream is created here)
     * 
     * while (NOT EOF)
     *     <base offset> | <data block size> [byte array] - original data
     *     <base offset> = <base offset> + <data block size>
     *     if (has data SHA1 checksum)
     *         <base offset> | 20 [SHA1 checksum] - block checksum
     *         <base offset> = <base offset> + 20
     */

    /// <summary>
    /// 实现DynamicAES加密模式的数据流
    /// </summary>
    public sealed class DynamicAESCryptoStream : Stream
    {
        private const ushort _VERSION = 1;
        private const ushort _REVISION = 1;
        private const KeyFlags _DEFAULT_FLAGS = KeyFlags.IsKeyEncryptedUsingRSA | KeyFlags.HasAESKeyChecksum | KeyFlags.HasAESIVChecksum | KeyFlags.HasDataChecksum;
        private const int _DEFAULT_DATA_SHA1_BLOCK_SIZE = 16364; // 16k block + checksum
        private static bool _warn_on_default_flags_no_rsa_flag = false;

        private readonly CryptoStreamMode _stream_mode;
        private readonly byte[] _aes_key, _aes_iv;
        private readonly int _data_sha1_block_size;
        private readonly CryptoStream _crypto_stream;
        private hash.SHA1 _data_sha1;

        private int _block_start_offset, _block_data_size;
        private byte[] _block_data;
        public DynamicAESCryptoStream(Stream upstream, CryptoStreamMode mode, RSAParameters rsa_key)
        {
            if (upstream == null) throw new ArgumentNullException("upstream");
            if (mode == CryptoStreamMode.Read && !upstream.CanRead) throw new ArgumentException("upstream is not readable, this CryptoStreamMode requires read access");
            else if (mode == CryptoStreamMode.Write && !upstream.CanWrite) throw new ArgumentException("upstream is not writable, this CryptoStreamMode requires write access");

            RSACryptoServiceProvider rsa = new RSACryptoServiceProvider();
            rsa.ImportParameters(rsa_key);
            if (mode == CryptoStreamMode.Read && rsa.PublicOnly) throw new ArgumentException("RSA private key is required for Read mode");

            _stream_mode = mode;
            _data_sha1 = new hash.SHA1();

            long? origin_position = null;
            try
            {
                if (upstream.CanSeek)
                    origin_position = upstream.Position;
            }
            catch (Exception) { }

            try
            {
                if (mode == CryptoStreamMode.Read)
                {
                    // READ mode
                    var marker = Encoding.UTF8.GetString(StreamHelper.ReadBytesAndCheckSize(upstream, 4));
                    StreamHelper.AssertEqual(marker, "BCSD");

                    // version
                    var version = StreamHelper.ReadType<ushort>(upstream);
                    StreamHelper.AssertEqual(version, _VERSION);

                    // revision
                    var revision = StreamHelper.ReadType<ushort>(upstream);
                    StreamHelper.AssertEqual(revision, _REVISION);

                    // preserved
                    var preserved = StreamHelper.ReadType<int>(upstream);
                    StreamHelper.WarnNotEqual(preserved, 0);

                    // key flags
                    var flags = (KeyFlags)StreamHelper.ReadType<int>(upstream);
                    if ((flags & KeyFlags.IsKeyEncryptedUsingRSA) != 0)
                    {
                        int key_length = StreamHelper.ReadType<int>(upstream);
                        var rsa_encrypted_aes_key = StreamHelper.ReadBytesAndCheckSize(upstream, key_length);
                        var rsa_encrypted_aes_iv = StreamHelper.ReadBytesAndCheckSize(upstream, key_length);
                        _aes_key = rsa.Decrypt(rsa_encrypted_aes_key, false);
                        _aes_iv = rsa.Decrypt(rsa_encrypted_aes_iv, false);
                    }
                    else
                        throw new FormatException("Expected flag: KeyFlags.IsKeyEncryptedUsingRSA");

                    if ((flags & KeyFlags.HasAESKeyChecksum) != 0)
                    {
                        var aes_key_sha1 = StreamHelper.ReadBytesAndCheckSize(upstream, 20);
                        StreamHelper.WarnNotEqual(Util.Hex(hash.SHA1.ComputeHash(_aes_key, 0, _aes_key.Length)), Util.Hex(aes_key_sha1));
                    }
                    if ((flags & KeyFlags.HasAESIVChecksum) != 0)
                    {
                        var aes_iv_sha1 = StreamHelper.ReadBytesAndCheckSize(upstream, 20);
                        StreamHelper.WarnNotEqual(Util.Hex(hash.SHA1.ComputeHash(_aes_iv, 0, _aes_iv.Length)), Util.Hex(aes_iv_sha1));
                    }

                    if ((flags & KeyFlags.HasDataChecksum) != 0)
                    {
                        _data_sha1_block_size = StreamHelper.ReadType<int>(upstream);
                        _block_data = new byte[_data_sha1_block_size + 20];
                        _block_start_offset = _block_data.Length;
                        _block_data_size = _block_data.Length;
                    }

                    _crypto_stream = Crypto.AES_StreamDecrypt(upstream, _aes_key, CipherMode.CFB, _aes_iv);
                }
                else
                {
                    // WRITE mode
                    var marker = Encoding.UTF8.GetBytes("BCSD");
                    upstream.Write(marker, 0, marker.Length);

                    // version
                    StreamHelper.WriteType(upstream, _VERSION);

                    // revision
                    StreamHelper.WriteType(upstream, _REVISION);

                    // preserved
                    StreamHelper.WriteType(upstream, 0);

                    // flags
                    var flags = _DEFAULT_FLAGS;
                    if ((flags & KeyFlags.IsKeyEncryptedUsingRSA) == 0)
                    {
                        if (!_warn_on_default_flags_no_rsa_flag)
                        {
                            Tracer.GlobalTracer.TraceWarning("No KeyFlags.IsKeyEncryptedUsingRSA set by default, it must be set when using DynamicAES");
                            _warn_on_default_flags_no_rsa_flag = true;
                        }
                        flags |= KeyFlags.IsKeyEncryptedUsingRSA;
                    }
                    StreamHelper.WriteType(upstream, (int)flags);

                    // aes key
                    var random = new Random();
                    var aes_key = new byte[32];
                    var aes_iv = new byte[16];
                    random.NextBytes(aes_key);
                    random.NextBytes(aes_iv);
                    var encrypted_aes_key = rsa.Encrypt(aes_key, false);
                    var encrypted_aes_iv = rsa.Encrypt(aes_iv, false);

                    if (encrypted_aes_key.Length != encrypted_aes_iv.Length || encrypted_aes_key.Length * 8 != rsa.KeySize)
                        throw new InvalidDataException("internal error: invalid key length");

                    // key length
                    StreamHelper.WriteType(upstream, rsa.KeySize / 8);

                    // encrypted aes key
                    upstream.Write(encrypted_aes_key, 0, encrypted_aes_key.Length);
                    upstream.Write(encrypted_aes_iv, 0, encrypted_aes_iv.Length);

                    if ((flags & KeyFlags.HasAESKeyChecksum) != 0)
                    {
                        var aes_key_sha1 = hash.SHA1.ComputeHash(aes_key, 0, aes_key.Length);
                        upstream.Write(aes_key_sha1, 0, aes_key_sha1.Length);
                    }
                    if ((flags & KeyFlags.HasAESIVChecksum) != 0)
                    {
                        var aes_iv_sha1 = hash.SHA1.ComputeHash(aes_iv, 0, aes_iv.Length);
                        upstream.Write(aes_iv_sha1, 0, aes_iv_sha1.Length);
                    }
                    if ((flags & KeyFlags.HasDataChecksum) != 0)
                    {
                        StreamHelper.WriteType(upstream, _DEFAULT_DATA_SHA1_BLOCK_SIZE);
                        _data_sha1_block_size = _DEFAULT_DATA_SHA1_BLOCK_SIZE;
                    }
                    upstream.Flush();
                    _crypto_stream = Crypto.AES_StreamEncrypt(upstream, aes_key, CipherMode.CFB, aes_iv);
                }
            }
            catch (Exception)
            {
                try
                {
                    if (upstream.CanSeek && origin_position != null)
                        upstream.Seek(origin_position.Value, SeekOrigin.Begin);
                }
                catch (Exception) { }
                throw;
            }
        }

        public override bool CanRead => _stream_mode == CryptoStreamMode.Read ? _crypto_stream.CanRead : false;

        public override bool CanSeek => false;

        public override bool CanWrite => _stream_mode == CryptoStreamMode.Write ? _crypto_stream.CanWrite : false;

        public override long Length => throw new NotSupportedException();

        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override void Flush()
        {
            if (CanWrite)
            {
                _crypto_stream.Flush();
            }
        }

        public void FlushFinalBlock()
        {
            if (CanWrite)
            {
                if (_data_sha1_block_size > 0 && _data_sha1.Length > 0)
                {
                    _data_sha1.TransformFinalBlock(new byte[0], 0, 0);
                    var sha1_hash = _data_sha1.Hash;
                    _data_sha1.Initialize();
                    _crypto_stream.Write(sha1_hash, 0, sha1_hash.Length);
                }
                _crypto_stream.FlushFinalBlock();
            }
        }

        public bool HasFlushFinalBlock => _crypto_stream.HasFlushedFinalBlock;

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (!CanRead) throw new InvalidOperationException("Stream is not readable");
            if (_data_sha1_block_size == 0)
                return _crypto_stream.Read(buffer, offset, count);

            int original_count = count;
            while (count > 0 && _block_data_size > 0)
            {
                // block read
                if (_block_start_offset == _block_data_size)
                {
                    int read_bytes;
                    int total_bytes = 0;
                    do
                    {
                        read_bytes = _crypto_stream.Read(_block_data, total_bytes, _block_data.Length - total_bytes);
                        total_bytes += read_bytes;
                    } while (read_bytes > 0);
                    _block_start_offset = 0;
                    _block_data_size = total_bytes;

                    // checking sha1
                    if (_block_data_size <= 20)
                        throw new FormatException("Invalid block, a block should contain at least 20 bytes for checksum");
                    _data_sha1.TransformFinalBlock(_block_data, 0, _block_data_size - 20);
                    var sha1_got_bytes = _data_sha1.Hash;
                    _data_sha1.Initialize();

                    var sha1_expected_bytes = new byte[20];
                    Array.Copy(_block_data, _block_data_size - 20, sha1_expected_bytes, 0, 20);
                    var sha1_got = Util.Hex(sha1_got_bytes);
                    var sha1_expected = Util.Hex(sha1_expected_bytes);
                    if (sha1_got != sha1_expected)
                    {
                        Tracer.GlobalTracer.TraceWarning($"Block SHA1 corrupt, expected \"{sha1_expected}\", but got \"{sha1_got}\"");
                    }
                }

                var min_bytes_to_read = Math.Min(count, _block_data_size - _block_start_offset - 20);
                Array.Copy(_block_data, _block_start_offset, buffer, offset, min_bytes_to_read);
                _block_start_offset += min_bytes_to_read + 20;
                offset += min_bytes_to_read;
                count -= min_bytes_to_read;
            }
            return original_count - count;
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
            if (!CanWrite) throw new InvalidOperationException("Stream is not writable");
            if (_data_sha1_block_size == 0)
            {
                _crypto_stream.Write(buffer, offset, count);
                return;
            }

            while (count > 0)
            {
                int min_to_write = (int)Math.Min(count, _data_sha1_block_size - _data_sha1.Length);
                _data_sha1.TransformBlock(buffer, offset, min_to_write);
                _crypto_stream.Write(buffer, offset, min_to_write);

                if (_data_sha1_block_size == _data_sha1.Length)
                { 
                    _data_sha1.TransformFinalBlock(buffer, 0, 0);
                    var sha1 = _data_sha1.Hash;
                    _crypto_stream.Write(sha1, 0, sha1.Length);
                    _data_sha1.Initialize();
                }

                offset += min_to_write;
                count -= min_to_write;
            }
        }
    }
}
