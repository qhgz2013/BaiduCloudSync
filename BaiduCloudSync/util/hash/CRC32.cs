// CRC32.cs
//
// 用于计算指定字节集的CRC32值
//
using System;
using System.IO;
using System.Linq;

namespace GlobalUtil.hash
{
    [Serializable]
    public class Crc32 : SerializableHashAlgorithm
    {
        [NonSerialized]
        private static readonly uint[] _table;
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
        private bool _transform_final_block_is_called;
        public Crc32()
        {
            Initialize();
        }
        /// <summary>
        /// 初始化计算并清空前一次的计算结果
        /// </summary>
        public override void Initialize()
        {
            _value = 0xffffffff;
            _length = 0;
            _transform_final_block_is_called = false;
        }
        public override void TransformBlock(byte[] buffer, int index, int length)
        {
            if (_transform_final_block_is_called)
                throw new InvalidOperationException("could not call TransformBlock after calling TransformFinalBlock, call Initialize to reset hash state");
            if (index < 0 || length < 0) return;

            for (int i = 0; i < length; i++)
            {
                _value = _table[(_value % 256) ^ buffer[index + i]] ^ (_value >> 8);
            }
            _length += length;
        }

        public override void TransformFinalBlock(byte[] buffer, int index, int length)
        {
            if (_transform_final_block_is_called)
                throw new InvalidOperationException("could not call TransformFinalBlock after calling TransformFinalBlock, call Initialize to reset hash state");
            TransformBlock(buffer, index, length);
        }
        /// <summary>
        /// 获取当前已经计算出的crc32的值
        /// </summary>
        /// <returns></returns>
        public override byte[] Hash
        {
            get
            {
                if (!_transform_final_block_is_called)
                    throw new InvalidOperationException("could not get the hash value before TransformFinalBlock is called");

                uint value = _value ^ 0xffffffff;
                byte[] bit_values = BitConverter.GetBytes(value);
                if (BitConverter.IsLittleEndian)
                    bit_values = bit_values.Reverse().ToArray();
                return bit_values;
            }
        }
        public override long Length { get { return _length; } }
        public static byte[] ComputeHash(byte[] data, int offset, int size)
        {
            var crc = new Crc32();
            crc.TransformFinalBlock(data, offset, size);
            return crc.Hash;
        }
    }
}
