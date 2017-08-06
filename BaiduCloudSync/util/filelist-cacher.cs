using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using static BaiduCloudSync.BaiduPCS;

namespace BaiduCloudSync
{
    //用于缓存网盘文件结构数据的类
    public class FileListCacher
    {
        public FileListCacher(BaiduPCS api)
        {
            if (api == null) throw new ArgumentNullException("api");
            _api = api;
            _data = new Dictionary<string, ObjectMetadata>();
            _unfetched_dir = new SortedSet<string>();
            _unfetched_dir.Add("/");
            _sync_lock = new ReaderWriterLock();
        }
        private BaiduPCS _api;

        private Dictionary<string, ObjectMetadata> _data;
        private SortedSet<string> _unfetched_dir;
        private ReaderWriterLock _sync_lock;
        private static bool _enable_tracing = false;
        //warning: 所有private函数不加线程锁 所有public函数必须加线程锁

        #region Private functions
        //移除指定路径下的所有文件信息缓存 (nothrow)
        private void _remove_files_from_data(string path)
        {
            var remove_list = new List<string>();
            if (!DirValidating(path)) throw new ArgumentException("path is invalid");

            //递归删除，即移除该文件夹下所有子文件夹的缓存信息
            foreach (var item in _data)
            {
                if (item.Key.StartsWith(path) && item.Key != path)
                    remove_list.Add(item.Key);
            }
            foreach (var item in remove_list)
            {
                if (!_data.Remove(item))
                {
                    Tracer.GlobalTracer.TraceWarning("Failed to remove " + path + " from __data");
                }
            }
            _unfetched_dir.Add(path);
        }
        //从api处更新指定目录的信息 (throwable)
        private void _add_files_from_api(string path)
        {
            Stack<string> parent_dirs = new Stack<string>();
            //从子到根的压栈
            do
            {
                if (!_dir_data_is_loaded(path))
                    parent_dirs.Push(path);
                path = Directory_GetParentDir(path, false);
            } while (!string.IsNullOrEmpty(path));
            //从根目录开始获取信息
            while (parent_dirs.Count > 0)
            {
                var dir = parent_dirs.Pop();
                //移除已有信息
                _remove_files_from_data(dir);

                //var files = _api.GetFileList(dir);
                ObjectMetadata[] files = null;
                List<ObjectMetadata> files_list = new List<ObjectMetadata>();
                const int _MAX_COUNT = 1000;
                int page = 1;
                do
                {
                    try { files = _api.GetFileList(dir, page: page, count: _MAX_COUNT); }
                    catch (ErrnoException ex)
                    {
                        if (ex.Errno == 9) Tracer.GlobalTracer.TraceWarning("Error 9 detected when fetching file list: no such directory");
                        else Tracer.GlobalTracer.TraceError("Error " + ex.Errno + " detected when fetching file list: unknown code");
                    }
                    if (files == null)
                    {
                        Tracer.GlobalTracer.TraceWarning("Fetching file list failed, retry in 1 second");
                        Thread.Sleep(1000);
                    }
                    else
                    {
                        files_list.AddRange(files);
                        if (files.Length == _MAX_COUNT) page++;
                    }
                } while (files == null || files.Length == _MAX_COUNT);
                if (!_unfetched_dir.Remove(dir))
                {
                    Tracer.GlobalTracer.TraceWarning("Removing path " + path + " failed from the _unfetched_dir list!");
                    throw new InvalidOperationException();
                }
                foreach (var item in files_list)
                {
                    var item_path = item.Path;
                    if (item.IsDir) item_path += "/";
                    if (_data.ContainsKey(item_path))
                    {
                        _data[item_path] = item;
                    }
                    else
                    {
                        _data.Add(item_path, item);
                    }
                    //若是文件夹类型，则添加到未缓存文件夹列表中
                    if (item.IsDir && !_unfetched_dir.Contains(item.Path + "/") && !_unfetched_dir.Add(item.Path + "/"))
                    {
                        Tracer.GlobalTracer.TraceWarning("Adding path " + item.Path + " to _unfetched_dir failed!");
                        throw new InvalidOperationException();
                    }

                }

            }
        }
        //获取指定路径下的所有文件: 允许非主线程调用 (throwable)
        //注意：未初始化时会有加载延时，建议：使用非主线程调用
        private ObjectMetadata[] _dir_get_files(string path)
        {
            var ret = new List<ObjectMetadata>();
            if (!_dir_data_is_loaded(path))
            {
                //从api处获取文件信息
                if (_api == null) throw new ArgumentNullException("pcsAPI");
                try
                {
                    _add_files_from_api(path);
                }
                catch (Exception ex)
                {
                    throw new WebException("获取数据出错，请稍后尝试", ex);
                }
            }
            //返回
            foreach (var item in _data)
            {
                if (!item.Key.StartsWith(path) || item.Key == path) continue;
                var file_name = item.Key.Substring(path.Length);
                //文件
                if (!file_name.Contains("/"))
                    ret.Add(item.Value);
                //子文件夹
                else if (file_name.EndsWith("/") && !file_name.Substring(0, file_name.Length - 1).Contains("/"))
                    ret.Add(item.Value);

            }
            return ret.ToArray();

        }
        //判断该目录的数据是否已经加载 (nothrow)
        private bool _dir_data_is_loaded(string path)
        {
            //在未加载列表中含有该路径，返回false
            if (_unfetched_dir.Contains(path)) return false;
            //在未加载列表中含有该路径的父级路径，返回false
            do
            {
                path = Directory_GetParentDir(path, false);
                if (_unfetched_dir.Contains(path)) return false;
            } while (!string.IsNullOrEmpty(path));

            return true;
        }

