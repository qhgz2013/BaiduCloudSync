using GlobalUtil;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BaiduCloudSync.api
{
    /// <summary>
    /// PCS文件系统路径
    /// </summary>
    public class PcsPath
    {
        private const string INVALID_PATH_CHAR = "\\/:*?<>|\"";
        // returns s containing invalid char or not
        private bool _check_invalid_path_char(string s)
        {
            foreach (var ch in INVALID_PATH_CHAR)
            {
                if (s.Contains(ch))
                    return true;
            }
            return false;
        }
        public PcsPath(string path)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentNullException("path");
            path = path.Replace("\\", "/");
            var path_split = path.Split('/');

            if (path_split.Length <= 1)
                throw new ArgumentException("Invalid path");

            if (!string.IsNullOrEmpty(path_split[0]))
                Tracer.GlobalTracer.TraceWarning("PCS path should begins with \"/\", but got \"" + path_split[0] + "/\", this prefix is ignored, and treated as error in future");

            List<string> path_tree = new List<string>();
            for (int start_idx = 1; start_idx < path_split.Length; start_idx++)
            {
                if (string.IsNullOrEmpty(path_split[start_idx]))
                    continue;
                if (path_split[start_idx] == ".")
                    continue;
                if (_check_invalid_path_char(path_split[start_idx]))
                    throw new ArgumentException("Invalid path string: " + path_split[start_idx]);
                if (path_split[start_idx] == "..")
                {
                    if (path_tree.Count == 0)
                        throw new ArgumentException("Invalid path: no parent before meeting \"..\"");
                    path_tree.RemoveAt(path_tree.Count - 1);
                }
                else
                    path_tree.Add(path_split[start_idx]);
            }

            if (path_tree.Count == 0)
            {
                _name_no_ext = "";
                _ext = "";
                _parent_dir = null;
            }
            else
            {
                var name = path_tree.Last();
                path_tree.RemoveAt(path_tree.Count - 1);
                var sb = new StringBuilder().Append("/");
                foreach (var dir_name in path_tree)
                    sb.Append(dir_name).Append("/");
                if (sb.Length > 1)
                    sb.Remove(sb.Length - 1, 1); // removing last "/"

                _parent_dir = new PcsPath(sb.ToString());
                var ext_split = name.LastIndexOf('.');
                if (ext_split <= 0 || ext_split == name.Length - 1)
                {
                    // only first ".xxx" or "xxxx." is found, treated as NO EXTENSION
                    _name_no_ext = name;
                    _ext = "";
                }
                else
                {
                    _name_no_ext = name.Substring(0, ext_split);
                    _ext = name.Substring(ext_split + 1);
                }
            }
        }

        private PcsPath _parent_dir;
        private string _name_no_ext;
        private string _ext;
        /// <summary>
        /// 文件夹名称
        /// </summary>
        public string Name { get { return string.IsNullOrEmpty(_ext) ? _name_no_ext : (_name_no_ext + "." + _ext); } }
        /// <summary>
        /// 文件夹绝对路径
        /// </summary>
        public string FullPath
        {
            get
            {
                if (_parent_dir == null) return "/";
                else if (_parent_dir.FullPath == "/") return _parent_dir + Name;
                else return _parent_dir + "/" + Name;
            }
        }
        /// <summary>
        /// 父级文件夹
        /// </summary>
        public PcsPath Parent { get { return _parent_dir; } }
        /// <summary>
        /// 不包含扩展名的文件名
        /// </summary>
        public string NameWithoutExtension { get { return _name_no_ext; } }
        /// <summary>
        /// 文件扩展名
        /// </summary>
        public string Extension { get { return _ext; } }
        public override string ToString()
        {
            return FullPath;
        }
    }
}
