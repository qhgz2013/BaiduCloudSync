// CRC32.cs
//
// 用于计算指定字节集的CRC32值
//
using System;
using System.IO;

namespace GlobalUtil
{
    [Serializable]
    public class Crc32
    {
        [NonSerialized]
        private static uint[] _table;
        static Crc32()
        {
            _table = new uint[256];
            for (uint i = 0; i < 256; i++)
            {
                uint r = i;
                for (int j = 0; j < 8; j++)
                {
                    if ((r & 1) != 0)
                        r = (r >> 1) ^ 0xedb88320;
                    else
                        r >>= 1;
                }
                _table[i] = r;
            }
        }
        private uint _value;
        private long _length;
        public Crc32()
        {
            Initialize();
        }
        /// <summary>
        /// 初始化计算并清空前一次的计算结果
        /// </summary>
        public void Initialize()
        {
            _value = 0xffffffff;
            _length = 0;
        }
        /// <summary>
        /// 在当前基础上更新并计算单个字节
        /// </summary>
        /// <param name="b">数据</param>
        /// <returns></returns>
        public uint TransformBlock(byte b)
        {
            _value = _table[(_value % 256) ^ b] ^ (_value >> 8);
            _length++;
            return Hash;
        }
        /// <summary>
        /// 在当前基础上更新并计算多个字节
        /// </summary>
        /// <param name="b">数据</param>
        /// <param name="offset">起始位置偏移</param>
        /// <param name="size">数据长度</param>
        /// <returns></returns>
        public uint TransformBlock(byte[] b, int offset, int size)
        {
            for (int i = 0; i < size; i++)
            {
                _value = _table[(_value % 256) ^ b[offset + i]] ^ (_value >> 8);
            }
            _length += size;
            return Hash;
        }
        /// <summary>
        /// 获取当前已经计算出的crc32的值
        /// </summary>
        /// <returns></returns>
        public uint Hash
        {
            get { return _value ^ 0xffffffff; }
        }
        public long Length { get { return _length; } }
        public static uint ComputeHash(byte[] data, int offset, int size)
        {
            var crc = new Crc32();
            return crc.TransformBlock(data, offset, size);
        }


        #region serialization
        /// <summary>
        /// 从数据流中逆序列化并创建一个CRC32类
        /// </summary>
        /// <param name="stream">可读取的数据流</param>
        /// <returns></returns>
        public static Crc32 Deserialize(Stream stream)
        {
            var fmt = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
            return (Crc32)fmt.Deserialize(stream);
        }
        /// <summary>
        /// 从数据流中序列化当前CRC32类（并保存当前的计算状态）
        /// </summary>
        /// <param name="stream">可写入的数据流</param>
        public void Serialize(Stream stream)
        {
            var fmt = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
            fmt.Serialize(stream, this);
        }
        /// <summary>
        /// 从文件中逆序列化并创建一个CRC32类
        /// </summary>
        /// <param name="file">文件路径</param>
        /// <returns></returns>
        public static Crc32 Deserialize(string file)
        {
            var fs = new FileStream(file, FileMode.Open, FileAccess.Read);
            var ret = Deserialize(fs);
            fs.Close();
            return ret;
        }
        /// <summary>
        /// 从文件中序列化当前CRC32类（并保存当前的计算状态）
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