        //刷新该文件夹的信息（包含全部子文件夹） (throwable)
        private IEnumerable<ObjectMetadata> _dir_refresh_files(string path)
        {
            _remove_files_from_data(path);
            return _dir_get_files(path);
        }
        #endregion

        #region Public functions
        /// <summary>
        /// 返回该路径是否加载
        /// </summary>
        /// <param name="path">路径</param>
        /// <returns>是否已经完成加载</returns>
        public bool Directory_IsLoaded(string path)
        {
            if (_enable_tracing) Tracer.GlobalTracer.TraceInfo("FileListCacher.Directory_IsLoaded called: string path=" + path);
            _sync_lock.AcquireReaderLock(Timeout.Infinite);
            bool ret = _dir_data_is_loaded(path);
            _sync_lock.ReleaseReaderLock();
            return ret;
        }
        /// <summary>
        /// 获取指定文件夹路径下的文件列表
        /// </summary>
        /// <param name="path">文件夹路径</param>
        /// <returns>失败时返回null</returns>
        public ObjectMetadata[] GetFileList(string path)
        {
            if (_enable_tracing) Tracer.GlobalTracer.TraceInfo("FileListCacher.GetFileList called: string path=" + path);
            if (!DirValidating(path)) return null;
            ObjectMetadata[] ret = null;
            try
            {
                _sync_lock.AcquireWriterLock(Timeout.Infinite);
                ret = _dir_get_files(path);
            }
            catch (Exception ex)
            {
                Tracer.GlobalTracer.TraceError(ex.ToString());
            }
            finally
            {
                _sync_lock.ReleaseWriterLock();
            }
            return ret;
        }
        /// <summary>
        /// 移除指定路径的文件
        /// </summary>
        /// <param name="path"></param>
        public void RemoveFileCache(string path)
        {
            if (_enable_tracing) Tracer.GlobalTracer.TraceInfo("FileListCacher.RemoveFileCache called: string path=" + path);
            if (!DirValidating(path)) return;
            _sync_lock.AcquireWriterLock(Timeout.Infinite);
            _remove_files_from_data(path);
            _sync_lock.ReleaseWriterLock();
        }
        public ObjectMetadata[] RefreshFileList(string path)
        {
            if (_enable_tracing) Tracer.GlobalTracer.TraceInfo("FileListCacher.RefreshFileList called: string path=" + path);
            if (!DirValidating(path)) return null;
            _sync_lock.AcquireWriterLock(Timeout.Infinite);
            ObjectMetadata[] ret = null;
            try
            {
                _remove_files_from_data(path);
                ret = _dir_get_files(path);
            }
            catch (Exception ex)
            {
                Tracer.GlobalTracer.TraceError(ex.ToString());
            }
            finally
            {
                _sync_lock.ReleaseWriterLock();
            }
            return ret;
        }
        //获取单个文件的信息
        //注意：未初始化时会有加载延时，建议：使用非主线程调用
        public ObjectMetadata GetData(string path)
        {
            if (_enable_tracing) Tracer.GlobalTracer.TraceInfo("FileListCacher.GetData called: string path=" + path);
            ObjectMetadata ret = new ObjectMetadata();
            if (!PathValidating(path)) return ret;

            //从内存中返回
            _sync_lock.AcquireReaderLock(Timeout.Infinite);
            bool suc_get_from_cache = false;
            if (_data.ContainsKey(path))
            {
                suc_get_from_cache = true;
                ret = _data[path];
            }
            _sync_lock.ReleaseReaderLock();
            if (suc_get_from_cache) return ret;

            //获取父级文件夹路径 
            var parent_dir = GetParentDir(path);
            if (string.IsNullOrEmpty(parent_dir))
            {
                return ret;
            }

            //已加载父级目录的信息但无法找到，则为不存在
            _sync_lock.AcquireReaderLock(Timeout.Infinite);
            if (_dir_data_is_loaded(parent_dir))
            {
                _sync_lock.ReleaseReaderLock();
                return ret;
            }
            _sync_lock.ReleaseReaderLock();

            _sync_lock.AcquireWriterLock(Timeout.Infinite);
            try
            {
                //更新api缓存
                _add_files_from_api(path);
                if (_data.ContainsKey(path))
                    ret = _data[path];
            }
            catch (Exception ex)
            {
                Tracer.GlobalTracer.TraceError(ex.ToString());
            }
            finally
            {
                _sync_lock.ReleaseWriterLock();
            }
            return ret;
        }
        #endregion

