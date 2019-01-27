using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace GlobalUtil
{
    /// <summary>
    /// 队列形式的数据流，只允许尾部写入和头部读取，对内存分配进行优化
    /// </summary>
    public class QueueStream : Stream
    {
        //数据长度
        private long _length;
        //数据块的链表
        private LinkedList<byte[]> _mem_list;
        //数据块大小
        private long _chunk_size;
        //当前数据块的偏移量
        private long _write_offset;
        private long _read_offset;
        public const long DEFAULT_CHUNK_SIZE = 4096;

        #region overriding properties for Stream
        public override bool CanRead
        {
            get
            {
                return true;
            }
        }

        public override bool CanSeek
        {
            get
            {
                return false;
            }
        }

        public override bool CanWrite
        {
            get
            {
                return true;
            }
        }

        public override long Length
        {
            get
            {
                return _length;
            }
        }

        public override long Position
        {
            get
            {
                return 0;
            }

            set
            {
                throw new NotSupportedException();
            }
        }
        #endregion
        public override void Flush()
        {
            //nothing to flush
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int readed_length = 0;
            while (readed_length < count && _mem_list.Count > 0)
            {
                var cur_data = _mem_list.First.Value;
                var available_length = _mem_list.Count == 1 ? _write_offset : cur_data.Length;
                var length = Math.Min(available_length - _read_offset, count - readed_length);

                Array.Copy(cur_data, _read_offset, buffer, offset, length);
                _read_offset += length;
                offset += (int)length;
                readed_length += (int)length;
                if (length == 0) break;
                if (_read_offset == cur_data.Length)
                {
                    _mem_list.RemoveFirst();
                    _read_offset = 0;
                }
            }
            _length -= readed_length;
            return readed_length;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException("Could not seek");
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException("Could not set length");
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            int index = 0;
            while (index < count)
            {
                var cur_data = _mem_list.Last.Value;
                var length = Math.Min(cur_data.Length - _write_offset, count - index);

                Array.Copy(buffer, offset, cur_data, _write_offset, length);
                _write_offset += length;
                offset += (int)length;
                index += (int)length;

                if (_write_offset == cur_data.Length)
                {
                    _mem_list.AddLast(new byte[_chunk_size]);
                    _write_offset = 0;
                }
            }
            _length += count;
        }

        public QueueStream(long chunk_size = DEFAULT_CHUNK_SIZE)
        {
            _chunk_size = chunk_size;
            _length = 0;
            _mem_list = new LinkedList<byte[]>();
            _mem_list.AddLast(new byte[_chunk_size]);
            _read_offset = 0;
            _write_offset = 0;
        }
        public long ChunkSize
        {
            get
            {
                return _chunk_size;
            }
            set
            {
                _chunk_size = value;
            }
        }

    }
}
