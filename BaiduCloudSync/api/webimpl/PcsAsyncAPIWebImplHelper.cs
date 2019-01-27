using GlobalUtil;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BaiduCloudSync.api.webimpl
{
    internal sealed class PcsAsyncAPIWebImplHelper
    {
        private static Random _random = new Random();
        /// <summary>
        /// 获取LogID参数
        /// </summary>
        public static string LogID
        {
            get
            {
                double log_ts = Util.ToUnixTimestamp(DateTime.Now) * 10000;
                // avoiding dumplication
                log_ts += _random.NextDouble();
                return Convert.ToBase64String(Encoding.UTF8.GetBytes(log_ts.ToString()));
            }
        }

        /// <summary>
        /// 检查API数据是否有错误标识
        /// </summary>
        /// <param name="response_json">json API数据</param>
        /// <returns>若无错误，返回null，否则返回对应的错误信息</returns>
        public static string CheckJson(JObject response_json)
        {
            var errno = response_json.Value<int>("errno");
            if (errno == 0) errno = response_json.Value<int>("error_code"); // for some api
            if (errno == 0) return null;
            string ret = $"Error: {errno}";
            if (response_json.TryGetValue("msg", out var msg))
                ret += $", Message: {msg.ToString()}";
            return ret;
        }

        public static PcsMetadata ReadMetadataFromJson(JObject json)
        {
            //string md5 = json.TryGetValue("md5", out JToken tmp_md5) ? tmp_md5.ToString() : string.Empty;
            return new PcsMetadata
            {
                FSID = json.Value<long>("fs_id"),
                IsDirectory = json.Value<int>("isdir") != 0,
                LocalCreationTime = Util.FromUnixTimestamp(json.Value<long>("local_ctime")),
                LocalModificationTime = Util.FromUnixTimestamp(json.Value<long>("local_mtime")),
                PathInfo = new PcsPath(json.Value<string>("path")),
                ServerCreationTime = Util.FromUnixTimestamp(json.Value<long>("server_ctime")),
                ServerModificationTime = Util.FromUnixTimestamp(json.Value<long>("server_mtime")),
                Size = json.Value<long>("size"),
                MD5 = json.Value<string>("md5")
            };
        }
    }
}
