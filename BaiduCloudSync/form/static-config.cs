using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.IO;

namespace BaiduCloudSync
{
    //todo: 属性封装+检验范围
    public static class StaticConfig
    {
        /// <summary>
        /// 最大的上传任务数
        /// </summary>
        public static int MAX_UPLOAD_PARALLEL_TASK_COUNT = 2;
        /// <summary>
        /// 最大的下载任务数
        /// </summary>
        public static int MAX_DOWNLOAD_PARALLEL_TASK_COUNT = 2;

        /// <summary>
        /// 下载/上传清单上最多显示的任务数
        /// </summary>
        public static int MAX_LIST_SIZE = 50;

        /// <summary>
        /// 每个任务最大的下载线程数
        /// </summary>
        public static int MAX_DOWNLOAD_THREAD = 96;

        public static int MAX_DEBUG_OUTPUT_COUNT = 200;

        public static void LoadStaticConfig()
        {
            if (!Directory.Exists(".cache")) return;
            var fs = new FileInfo(".cache/config.json");
            if (fs.Exists)
            {
                try
                {
                    var sr = fs.OpenText();
                    var data = sr.ReadToEnd();
                    sr.Close();

                    var json = JsonConvert.DeserializeObject(data) as JObject;
                    MAX_DEBUG_OUTPUT_COUNT = json.Value<int>("max-debug-output-count");
                    MAX_DOWNLOAD_PARALLEL_TASK_COUNT = json.Value<int>("max-download-parallel-task-count");
                    MAX_DOWNLOAD_THREAD = json.Value<int>("max-download-thread");
                    MAX_LIST_SIZE = json.Value<int>("max-list-size");
                    MAX_UPLOAD_PARALLEL_TASK_COUNT = json.Value<int>("max-upload-parallel-task-count");

                    //validation
                    MAX_DEBUG_OUTPUT_COUNT = Math.Min(MAX_DEBUG_OUTPUT_COUNT, 1000);
                    MAX_DOWNLOAD_PARALLEL_TASK_COUNT = Math.Min(MAX_DOWNLOAD_PARALLEL_TASK_COUNT, 10);
                    MAX_DOWNLOAD_THREAD = Math.Min(MAX_DOWNLOAD_THREAD, 200);
                    MAX_LIST_SIZE = Math.Min(MAX_LIST_SIZE, 200);
                    MAX_UPLOAD_PARALLEL_TASK_COUNT = Math.Min(MAX_UPLOAD_PARALLEL_TASK_COUNT, 10);
                }
                catch (Exception ex)
                {
                    Tracer.GlobalTracer.TraceError(ex.ToString());
                }
            }
        }
        public static void SaveStaticConfig()
        {
            //validation
            MAX_DEBUG_OUTPUT_COUNT = Math.Min(MAX_DEBUG_OUTPUT_COUNT, 1000);
            MAX_DOWNLOAD_PARALLEL_TASK_COUNT = Math.Min(MAX_DOWNLOAD_PARALLEL_TASK_COUNT, 10);
            MAX_DOWNLOAD_THREAD = Math.Min(MAX_DOWNLOAD_THREAD, 200);
            MAX_LIST_SIZE = Math.Min(MAX_LIST_SIZE, 200);
            MAX_UPLOAD_PARALLEL_TASK_COUNT = Math.Min(MAX_UPLOAD_PARALLEL_TASK_COUNT, 10);

            if (!Directory.Exists(".cache")) Directory.CreateDirectory(".cache");
            var json = new JObject();
            json.Add("max-debug-output-count", MAX_DEBUG_OUTPUT_COUNT);
            json.Add("max-download-parallel-task-count", MAX_DOWNLOAD_PARALLEL_TASK_COUNT);
            json.Add("max-download-thread", MAX_DOWNLOAD_THREAD);
            json.Add("max-list-size", MAX_LIST_SIZE);
            json.Add("max-upload-parallel-task-count", MAX_UPLOAD_PARALLEL_TASK_COUNT);
            var data = JsonConvert.SerializeObject(json);
            try
            {
                var sw = new StreamWriter(".cache/config.json", false);
                sw.WriteLine(data);
                sw.Close();
            }
            catch (Exception ex)
            {
                Tracer.GlobalTracer.TraceError(ex.ToString());
            }
        }
    }
}
