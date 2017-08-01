// CRC32.cs
//
// 用于计算指定字节集的CRC32值
//
namespace BaiduCloudSync
{
    public class Crc32
    {
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
        }
        /// <summary>
        /// 在当前基础上更新并计算单个字节
        /// </summary>
        /// <param name="b">数据</param>
        /// <returns></returns>
        public uint AppendByte(byte b)
        {
            _value = _table[(_value % 256) ^ b] ^ (_value >> 8);
            return GetCrc32();
        }
        /// <summary>
        /// 在当前基础上更新并计算多个字节
        /// </summary>
        /// <param name="b">数据</param>
        /// <param name="offset">起始位置偏移</param>
        /// <param name="size">数据长度</param>
        /// <returns></returns>
        public uint Append(byte[] b, int offset, int size)
        {
            for (int i = 0; i < size; i++)
            {
                _value = _table[(_value % 256) ^ b[offset + i]] ^ (_value >> 8);
            }
            return GetCrc32();
        }
        /// <summary>
        /// 获取当前已经计算出的crc32的值
        /// </summary>
        /// <returns></returns>
        public uint GetCrc32()
        {
            return _value ^ 0xffffffff;
        }
        public static uint CalculateCrc32(byte[] data, int offset, int size)
        {
            var crc = new Crc32();
            return crc.Append(data, offset, size);
        }
    }
}
