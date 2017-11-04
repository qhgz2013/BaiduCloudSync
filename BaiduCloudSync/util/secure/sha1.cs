using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace BaiduCloudSync
{
    //a simple class to calculate SHA1 with pause and continue supports
    //一个简单的用于计算SHA1的类，支持暂停和继续计算功能
    [Serializable]
    public class SHA1
    {

        #region vars
        private uint _A, _B, _C, _D, _E;
        private int _offset;
        private byte[] _data;
        private long _length;
        #endregion


        #region calculation functions
        private uint _sha1_function_ft0(uint b, uint c, uint d)
        {
            return (b & c) | (~b & d);
        }
        private uint _sha1_function_ft1(uint b, uint c, uint d)
        {
            return b ^ c ^ d;
        }
        private uint _sha1_function_ft2(uint b, uint c, uint d)
        {
            return (b & c) | (b & d) | (c & d);
        }
        private uint _sha1_function_ft3(uint b, uint c, uint d)
        {
            return b ^ c ^ d;
        }
        private void _sha1_function_f0(ref uint a, ref uint b, ref uint c, ref uint d, ref uint e, uint wt)
        {
            uint old_a = a;
            a = (a << 5) | (a >> 27);
            a += _sha1_function_ft0(b, c, d) + e + wt + 0x5a827999;
            e = d;
            d = c;
            c = (b << 30) | (b >> 2);
            b = old_a;
        }
        private void _sha1_function_f1(ref uint a, ref uint b, ref uint c, ref uint d, ref uint e, uint wt)
        {
            uint old_a = a;
            a = (a << 5) | (a >> 27);
            a += _sha1_function_ft1(b, c, d) + e + wt + 0x6ed9eba1;
            e = d;
            d = c;
            c = (b << 30) | (b >> 2);
            b = old_a;
        }
        private void _sha1_function_f2(ref uint a, ref uint b, ref uint c, ref uint d, ref uint e, uint wt)
        {
            uint old_a = a;
            a = (a << 5) | (a >> 27);
            a += _sha1_function_ft2(b, c, d) + e + wt + 0x8f1bbcdc;
            e = d;
            d = c;
            c = (b << 30) | (b >> 2);
            b = old_a;
        }
        private void _sha1_function_f3(ref uint a, ref uint b, ref uint c, ref uint d, ref uint e, uint wt)
        {
            uint old_a = a;
            a = (a << 5) | (a >> 27);
            a += _sha1_function_ft3(b, c, d) + e + wt + 0xca62c1d6;
            e = d;
            d = c;
            c = (b << 30) | (b >> 2);
            b = old_a;
        }
        private uint[] _sha1_byte_array_to_uint_array(byte[] data)
        {
            var a = new uint[data.Length >> 2];
            for (int i = 0; i < a.Length; i++)
            {
                a[i] = ((uint)data[i << 2] << 24) | ((uint)data[(i << 2) + 1] << 16) | ((uint)data[(i << 2) + 2] << 8) | ((uint)data[(i << 2) + 3]);
            }
            return a;
        }
        private void _sha1_update_block()
        {
            var aa = _sha1_byte_array_to_uint_array(_data);
            var W = new uint[80];
            Array.Copy(aa, W, 16);
            for (int i = 16; i < 80; i++)
            {
                uint x = W[i - 3] ^ W[i - 8] ^ W[i - 14] ^ W[i - 16];
                x = (x << 1) | (x >> 31);
                W[i] = x;
            }

            uint a = _A, b = _B, c = _C, d = _D, e = _E;
            _sha1_function_f0(ref a, ref b, ref c, ref d, ref e, W[0]);
            _sha1_function_f0(ref a, ref b, ref c, ref d, ref e, W[1]);
            _sha1_function_f0(ref a, ref b, ref c, ref d, ref e, W[2]);
            _sha1_function_f0(ref a, ref b, ref c, ref d, ref e, W[3]);
            _sha1_function_f0(ref a, ref b, ref c, ref d, ref e, W[4]);
            _sha1_function_f0(ref a, ref b, ref c, ref d, ref e, W[5]);
            _sha1_function_f0(ref a, ref b, ref c, ref d, ref e, W[6]);
            _sha1_function_f0(ref a, ref b, ref c, ref d, ref e, W[7]);
            _sha1_function_f0(ref a, ref b, ref c, ref d, ref e, W[8]);
            _sha1_function_f0(ref a, ref b, ref c, ref d, ref e, W[9]);
            _sha1_function_f0(ref a, ref b, ref c, ref d, ref e, W[10]);
            _sha1_function_f0(ref a, ref b, ref c, ref d, ref e, W[11]);
            _sha1_function_f0(ref a, ref b, ref c, ref d, ref e, W[12]);
            _sha1_function_f0(ref a, ref b, ref c, ref d, ref e, W[13]);
            _sha1_function_f0(ref a, ref b, ref c, ref d, ref e, W[14]);
            _sha1_function_f0(ref a, ref b, ref c, ref d, ref e, W[15]);
            _sha1_function_f0(ref a, ref b, ref c, ref d, ref e, W[16]);
            _sha1_function_f0(ref a, ref b, ref c, ref d, ref e, W[17]);
            _sha1_function_f0(ref a, ref b, ref c, ref d, ref e, W[18]);
            _sha1_function_f0(ref a, ref b, ref c, ref d, ref e, W[19]);

            _sha1_function_f1(ref a, ref b, ref c, ref d, ref e, W[20]);
            _sha1_function_f1(ref a, ref b, ref c, ref d, ref e, W[21]);
            _sha1_function_f1(ref a, ref b, ref c, ref d, ref e, W[22]);
            _sha1_function_f1(ref a, ref b, ref c, ref d, ref e, W[23]);
            _sha1_function_f1(ref a, ref b, ref c, ref d, ref e, W[24]);
            _sha1_function_f1(ref a, ref b, ref c, ref d, ref e, W[25]);
            _sha1_function_f1(ref a, ref b, ref c, ref d, ref e, W[26]);
            _sha1_function_f1(ref a, ref b, ref c, ref d, ref e, W[27]);
            _sha1_function_f1(ref a, ref b, ref c, ref d, ref e, W[28]);
            _sha1_function_f1(ref a, ref b, ref c, ref d, ref e, W[29]);
            _sha1_function_f1(ref a, ref b, ref c, ref d, ref e, W[30]);
            _sha1_function_f1(ref a, ref b, ref c, ref d, ref e, W[31]);
            _sha1_function_f1(ref a, ref b, ref c, ref d, ref e, W[32]);
            _sha1_function_f1(ref a, ref b, ref c, ref d, ref e, W[33]);
            _sha1_function_f1(ref a, ref b, ref c, ref d, ref e, W[34]);
            _sha1_function_f1(ref a, ref b, ref c, ref d, ref e, W[35]);
            _sha1_function_f1(ref a, ref b, ref c, ref d, ref e, W[36]);
            _sha1_function_f1(ref a, ref b, ref c, ref d, ref e, W[37]);
            _sha1_function_f1(ref a, ref b, ref c, ref d, ref e, W[38]);
            _sha1_function_f1(ref a, ref b, ref c, ref d, ref e, W[39]);

            _sha1_function_f2(ref a, ref b, ref c, ref d, ref e, W[40]);
            _sha1_function_f2(ref a, ref b, ref c, ref d, ref e, W[41]);
            _sha1_function_f2(ref a, ref b, ref c, ref d, ref e, W[42]);
            _sha1_function_f2(ref a, ref b, ref c, ref d, ref e, W[43]);
            _sha1_function_f2(ref a, ref b, ref c, ref d, ref e, W[44]);
            _sha1_function_f2(ref a, ref b, ref c, ref d, ref e, W[45]);
            _sha1_function_f2(ref a, ref b, ref c, ref d, ref e, W[46]);
            _sha1_function_f2(ref a, ref b, ref c, ref d, ref e, W[47]);
            _sha1_function_f2(ref a, ref b, ref c, ref d, ref e, W[48]);
            _sha1_function_f2(ref a, ref b, ref c, ref d, ref e, W[49]);
            _sha1_function_f2(ref a, ref b, ref c, ref d, ref e, W[50]);
            _sha1_function_f2(ref a, ref b, ref c, ref d, ref e, W[51]);
            _sha1_function_f2(ref a, ref b, ref c, ref d, ref e, W[52]);
            _sha1_function_f2(ref a, ref b, ref c, ref d, ref e, W[53]);
            _sha1_function_f2(ref a, ref b, ref c, ref d, ref e, W[54]);
            _sha1_function_f2(ref a, ref b, ref c, ref d, ref e, W[55]);
            _sha1_function_f2(ref a, ref b, ref c, ref d, ref e, W[56]);
            _sha1_function_f2(ref a, ref b, ref c, ref d, ref e, W[57]);
            _sha1_function_f2(ref a, ref b, ref c, ref d, ref e, W[58]);
            _sha1_function_f2(ref a, ref b, ref c, ref d, ref e, W[59]);

            _sha1_function_f3(ref a, ref b, ref c, ref d, ref e, W[60]);
            _sha1_function_f3(ref a, ref b, ref c, ref d, ref e, W[61]);
            _sha1_function_f3(ref a, ref b, ref c, ref d, ref e, W[62]);
            _sha1_function_f3(ref a, ref b, ref c, ref d, ref e, W[63]);
            _sha1_function_f3(ref a, ref b, ref c, ref d, ref e, W[64]);
            _sha1_function_f3(ref a, ref b, ref c, ref d, ref e, W[65]);
            _sha1_function_f3(ref a, ref b, ref c, ref d, ref e, W[66]);
            _sha1_function_f3(ref a, ref b, ref c, ref d, ref e, W[67]);
            _sha1_function_f3(ref a, ref b, ref c, ref d, ref e, W[68]);
            _sha1_function_f3(ref a, ref b, ref c, ref d, ref e, W[69]);
            _sha1_function_f3(ref a, ref b, ref c, ref d, ref e, W[70]);
            _sha1_function_f3(ref a, ref b, ref c, ref d, ref e, W[71]);
            _sha1_function_f3(ref a, ref b, ref c, ref d, ref e, W[72]);
            _sha1_function_f3(ref a, ref b, ref c, ref d, ref e, W[73]);
            _sha1_function_f3(ref a, ref b, ref c, ref d, ref e, W[74]);
            _sha1_function_f3(ref a, ref b, ref c, ref d, ref e, W[75]);
            _sha1_function_f3(ref a, ref b, ref c, ref d, ref e, W[76]);
            _sha1_function_f3(ref a, ref b, ref c, ref d, ref e, W[77]);
            _sha1_function_f3(ref a, ref b, ref c, ref d, ref e, W[78]);
            _sha1_function_f3(ref a, ref b, ref c, ref d, ref e, W[79]);

            _A += a;
            _B += b;
            _C += c;
            _D += d;
            _E += e;
        }
        #endregion

        #region public functions
        /// <summary>
        /// 初始化所有变量
        /// </summary>
        public void Initialize()
        {
            _A = 0x67452301;
            _B = 0xefcdab89;
            _C = 0x98badcfe;
            _D = 0x10325476;
            _E = 0xc3d2e1f0;
            _data = new byte[64];
            _offset = 0;
            _length = 0;
        }
        public SHA1()
        {
            Initialize();
        }

        /// <summary>
        /// 在当前基础上更新字节块
        /// </summary>
        /// <param name="buffer">数据</param>
        /// <param name="index">起始位置偏移</param>
        /// <param name="length">数据长度</param>
        public void TransformBlock(byte[] buffer, int index, int length)
        {
            if (index < 0 || length <= 0) return;
            _length += length;

            if (_offset + length >= 64)
            {
                int size = 64 - _offset;
                Array.Copy(buffer, index, _data, _offset, size);
                _sha1_update_block();
                index += size;
                length -= size;
                _offset = 0;
            }

            while (length >= 64)
            {
                Array.Copy(buffer, index, _data, 0, 64);
                _sha1_update_block();
                index += 64;
                length -= 64;
            }

            Array.Copy(buffer, index, _data, _offset, length);
            _offset += length;
        }

        /// <summary>
        /// 在当前基础上更新最后的字节块
        /// </summary>
        /// <param name="buffer">数据</param>
        /// <param name="index">起始位置偏移</param>
        /// <param name="length">数据长度</param>
        public void TransformFinalBlock(byte[] buffer, int index, int length)
        {
            if (index < 0 || length < 0) return;
            _length += length;

            if (_offset + length >= 64)
            {
                int size = 64 - _offset;
                Array.Copy(buffer, index, _data, _offset, size);
                _sha1_update_block();
                index += size;
                length -= size;
                _offset = 0;
            }

            while (length >= 64)
            {
                Array.Copy(buffer, index, _data, 0, 64);
                _sha1_update_block();
                index += 64;
                length -= 64;
            }

            Array.Copy(buffer, index, _data, _offset, length);
            _offset += length;
            _data[_offset++] = 0x80;
            if (_offset + length >= 56)
            {
                Array.Clear(_data, _offset, 64 - _offset);
                _offset = 0;
                _sha1_update_block();

                Array.Clear(_data, 0, 56);
            }
            else
            {
                Array.Clear(_data, _offset, 56 - _offset);
            }

            long len = _length << 3;
            for (int i = 0; i < 8; i++)
            {
                _data[63 - i] = (byte)(len & 0xff);
                len >>= 8;
            }
            _sha1_update_block();
        }
        /// <summary>
         /// 返回当前字节数据所计算出的MD5
         /// </summary>
        public byte[] Hash
        {
            get
            {
                var data = new byte[20];
                uint a = _A, b = _B, c = _C, d = _D, e = _E;
                for (int i = 0; i < 4; i++)
                {
                    data[3 - i] = (byte)(a & 0xff);
                    a >>= 8;
                    data[7 - i] = (byte)(b & 0xff);
                    b >>= 8;
                    data[11 - i] = (byte)(c & 0xff);
                    c >>= 8;
                    data[15 - i] = (byte)(d & 0xff);
                    d >>= 8;
                    data[19 - i] = (byte)(e & 0xff);
                    e >>= 8;
                }
                return data;
            }
        }
        public long Length { get { return _length; } }
        #endregion


        #region util functions
        /// <summary>
        /// 计算该字节数组的MD5值
        /// </summary>
        /// <param name="buffer">数据</param>
        /// <param name="index">起始位置偏移</param>
        /// <param name="length">数据长度</param>
        /// <returns></returns>
        public static byte[] ComputeHash(byte[] buffer, int index, int length)
        {
            SHA1 sha1 = new SHA1();
            sha1.TransformFinalBlock(buffer, index, length);
            return sha1.Hash;
        }
        /// <summary>
        /// 计算字符串的MD5值
        /// </summary>
        /// <param name="str">字符串</param>
        /// <param name="encoding">编码类型，默认utf8</param>
        /// <returns></returns>
        public static byte[] ComputeHash(string str, Encoding encoding = null)
        {
            if (encoding == null) encoding = Encoding.UTF8;
            var data = encoding.GetBytes(str);
            return ComputeHash(data, 0, data.Length);
        }
        #endregion

        #region serialization
        /// <summary>
        /// 从数据流中逆序列化并创建一个SHA1类
        /// </summary>
        /// <param name="stream">可读取的数据流</param>
        /// <returns></returns>
        public static SHA1 Deserialize(Stream stream)
        {
            var fmt = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
            return (SHA1)fmt.Deserialize(stream);
        }
        /// <summary>
        /// 从数据流中序列化当前SHA1类（并保存当前的计算状态）
        /// </summary>
        /// <param name="stream">可写入的数据流</param>
        public void Serialize(Stream stream)
        {
            var fmt = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
            fmt.Serialize(stream, this);
        }
        /// <summary>
        /// 从文件中逆序列化并创建一个SHA1类
        /// </summary>
        /// <param name="file">文件路径</param>
        /// <returns></returns>
        public static SHA1 Deserialize(string file)
        {
            var fs = new FileStream(file, FileMode.Open, FileAccess.Read);
            var ret = Deserialize(fs);
            fs.Close();
            return ret;
        }
        /// <summary>
        /// 从文件中序列化当前SHA1类（并保存当前的计算状态）
        /// </summary>
        /// <param name="file">文件路径</param>
        public void Serialize(string file)
        {
            var fs = new FileStream(file, FileMode.Create, FileAccess.Write);
            Serialize(fs);
            fs.Close();
        }
        #endregion
    }
}
