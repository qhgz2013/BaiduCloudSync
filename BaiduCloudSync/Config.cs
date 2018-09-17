using GlobalUtil;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace BaiduCloudSync
{
    /// <summary>
    /// 网盘设置
    /// </summary>
    public class Config
    {
        /// <summary>
        /// 网盘的全局设置
        /// </summary>
        public static Config GlobalConfig { get; private set; }
        /// <summary>
        /// 网盘设置的默认文件路径
        /// </summary>

        public const string DEFAULT_CONFIG_FILENAME = "data/config.json";


        #region config parameters
        private string _cookie_filename = "data/cookie.dat";
        #endregion


        #region config getter and setter
        /// <summary>
        /// 获取或设置cookie保存文件路径的值
        /// </summary>
        public string CookieFileName
        {
            get
            {
                return _cookie_filename;
            }
            set
            {
                if (string.IsNullOrEmpty(value))
                    throw new ArgumentNullException("Could not set an empty cookie filename");
                string temp;
                if (string.IsNullOrEmpty(temp = System.IO.Path.GetFullPath(value)))
                    throw new ArgumentException("Could not locate cookie file");
                _cookie_filename = temp;
            }
        }
        #endregion
        static Config()
        {
            GlobalConfig = new Config();
        }
        public Config()
        {

        }

        private T _load_key<T>(JObject obj, string key_name)
        {
            JToken value_token;
            if (!obj.TryGetValue(key_name, out value_token))
                throw new KeyNotFoundException("key " + key_name + " not found");
            return value_token.Value<T>();
        }

        private void _save_key(JObject obj, string key_name, string val)
        {
            obj.Add(key_name, JToken.FromObject(val));
        }

        /// <summary>
        /// 读取设置
        /// </summary>
        /// <param name="path_name">网盘设置文件路径</param>
        public void LoadConfig(string path_name = DEFAULT_CONFIG_FILENAME)
        {
            if (string.IsNullOrEmpty(path_name))
                throw new ArgumentNullException("path_name");
            if (!File.Exists(path_name))
                throw new FileNotFoundException();
            var content = File.ReadAllText(path_name);

            var json = JsonConvert.DeserializeObject(content) as JObject;

            CookieFileName = _load_key<string>(json, "CookieFileName");
        }
        public void SaveConfig(string path_name = DEFAULT_CONFIG_FILENAME)
        {
            if (string.IsNullOrEmpty(path_name))
                throw new ArgumentNullException("path_name");
            var json = new JObject();

            _save_key(json, "CookieFileName", CookieFileName);

            var parent_dir_info = new FileInfo(path_name).Directory;
            if (!parent_dir_info.Exists)
                util.CreateDirectory(parent_dir_info.FullName);
            File.WriteAllText(path_name, JsonConvert.SerializeObject(json));
        }
    }
}
