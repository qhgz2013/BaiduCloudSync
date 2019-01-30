using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BaiduCloudSync.api.callbackargs;

namespace BaiduCloudSync.api
{
    public interface IPcsAsyncAPI
    {
        /// <summary>
        /// 列出指定路径下的所有文件
        /// </summary>
        /// <param name="path">指定的路径</param>
        /// <param name="callback">回调函数</param>
        /// <param name="order">文件排序顺序</param>
        /// <param name="desc">是否倒序排序</param>
        /// <param name="page">页数（从1开始）</param>
        /// <param name="count">每页最大大小</param>
        /// <param name="state">回调函数的附加参数</param>
        void ListDir(string path, EventHandler<PcsApiMultiObjectMetaCallbackArgs> callback, PcsFileOrder order = PcsFileOrder.Name, bool desc = false, int page = 1, int count = 1000, object state = null);

        /// <summary>
        /// 获取网盘的配额
        /// </summary>
        /// <param name="callback">回调函数</param>
        /// <param name="state">回调函数的附加参数</param>
        void GetQuota(EventHandler<PcsApiQuotaCallbackArgs> callback, object state = null);

        /// <summary>
        /// 创建文件夹
        /// </summary>
        /// <param name="path">文件夹路径</param>
        /// <param name="callback">回调函数</param>
        /// <param name="state">回调函数的附加参数</param>
        void CreateDirectory(string path, EventHandler<PcsApiOperationCallbackArgs> callback, object state = null);

        /// <summary>
        /// 删除文件或文件夹
        /// </summary>
        /// <param name="paths">文件/文件夹路径</param>
        /// <param name="callback">回调函数</param>
        /// <param name="state">回调函数的附加参数</param>
        void Delete(IEnumerable<string> paths, EventHandler<PcsApiOperationCallbackArgs> callback, object state = null);

        /// <summary>
        /// 移动文件或文件夹
        /// </summary>
        /// <param name="source">原文件/文件夹路径</param>
        /// <param name="destination">目标文件/文件夹路径（包含文件/文件夹名）</param>
        /// <param name="callback">回调函数</param>
        /// <param name="state">回调函数的附加参数</param>
        /// <example>
        /// 将/aa/bb.txt文件移动到/c文件夹下，且文件名不变：
        /// Move(new string[] { "/aa/bb.txt" }, new string[] { "/c/bb.txt" }, null, null)
        /// </example>
        void Move(IEnumerable<string> source, IEnumerable<string> destination, EventHandler<PcsApiOperationCallbackArgs> callback, object state = null);

        /// <summary>
        /// 移动文件或文件夹
        /// </summary>
        /// <param name="source">原文件/文件夹路径</param>
        /// <param name="destination">目标文件/文件夹路径（包含文件/文件夹名）</param>
        /// <param name="callback">回调函数</param>
        /// <param name="state">回调函数的附加参数</param>
        void Copy(IEnumerable<string> source, IEnumerable<string> destination, EventHandler<PcsApiOperationCallbackArgs> callback, object state = null);

        /// <summary>
        /// 重命名文件/文件夹
        /// </summary>
        /// <param name="source">原文件/文件夹路径</param>
        /// <param name="new_name">新的文件/文件夹名</param>
        /// <param name="callback">回调函数</param>
        /// <param name="state">回调函数的附加参数</param>
        /// <remarks>
        /// 等价于同目录下的Move操作
        /// </remarks>
        void Rename(IEnumerable<string> source, IEnumerable<string> new_name, EventHandler<PcsApiOperationCallbackArgs> callback, object state = null);

    }
}
