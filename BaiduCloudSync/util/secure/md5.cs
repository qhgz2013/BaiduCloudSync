using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace BaiduCloudSync
{
    //a simple class to calculate MD5 with pause and continue supports
    //一个简单的用于计算MD5的类，支持暂停和继续计算功能
    [Serializable]
    public class MD5
    {
        #region vars
        //variable controls
        private uint _A, _B, _C, _D; //128bit的MD5所对于的4个32bit整型
        private int _offset; //下次数据读入的起始偏移
        private byte[] _data; //当前的数据（满64B写入时进行块更新）
        private long _length; //总传入的数据位数（用于计算最后的alignment块）
        #endregion

        #region calculation functions
        private uint _md5_function_f(uint x, uint y, uint z)
        {
            return (x & y) | (~x & z);
        }
        private uint _md5_function_g(uint x, uint y, uint z)
        {
            return (x & z) | (y & ~z);
        }
        private uint _md5_function_h(uint x, uint y, uint z)
        {
            return x ^ y ^ z;
        }
        private uint _md5_function_i(uint x, uint y, uint z)
        {
            return y ^ (x | ~z);
        }
        private uint _md5_function_ff(uint a, uint b, uint c, uint d, uint mj, int s, uint ti)
        {
            uint x = a + _md5_function_f(b, c, d) + mj + ti;
            x = (x << s) | (x >> (32 - s));
            return b + x;
        }
        private uint _md5_function_gg(uint a, uint b, uint c, uint d, uint mj, int s, uint ti)
        {
            uint x = a + _md5_function_g(b, c, d) + mj + ti;
            x = (x << s) | (x >> (32 - s));
            return b + x;
        }
        private uint _md5_function_hh(uint a, uint b, uint c, uint d, uint mj, int s, uint ti)
        {
            uint x = a + _md5_function_h(b, c, d) + mj + ti;
            x = (x << s) | (x >> (32 - s));
            return b + x;
        }
        private uint _md5_function_ii(uint a, uint b, uint c, uint d, uint mj, int s, uint ti)
        {
            uint x = a + _md5_function_i(b, c, d) + mj + ti;
            x = (x << s) | (x >> (32 - s));
            return b + x;
        }
        private uint[] _md5_byte_array_to_uint_array(byte[] data)
        {
            var a = new uint[data.Length >> 2];
            for (int i = 0; i < a.Length; i++)
            {
                a[i] = ((uint)data[i << 2]) | (((uint)data[(i << 2) | 1]) << 8) | (((uint)data[(i << 2) | 2]) << 16) | (((uint)data[(i << 2) | 3]) << 24);
            }
            return a;
        }
        private void _md5_update_block()
        {
            uint a = _A, b = _B, c = _C, d = _D;
            var data = _md5_byte_array_to_uint_array(_data);
            a = _md5_function_ff(a, b, c, d, data[0], 7, 0xd76aa478);
            d = _md5_function_ff(d, a, b, c, data[1], 12, 0xe8c7b756);
            c = _md5_function_ff(c, d, a, b, data[2], 17, 0x242070db);
            b = _md5_function_ff(b, c, d, a, data[3], 22, 0xc1bdceee);
            a = _md5_function_ff(a, b, c, d, data[4], 7, 0xf57c0faf);
            d = _md5_function_ff(d, a, b, c, data[5], 12, 0x4787c62a);
            c = _md5_function_ff(c, d, a, b, data[6], 17, 0xa8304613);
            b = _md5_function_ff(b, c, d, a, data[7], 22, 0xfd469501);
            a = _md5_function_ff(a, b, c, d, data[8], 7, 0x698098d8);
            d = _md5_function_ff(d, a, b, c, data[9], 12, 0x8b44f7af);
            c = _md5_function_ff(c, d, a, b, data[10], 17, 0xffff5bb1);
            b = _md5_function_ff(b, c, d, a, data[11], 22, 0x895cd7be);
            a = _md5_function_ff(a, b, c, d, data[12], 7, 0x6b901122);
            d = _md5_function_ff(d, a, b, c, data[13], 12, 0xfd987193);
            c = _md5_function_ff(c, d, a, b, data[14], 17, 0xa679438e);
            b = _md5_function_ff(b, c, d, a, data[15], 22, 0x49b40821);

            a = _md5_function_gg(a, b, c, d, data[1], 5, 0xf61e2562);
            d = _md5_function_gg(d, a, b, c, data[6], 9, 0xc040b340);
            c = _md5_function_gg(c, d, a, b, data[11], 14, 0x265e5a51);
            b = _md5_function_gg(b, c, d, a, data[0], 20, 0xe9b6c7aa);
            a = _md5_function_gg(a, b, c, d, data[5], 5, 0xd62f105d);
            d = _md5_function_gg(d, a, b, c, data[10], 9, 0x02441453);
            c = _md5_function_gg(c, d, a, b, data[15], 14, 0xd8a1e681);
            b = _md5_function_gg(b, c, d, a, data[4], 20, 0xe7d3fbc8);
            a = _md5_function_gg(a, b, c, d, data[9], 5, 0x21e1cde6);
            d = _md5_function_gg(d, a, b, c, data[14], 9, 0xc33707d6);
            c = _md5_function_gg(c, d, a, b, data[3], 14, 0xf4d50d87);
            b = _md5_function_gg(b, c, d, a, data[8], 20, 0x455a14ed);
            a = _md5_function_gg(a, b, c, d, data[13], 5, 0xa9e3e905);
            d = _md5_function_gg(d, a, b, c, data[2], 9, 0xfcefa3f8);
            c = _md5_function_gg(c, d, a, b, data[7], 14, 0x676f02d9);
            b = _md5_function_gg(b, c, d, a, data[12], 20, 0x8d2a4c8a);

            a = _md5_function_hh(a, b, c, d, data[5], 4, 0xfffa3942);
            d = _md5_function_hh(d, a, b, c, data[8], 11, 0x8771f681);
            c = _md5_function_hh(c, d, a, b, data[11], 16, 0x6d9d6122);
            b = _md5_function_hh(b, c, d, a, data[14], 23, 0xfde5380c);
            a = _md5_function_hh(a, b, c, d, data[1], 4, 0xa4beea44);
            d = _md5_function_hh(d, a, b, c, data[4], 11, 0x4bdecfa9);
            c = _md5_function_hh(c, d, a, b, data[7], 16, 0xf6bb4b60);
            b = _md5_function_hh(b, c, d, a, data[10], 23, 0xbebfbc70);
            a = _md5_function_hh(a, b, c, d, data[13], 4, 0x289b7ec6);
            d = _md5_function_hh(d, a, b, c, data[0], 11, 0xeaa127fa);
            c = _md5_function_hh(c, d, a, b, data[3], 16, 0xd4ef3085);
            b = _md5_function_hh(b, c, d, a, data[6], 23, 0x04881d05);
            a = _md5_function_hh(a, b, c, d, data[9], 4, 0xd9d4d039);
            d = _md5_function_hh(d, a, b, c, data[12], 11, 0xe6db99e5);
            c = _md5_function_hh(c, d, a, b, data[15], 16, 0x1fa27cf8);
            b = _md5_function_hh(b, c, d, a, data[2], 23, 0xc4ac5665);

            a = _md5_function_ii(a, b, c, d, data[0], 6, 0xf4292244);
            d = _md5_function_ii(d, a, b, c, data[7], 10, 0x432aff97);
            c = _md5_function_ii(c, d, a, b, data[14], 15, 0xab9423a7);
            b = _md5_function_ii(b, c, d, a, data[5], 21, 0xfc93a039);
            a = _md5_function_ii(a, b, c, d, data[12], 6, 0x655b59c3);
            d = _md5_function_ii(d, a, b, c, data[3], 10, 0x8f0ccc92);
            c = _md5_function_ii(c, d, a, b, data[10], 15, 0xffeff47d);
            b = _md5_function_ii(b, c, d, a, data[1], 21, 0x85845dd1);
            a = _md5_function_ii(a, b, c, d, data[8], 6, 0x6fa87e4f);
            d = _md5_function_ii(d, a, b, c, data[15], 10, 0xfe2ce6e0);
            c = _md5_function_ii(c, d, a, b, data[6], 15, 0xa3014314);
            b = _md5_function_ii(b, c, d, a, data[13], 21, 0x4e0811a1);
            a = _md5_function_ii(a, b, c, d, data[4], 6, 0xf7537e82);
            d = _md5_function_ii(d, a, b, c, data[11], 10, 0xbd3af235);
            c = _md5_function_ii(c, d, a, b, data[2], 15, 0x2ad7d2bb);
            b = _md5_function_ii(b, c, d, a, data[9], 21, 0xeb86d391);

            _A += a;
            _B += b;
            _C += c;
            _D += d;
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
            _data = new byte[64];
            _offset = 0;
            _length = 0;
        }

        public MD5()
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
                int len = 64 - _offset;
                Array.Copy(buffer, index, _data, _offset, len);
                _md5_update_block();
                index += len;
                length -= len;
                _offset = 0;
            }

            while (length >= 64)
            {
                Array.Copy(buffer, index, _data, 0, 64);
                _md5_update_block();
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
                int len = 64 - _offset;
                Array.Copy(buffer, index, _data, _offset, len);
                _md5_update_block();
                index += len;
                length -= len;
                _offset = 0;
            }

            while (length >= 64)
            {
                Array.Copy(buffer, index, _data, 0, 64);
                _md5_update_block();
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
                _md5_update_block();

                Array.Clear(_data, 0, 56);
            }
            else
            {
                Array.Clear(_data, _offset, 56 - _offset);
            }
            long data_len = _length << 3;
            for (int i = 0; i < 8; i++)
            {
                _data[56 + i] = (byte)(data_len & 0xff);
                data_len >>= 8;
            }
            _md5_update_block();
        }
        /// <summary>
        /// 返回当前字节数据所计算出的MD5
        /// </summary>
        public byte[] Hash
        {
            get
            {
                var data = new byte[16];
                uint a = _A, b = _B, c = _C, d = _D;
                for (int i = 0; i < 4; i++)
                {
                    data[i] = (byte)(a & 0xff);
                    a >>= 8;
                    data[4 + i] = (byte)(b & 0xff);
                    b >>= 8;
                    data[8 + i] = (byte)(c & 0xff);
                    c >>= 8;
                    data[12 + i] = (byte)(d & 0xff);
                    d >>= 8;
                }
                return data;
            }
        }
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
            MD5 md5 = new MD5();
            md5.TransformFinalBlock(buffer, index, length);
            return md5.Hash;
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
        /// 从数据流中逆序列化并创建一个MD5类
        /// </summary>
        /// <param name="stream">可读取的数据流</param>
        /// <returns></returns>
        public static MD5 Deserialize(Stream stream)
        {
            var fmt = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
            return (MD5)fmt.Deserialize(stream);
        }
        /// <summary>
        /// 从数据流中序列化当前MD5类（并保存当前的计算状态）
        /// </summary>
        /// <param name="stream">可写入的数据流</param>
        public void Serialize(Stream stream)
        {
            var fmt = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
            fmt.Serialize(stream, this);
        }
        /// <summary>
        /// 从文件中逆序列化并创建一个MD5类
        /// </summary>
        /// <param name="file">文件路径</param>
        /// <returns></returns>
        public static MD5 Deserialize(string file)
        {
            var fs = new FileStream(file, FileMode.Open, FileAccess.Read);
            var ret = Deserialize(fs);
            fs.Close();
            return ret;
        }
        /// <summary>
        /// 从文件中序列化当前MD5类（并保存当前的计算状态）
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
