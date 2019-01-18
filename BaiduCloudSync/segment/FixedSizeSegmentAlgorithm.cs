using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BaiduCloudSync.segment
{
    /// <summary>
    /// 以固定大小进行文件分段的文件分段算法
    /// </summary>
    public class FixedSizeSegmentAlgorithm : ISegmentAlgorithm
    {
        /// <summary>
        /// 实例化该算法
        /// </summary>
        /// <param name="file_size">文件大小</param>
        /// <param name="block_size">分段大小</param>
        public FixedSizeSegmentAlgorithm(long file_size, long block_size)
        {
            if (file_size < 0) throw new ArgumentOutOfRangeException("file_size");
            if (block_size <= 0) throw new ArgumentOutOfRangeException("block_size");
            long blocks_in_need = file_size / block_size;
            if (block_size * blocks_in_need < file_size) blocks_in_need++; // ceil op

            _finished_pos = new long[blocks_in_need];
            _block_in_free = new SortedSet<int>();
            _allocated_id = new SortedDictionary<Guid, int>();
            for (int i = 0; i < blocks_in_need; i++)
            {
                if (!_block_in_free.Add(i))
                    throw new InvalidOperationException("could not assign an object to hash set");
                _finished_pos[i] = i * block_size;
            }
            _size = file_size;
            _block_size = block_size;
        }
        private long _size;
        private long _block_size;
        private long[] _finished_pos;
        private SortedSet<int> _block_in_free;
        private SortedDictionary<Guid, int> _allocated_id;
        private object _sync_root = new object();
        private Guid _gen_guid(int i)
        {
            while (true)
            {
                Guid id = Guid.NewGuid();
                if (!_allocated_id.ContainsKey(id))
                {
                    _allocated_id.Add(id, i);
                    return id;
                }
            }
        }
        public Segment AllocateNewBlock()
        {
            lock (_sync_root)
            {
                if (_block_in_free.Count == 0) return new Segment { SegmentID = Guid.Empty, BeginPosition = 0, EndPosition = 0 };
                int alloc_block = _block_in_free.Min;
                _block_in_free.Remove(alloc_block);
                var guid = _gen_guid(alloc_block);
                return new Segment { SegmentID = guid, BeginPosition = _finished_pos[alloc_block], EndPosition = Math.Min((alloc_block + 1) * _block_size, _size) };
            }
        }

        public void ReleaseAllocatedBlock(Guid block)
        {
            lock (_sync_root)
            {
                if (!_allocated_id.ContainsKey(block)) return;
                int alloc_block = _allocated_id[block];

                if (!_block_in_free.Add(alloc_block))
                    throw new InvalidOperationException("could not assign an object to hash set");
                _allocated_id.Remove(block);
            }
        }

        public bool UpdateBlock(Guid block, long new_position)
        {
            lock (_sync_root)
            {
                if (!_allocated_id.ContainsKey(block)) return false;
                int alloc_block = _allocated_id[block];
                _finished_pos[alloc_block] = new_position;
                if (new_position >= Math.Min(_size, (alloc_block + 1) * _block_size))
                    return false;
                else
                    return true;
            }
        }
    }
}
