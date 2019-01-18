using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BaiduCloudSync.segment
{

    public struct Segment
    {
        public Guid SegmentID;
        public long BeginPosition;
        public long EndPosition;
    }
    /// <summary>
    /// 文件分段算法
    /// </summary>
    public interface ISegmentAlgorithm
    {
        /// <summary>
        /// 分配一个新的文件分段，返回一个随机的Guid，若分配失败则返回Guid.Empty
        /// </summary>
        /// <returns></returns>
        Segment AllocateNewBlock();
        /// <summary>
        /// 释放已分配的文件分段
        /// </summary>
        /// <param name="block">要释放的分段的Guid</param>
        void ReleaseAllocatedBlock(Guid block);
        /// <summary>
        /// 更新分段已完成部分的位置，返回该分段是否需要继续执行IO操作
        /// </summary>
        /// <param name="block">分段的Guid</param>
        /// <param name="new_position">分段已完成的新位置</param>
        /// <returns>分段是否需要继续执行IO操作</returns>
        bool UpdateBlock(Guid block, long new_position);
    }
}
