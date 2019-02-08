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

        /// <summary>
        /// 预创建文件，并获得上传ID
        /// </summary>
        /// <param name="path">文件路径</param>
        /// <param name="segment_count">分段的数量</param>
        /// <param name="callback">回调函数</param>
        /// <param name="modify_time">文件的修改时间，如未指定，则为当前时间，体现在元数据中的LocalModificationTime字段</param>
        /// <param name="state">回调函数的附加参数</param>
        /// <remarks>
        /// 文件以固定的4MB大小分段，具体分段的算法可采用BaiduCloudSync.segment.FixedSizeSegmentAlgorithm
        /// API的调用顺序为: PreCreate -> foreach (分段 in 文件) do: SuperFile -> Create
        /// </remarks>
        void PreCreate(string path, int segment_count, EventHandler<PcsApiPreCreateCallbackArgs> callback, DateTime? modify_time = null, object state = null);

        /// <summary>
        /// 上传文件分段
        /// </summary>
        /// <param name="path">文件路径</param>
        /// <param name="upload_id">预创建时得到的上传ID</param>
        /// <param name="part_seq">文件分段序号，从0开始计数</param>
        /// <param name="payload">文件分段数据</param>
        /// <param name="callback">回调函数</param>
        /// <param name="host">上传的服务器域名，默认为c.pcs.baidu.com，更多的服务器域名可以通过LocateUpload获得</param>
        /// <param name="state">回调函数的附加参数</param>
        /// <remarks>
        /// 文件以固定的4MB大小分段，具体分段的算法可采用BaiduCloudSync.segment.FixedSizeSegmentAlgorithm
        /// API的调用顺序为: PreCreate -> foreach (分段 in 文件) do: SuperFile -> Create
        /// </remarks>
        void SuperFile(string path, string upload_id, int part_seq, byte[] payload, EventHandler<PcsApiSegmentUploadCallbackArgs> callback, string host, object state = null);

        /// <summary>
        /// 创建上传文件
        /// </summary>
        /// <param name="path">文件路径</param>
        /// <param name="segment_md5">每个分段MD5</param>
        /// <param name="size">文件总大小</param>
        /// <param name="upload_id">预创建时得到的上传ID</param>
        /// <param name="callback">回调函数</param>
        /// <param name="modify_time">文件修改时间，如未指定，则为当前时间，体现在元数据中的LocalModificationTime字段</param>
        /// <param name="state">回调函数的附加参数</param>
        /// <remarks>
        /// 文件以固定的4MB大小分段，具体分段的算法可采用BaiduCloudSync.segment.FixedSizeSegmentAlgorithm
        /// API的调用顺序为: PreCreate -> foreach (分段 in 文件) do: SuperFile -> Create
        /// </remarks>
        void Create(string path, IEnumerable<string> segment_md5, long size, string upload_id, EventHandler<PcsApiObjectMetaCallbackArgs> callback, DateTime? modify_time = null, object state = null);

        /// <summary>
        /// 文件秒传
        /// </summary>
        /// <param name="path">文件路径</param>
        /// <param name="content_size">文件大小</param>
        /// <param name="content_md5">文件MD5</param>
        /// <param name="slice_md5">前256kB分段的MD5</param>
        /// <param name="callback">回调函数</param>
        /// <param name="modify_time">文件修改时间，如未指定，则为当前时间，体现在元数据中的LocalModificationTime字段</param>
        /// <param name="state">回调函数的附加参数</param>
        void RapidUpload(string path, long content_size, string content_md5, string slice_md5, EventHandler<PcsApiObjectMetaCallbackArgs> callback, DateTime? modify_time = null, object state = null);

        /// <summary>
        /// 获取用于上传的PCS服务器域名
        /// </summary>
        /// <param name="callback">回调函数</param>
        /// <param name="state">回调函数的附加参数</param>
        void LocateUpload(EventHandler<PcsApiURLCallbackArgs> callback, object state = null);

        void ListHost(EventHandler<PcsApiURLCallbackArgs> callback, object state = null);

        /// <summary>
        /// 获取下载的url地址
        /// </summary>
        /// <param name="fs_id">文件ID</param>
        /// <param name="callback">回调函数</param>
        /// <param name="pre_jump">是否进行HTTP 30X预跳转</param>
        /// <param name="state">回调函数的附加参数</param>
        void Download(long fs_id, EventHandler<PcsApiURLCallbackArgs> callback, bool pre_jump = false, object state = null);

    }
}