        #region utility functions
        //路径的非法字符
        private const string _path_invalid_words = "<>|*?/\\\":";
        /// <summary>
        /// 验证文件夹路径是否合法
        /// </summary>
        /// <param name="path">输入的文件夹路径</param>
        /// <returns>合法: true, 非法: false</returns>
        public static bool DirValidating(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            if (!path.StartsWith("/")) return false;
            if (!path.EndsWith("/")) return false;
            var paths = path.Split('/');
            for (int i = 1; i < paths.Length - 1; i++)
            {
                foreach (var ch in paths[i])
                {
                    if (_path_invalid_words.Contains(ch)) return false;
                }
            }
            return true;
        }
        /// <summary>
        /// 验证文件路径是否合法
        /// </summary>
        /// <param name="path">输入的文件路径</param>
        /// <returns>合法: true, 非法: false</returns>
        public static bool FileValidating(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            if (!path.StartsWith("/")) return false;
            if (path.EndsWith("/")) return false;
            var paths = path.Split('/');
            for (int i = 1; i < paths.Length; i++)
            {
                foreach (var ch in paths[i])
                {
                    if (_path_invalid_words.Contains(ch)) return false;
                }
            }
            return true;
        }
        /// <summary>
        /// 验证路径是否合法（含文件夹和文件）
        /// </summary>
        /// <param name="path">输入的路径</param>
        /// <returns>合法: true, 非法: false</returns>
        public static bool PathValidating(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            if (path.EndsWith("/")) return DirValidating(path);
            else return FileValidating(path);
        }
        /// <summary>
        /// 返回文件夹的父级文件夹路径
        /// </summary>
        /// <param name="path">文件夹的路径</param>
        /// <param name="validating_check">是否对路径进行有效性检测</param>
        /// <returns>根路径时返回empty string，不合法时返回null</returns>
        public static string Directory_GetParentDir(string path, bool validating_check = true)
        {
            if (validating_check && !DirValidating(path)) return null;
            if (path == "/") return string.Empty;
            path = path.Substring(0, path.Length - 1); //除去最后的"/"
            var index = path.LastIndexOf('/');
            return path.Substring(0, index + 1); //包含上一级最后的"/"
        }
        /// <summary>
        /// 返回文件的父级文件夹路径
        /// </summary>
        /// <param name="path">文件的路径</param>
        /// <param name="validating_check">是否对路径进行有效性检测</param>
        /// <returns>根路径时返回empty string，不合法时返回null</returns>
        public static string File_GetParentDir(string path, bool validating_check = true)
        {
            if (validating_check & !FileValidating(path)) return null;
            var index = path.LastIndexOf('/');
            return path.Substring(0, index + 1);
        }
        /// <summary>
        /// 返回路径(包含文件和文件夹)的父级文件夹目录
        /// </summary>
        /// <param name="path">路径</param>
        /// <param name="validating_check">是否对路径进行有效性检测</param>
        /// <returns>根路径时返回empty string，不合法时返回null</returns>
        public static string GetParentDir(string path, bool validating_check = true)
        {
            if (validating_check && !PathValidating(path)) return null;
            if (path.EndsWith("/")) return Directory_GetParentDir(path, false);
            else return File_GetParentDir(path, false);
        }
        #endregion
    }

}
