using BaiduCloudSync.oauth.exception;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace BaiduCloudSync.oauth
{
    /// <summary>
    /// 支持文件读写的OAuth接口实现类
    /// </summary>
    public sealed class SimpleFileOAuth : IOAuth
    {
        public SimpleFileOAuth(IOAuth auth)
        {
            InternalOAuth = auth;
        }
        public SimpleFileOAuth(string file_path)
        {
            Load(file_path);
        }
        public bool IsLogin => InternalOAuth != null && InternalOAuth.IsLogin;

        public string BaiduID
        {
            get
            {
                if (!IsLogin)
                    throw new NotLoggedInException();
                return InternalOAuth.BaiduID;
            }
        }

        public string BDUSS
        {
            get
            {
                if (!IsLogin)
                    throw new NotLoggedInException();
                return InternalOAuth.BDUSS;
            }
        }

        public string SToken
        {
            get
            {
                if (!IsLogin)
                    throw new NotLoggedInException();
                return InternalOAuth.SToken;
            }
        }

        public DateTime ExpirationTime
        {
            get
            {
                if (!IsLogin)
                    throw new NotLoggedInException();
                return InternalOAuth.ExpirationTime;
            }
        }

        public object GetCaptcha()
        {
            throw new NotSupportedException();
        }

        public bool Login(string username, string password, object captcha = null)
        {
            throw new NotSupportedException();
        }

        public bool Logout()
        {
            throw new NotSupportedException();
        }

        public IOAuth InternalOAuth { get; set; }

        /// <summary>
        /// 将OAuth数据保存到文件中
        /// </summary>
        /// <param name="file_path">要写入的文件的路径</param>
        /// <exception cref="ArgumentNullException">路径为空时的异常</exception>
        /// <exception cref="IOException">写入文件时的IO异常</exception>
        public void Save(string file_path)
        {
            if (string.IsNullOrEmpty(file_path))
                throw new ArgumentNullException("file_path");

            if (InternalOAuth == null)
            {
                GlobalUtil.Tracer.GlobalTracer.TraceWarning("Trying to save a null OAuth object, make sure InternalOAuth has set to a non-null value before saving to file");
            }
            else if (!InternalOAuth.IsLogin)
            {
                throw new NotLoggedInException();
            }
            else
            {
                var json = new JObject
                {
                    { "BaiduID", InternalOAuth.BaiduID },
                    { "BDUSS", InternalOAuth.BDUSS },
                    { "SToken", InternalOAuth.SToken },
                    { "ExpirationTime", InternalOAuth.ExpirationTime }
                };
                var str_json = JsonConvert.SerializeObject(json);
                var stream_writer = new StreamWriter(file_path, false, Encoding.UTF8);
                stream_writer.Write(str_json);
                stream_writer.Close();
            }
        }
        /// <summary>
        /// 从文件中读取OAuth数据
        /// </summary>
        /// <param name="file_path"></param>
        public void Load(string file_path)
        {
            if (string.IsNullOrEmpty(file_path))
                throw new ArgumentNullException("file_path");
            
            if (!File.Exists(file_path))
                throw new ArgumentException("Path not exist");

            var stream_reader = new StreamReader(file_path, Encoding.UTF8);
            var str_json = stream_reader.ReadToEnd();
            stream_reader.Close();
            var json = JsonConvert.DeserializeObject(str_json) as JObject;

            var baidu_id = json.Value<string>("BaiduID");
            var bduss = json.Value<string>("BDUSS");
            var stoken = json.Value<string>("SToken");
            var expiration_time = json.Value<DateTime>("ExpirationTime");

            if (InternalOAuth != null)
            {
                GlobalUtil.Tracer.GlobalTracer.TraceWarning("Overwriting existed OAuth data, may cause data loss");
            }
            InternalOAuth = new SimpleOAuth(baidu_id, bduss, stoken, expiration_time);
        }
    }
}
