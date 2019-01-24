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
        void ListDir(string path, EventHandler<PcsApiMultiObjectMetaCallbackArgs> callback, PcsFileOrder order = PcsFileOrder.Name, bool desc = false, int page = 1, int count = 1000, object state = null);

    }
}
