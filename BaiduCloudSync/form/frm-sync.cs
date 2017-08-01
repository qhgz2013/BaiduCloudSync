//remove this file!

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace BaiduCloudSync
{
    public partial class frm_sync : Form
    {

        private static List<string> _locked_data;
        private static object _lock_obj;
        static frm_sync()
        {
            _locked_data = new List<string>();
            _lock_obj = new object();
        }
        private static bool _is_locked(string path)
        {
            lock (_lock_obj)
            {
                return _locked_data.Contains(path);
            }

        }
        private static void _lock_path(string path)
        {
            lock (_lock_obj)
            {
                if (!_locked_data.Contains(path))
                    _locked_data.Add(path);
            }
        }
        private static void _unlock_path(string path)
        {
            lock (_lock_obj)
            {
                if (_locked_data.Contains(path))
                    _locked_data.Remove(path);
            }
        }

        private object _external_lock = new object();
        public frm_sync(BaiduPCS api, string local_path, string remote_path)
        {
            InitializeComponent();
            lock (_external_lock)
            {
                _api = api;
                if (api == null) throw new ArgumentNullException("api");
                if (string.IsNullOrEmpty(local_path)) throw new ArgumentNullException("local_path");
                if (string.IsNullOrEmpty(remote_path)) throw new ArgumentNullException("remote_path");

                if (_is_locked(remote_path)) throw new ArgumentException("remote_path is locked by other Sync form");

                _local_path = local_path.Replace(@"\", "/");
                if (_local_path.EndsWith("/") && _local_path.Length > 1) _local_path = _local_path.Substring(0, _local_path.Length - 1);
                _remote_path = remote_path;
                if (_remote_path.EndsWith("/") && _remote_path.Length > 1) _remote_path = _remote_path.Substring(0, _remote_path.Length - 1);

                _lock_path(_remote_path);

                _init_class();
            }
        }
        private delegate void NoArgSTA();
        private void frm_sync_Load(object sender, EventArgs e)
        {
            ThreadPool.QueueUserWorkItem(delegate
            {

                Invoke(new NoArgSTA(delegate 
                {
                    lStatus.Text = "正在获取网盘信息";
                }));

                _fetch_remote_data_from_api();

                Invoke(new NoArgSTA(delegate
                {
                    lStatus.Text = "正在获取本地文件信息";
                }));
                _fetch_local_data_from_cache();

                _fetch_local_data_from_path(false);

            });

        }


        //本地地址。不含最后的文件夹分隔符
        private string _local_path;
        private BaiduPCS _api;
        //网盘地址。不含最后的文件夹分隔符
        private string _remote_path;

        private string _sync_setting_path;

        private List<TrackedData> _local_file_data;
        private List<TrackedData> _remote_file_data;
        private List<TrackedData> _sealed_remote_file_data;
        //其他初始化工作
        private void _init_class()
        {
            var dir_info = new DirectoryInfo(_local_path);
            if (!dir_info.Exists) throw new IOException("文件夹不存在");

            _sync_setting_path = _local_path + "/.sync";
            var sync_dir_info = new DirectoryInfo(_sync_setting_path);

            if (!sync_dir_info.Exists) sync_dir_info.Create();

            _local_file_data = null;
            _remote_file_data = null;
            _sealed_remote_file_data = null;
        }
        //从api中获取数据(同步执行),同步保存到./.sync/remote_data中
        private void _fetch_remote_data_from_api()
        {
            _remote_file_data = new List<TrackedData>();
            Queue<string> fetch_dirs = new Queue<string>();

            //bfs fetch
            fetch_dirs.Enqueue(_remote_path);

            while (fetch_dirs.Count > 0)
            {
                var current_dir = fetch_dirs.Dequeue();
                var files = _api.GetFileList(current_dir);

                Invoke(new NoArgSTA(delegate
                {
                    lStatus.Text = "正在获取网盘信息 (队列:" + fetch_dirs.Count + " ,文件数:" + _remote_file_data.Count + ")";
                }));

                foreach (var item in files)
                {
                    var data = new TrackedData();
                    data.ContentSize = item.Size;
                    data.CreationTime = util.FromUnixTimestamp(item.LocalCTime);
                    data.IsDir = item.IsDir;
                    data.MD5 = item.MD5;
                    data.ModifiedTime = util.FromUnixTimestamp(item.LocalMTime);
                    data.Path = item.Path;

                    _remote_file_data.Add(data);
                    if (item.IsDir)
                        fetch_dirs.Enqueue(item.Path);
                }
            }

            //writing to files
            var ofs = new FileStream(_sync_setting_path + "/remote_data", FileMode.Create, FileAccess.Write, FileShare.Read);
            var count = _remote_file_data.Count;
            var bs = new byte[4];
            bs[0] = (byte)((count >> 24) & 0xff);
            bs[1] = (byte)((count >> 16) & 0xff);
            bs[2] = (byte)((count >> 8) & 0xff);
            bs[3] = (byte)(count & 0xff);
            ofs.Write(bs, 0, 4);
            foreach (var item in _remote_file_data)
            {
                item.WriteSerialiedData(ofs);
            }
            ofs.Close();
        }
        //从本地缓存 (./.sync/sealed_remote_data)中获取上一次远端的数据 (未初始化时为null，已初始化但为空则为count=0）
        private void _fetch_remote_data_from_cache()
        {
            _sealed_remote_file_data = null;
            if (!File.Exists(_sync_setting_path + "/sealed_remote_data")) return;
            _sealed_remote_file_data = new List<TrackedData>();
            var ifs = new FileStream(_sync_setting_path + "/sealed_remote_data", FileMode.Open, FileAccess.Read, FileShare.Read);

            var buffer = new byte[4];
            int nread = 0;
            while (nread < 4)
            {
                nread += ifs.Read(buffer, nread, 4 - nread);
            }
            int count = (((((buffer[0] << 8) | buffer[1]) << 8) | buffer[2]) << 8) | buffer[3];
            for (int i = 0; i < count; i++)
            {
                var data = new TrackedData(ifs);
                _sealed_remote_file_data.Add(data);
            }
            ifs.Close();
        }

        //从本地缓存 (./.sync/local_data)中获取本地的文件数据
        private void _fetch_local_data_from_cache()
        {
            _local_file_data = new List<TrackedData>();
            if (!File.Exists(_sync_setting_path + "/local_data")) return;
            var ifs = new FileStream(_sync_setting_path + "/local_data", FileMode.Open, FileAccess.Read, FileShare.Read);

            var buffer = new byte[4];
            int nread = 0;
            while (nread < 4)
            {
                nread += ifs.Read(buffer, nread, 4 - nread);
            }
            int count = (((((buffer[0] << 8) | buffer[1]) << 8) | buffer[2]) << 8) | buffer[3];
            for (int i = 0; i < count; i++)
            {
                var data = new TrackedData(ifs);
                _local_file_data.Add(data);
            }
            ifs.Close();
        }
        //从本地中获取文件数据，同步写入到本地缓存 (./.sync/local_data)中
        private void _fetch_local_data_from_path(bool force_mode)
        {
            if (_local_file_data == null) _local_file_data = new List<TrackedData>();

            //强制模式，停用所有缓存数据
            if (force_mode) _local_file_data.Clear();

            _local_file_data.Sort();

            //获取本地文件的所有文件信息
            var local_directories = new Queue<string>();
            local_directories.Enqueue(_local_path);

            var default_datetime_sensor = new TimeSpan(0, 0, 10); //默认在10s内的文件时间差异会被忽略掉

            while (local_directories.Count > 0)
            {
                var cur_path = local_directories.Dequeue();
                var dir_info = new DirectoryInfo(cur_path);

                Invoke(new NoArgSTA(delegate
                {
                    lStatus.Text = "正在获取本地信息 (队列:" + local_directories.Count + " ,文件数:" + _local_file_data.Count + ")";
                }));

                foreach (var item in dir_info.GetFiles())
                {
                    var data = new TrackedData();
                    data.CreationTime = item.CreationTime;
                    data.ModifiedTime = item.LastWriteTime;
                    data.ContentSize = (ulong)item.Length;
                    data.Path = item.FullName.Substring(_local_path.Length);
                    data.IsDir = false;

                    var cached_data = from TrackedData dat
                                      in _local_file_data
                                      where dat.Path == data.Path
                                      select dat;
                    if (cached_data.Count() == 0)
                    {
                        //non match, adding to the cache

                        //todo: visualable output
                        var file_data = _api.GetRapidUploadArguments(item.FullName);
                        data.MD5 = file_data.content_md5;
                        _local_file_data.Add(data);
                    }
                    else
                    {
                        //matched, comparing data info
                        var cached_element = cached_data.First();
                        if (_in_duration(cached_element.CreationTime, data.CreationTime, default_datetime_sensor) && _in_duration(cached_element.ModifiedTime, data.ModifiedTime, default_datetime_sensor) && cached_element.ContentSize == data.ContentSize)
                        {
                            //info matched, ignored
                        }
                        else
                        {
                            //info out-dated, update

                            //todo: visualable output
                            var file_data = _api.GetRapidUploadArguments(item.FullName);
                            data.MD5 = file_data.content_md5;
                            _local_file_data.Remove(cached_element);
                            _local_file_data.Add(data);
                        }
                    }
                }
            }

            //writing to files
            var ofs = new FileStream(_sync_setting_path + "/local_data", FileMode.Create, FileAccess.Write, FileShare.Read);
            var count = _remote_file_data.Count;
            var bs = new byte[4];
            bs[0] = (byte)((count >> 24) & 0xff);
            bs[1] = (byte)((count >> 16) & 0xff);
            bs[2] = (byte)((count >> 8) & 0xff);
            bs[3] = (byte)(count & 0xff);
            ofs.Write(bs, 0, 4);
            foreach (var item in _remote_file_data)
            {
                item.WriteSerialiedData(ofs);
            }
            ofs.Close();
        }
        private bool _in_duration(DateTime t1, DateTime t2, TimeSpan ts)
        {
            if (t1 > t2)
                return (t1 - t2) <= ts;
            else
                return (t2 - t1) <= ts;
        }

        //将本地文件同步到服务器，服务器上的文件会相应修改，本地文件不变
        public void SyncUp()
        {

        }


        //文件冲突，即本地储存的网盘文件信息与本次获取的网盘文件信息出现差异
        public event EventHandler<ConflictFilesEventArg> FilesConflicted;

        private void tLocalPath_TextChanged(object sender, EventArgs e)
        {
            //bChangeLocalPath.Enabled = true;
        }

        private void tRemotePath_TextChanged(object sender, EventArgs e)
        {
            //bChangeRemotePath.Enabled = true;
        }

        private void bChangeLocalPath_Click(object sender, EventArgs e)
        {
            bChangeLocalPath.Enabled = false;
            var tmp_path = _local_path;
            try
            {
                _local_path = tLocalPath.Text;
                _init_class();
            }
            catch (Exception)
            {
                MessageBox.Show("读取文件夹信息出错");
                bChangeLocalPath.Enabled = true;
                _local_path = tmp_path;
            }
        }

        private void bChangeRemotePath_Click(object sender, EventArgs e)
        {
            bChangeRemotePath.Enabled = false;
            var tmp_path = _remote_path;
            try
            {
                if (string.IsNullOrEmpty(tRemotePath.Text)) throw new ArgumentNullException("remote path");
                if (!tRemotePath.Text.EndsWith("/")) tRemotePath.Text += "/";
                _remote_path = tRemotePath.Text;
                _init_class();
            }
            catch (Exception)
            {
                MessageBox.Show("修改文件夹出错");
                bChangeRemotePath.Enabled = true;
                _remote_path = tmp_path;
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {

        }
    }

    [Serializable]
    public struct TrackedData : IComparable
    {
        public string Path;
        public string MD5;
        public ulong ContentSize;
        public DateTime CreationTime;
        public DateTime ModifiedTime;
        public bool IsDir;
        public TrackedData(string path, string md5, ulong contentSize, DateTime creationTime, DateTime modifiedTime, bool isDir)
        {
            Path = path;
            MD5 = md5;
            ContentSize = contentSize;
            CreationTime = creationTime;
            ModifiedTime = modifiedTime;
            IsDir = isDir;
        }
        public byte[] SerializeData()
        {
            var serialized_path = Encoding.UTF8.GetBytes(Path);

            ushort len = serialized_path.Length > 0xffff ? (ushort)0xffff : (ushort)serialized_path.Length;
            var ret = new byte[len + 43];
            // 2+x B
            ret[0] = (byte)(len >> 8);
            ret[1] = (byte)(len & 0xff);
            Array.Copy(serialized_path, 0, ret, 2, len);
            // 16 B
            if (string.IsNullOrEmpty(MD5)) MD5 = "00000000000000000000000000000000";
            var serialized_md5 = util.Hex(MD5);
            Array.Copy(serialized_md5, 0, ret, 2 + len, 16);
            // 8 B
            ulong temp_size = ContentSize;
            for (int i = 7; i >= 0; i--)
            {
                ret[len + 18 + i] = (byte)(temp_size & 0xff);
                temp_size >>= 8;
            }
            // 8 B
            temp_size = (ulong)CreationTime.Ticks;
            for (int i = 7; i >= 0; i--)
            {
                ret[len + 26 + i] = (byte)(temp_size & 0xff);
                temp_size >>= 8;
            }

            // 8 B
            temp_size = (ulong)ModifiedTime.Ticks;
            for (int i = 7; i >= 0; i--)
            {
                ret[len + 34 + i] = (byte)(temp_size & 0xff);
                temp_size >>= 8;
            }
            // 1 B
            ret[len + 42] = (byte)(IsDir ? 1 : 0);
            return ret;
        }
        public void WriteSerialiedData(Stream ostream)
        {
            var data = SerializeData();
            ostream.Write(data, 0, data.Length);
        }
        public void ReadSerializedData(Stream istream)
        {
            var buffer = new byte[16];
            //2+x B
            int readed_bytes = 0;
            while (readed_bytes < 2)
            {
                readed_bytes += istream.Read(buffer, readed_bytes, 2 - readed_bytes);
            }

            ushort len = (ushort)(buffer[0] << 8 | buffer[1]);
            buffer = new byte[len];
            readed_bytes = 0;
            while (readed_bytes < len)
            {
                readed_bytes += istream.Read(buffer, readed_bytes, len - readed_bytes);
            }

            Path = Encoding.UTF8.GetString(buffer);
            //16 B
            buffer = new byte[16];
            readed_bytes = 0;
            while (readed_bytes < 16)
            {
                readed_bytes += istream.Read(buffer, readed_bytes, 16 - readed_bytes);
            }

            MD5 = util.Hex(buffer);
            //8 B
            ContentSize = 0;
            readed_bytes = 0;
            while (readed_bytes < 8)
            {
                readed_bytes += istream.Read(buffer, readed_bytes, 8 - readed_bytes);
            }
            for (int i = 0; i < 8; i++)
            {
                ContentSize <<= 8;
                ContentSize |= buffer[i];
            }
            //8B
            ulong temp_uint64 = 0;
            readed_bytes = 0;
            while (readed_bytes < 8)
            {
                readed_bytes += istream.Read(buffer, readed_bytes, 8 - readed_bytes);
            }
            for (int i = 0; i < 8; i++)
            {
                temp_uint64 <<= 8;
                temp_uint64 |= buffer[i];
            }
            CreationTime = new DateTime((long)temp_uint64);

            //8B
            temp_uint64 = 0;
            readed_bytes = 0;
            while (readed_bytes < 8)
            {
                readed_bytes += istream.Read(buffer, readed_bytes, 8 - readed_bytes);
            }
            for (int i = 0; i < 8; i++)
            {
                temp_uint64 <<= 8;
                temp_uint64 |= buffer[i];
            }
            ModifiedTime = new DateTime((long)temp_uint64);

            //1B
            readed_bytes = 0;
            while (readed_bytes < 1)
            {
                readed_bytes += istream.Read(buffer, readed_bytes, 1 - readed_bytes);
            }
            IsDir = (buffer[0] == 0) ? false : true;
        }

        public int CompareTo(object obj)
        {
            return Path.CompareTo(obj);
        }

        public TrackedData(Stream istream)
        {
            Path = null; MD5 = null; ContentSize = 0; CreationTime = new DateTime(0); ModifiedTime = new DateTime(0); IsDir = false;
            ReadSerializedData(istream);
        }
    }

    public class ConflictFilesEventArg : EventArgs
    {
        public readonly List<TrackedData> ConflictFileList;
    }
}
