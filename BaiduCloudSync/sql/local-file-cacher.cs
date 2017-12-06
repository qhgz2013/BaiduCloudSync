using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using GlobalUtil;

namespace BaiduCloudSync
{
    public delegate void LocalFileIOCallback(string path, long current, long total);
    public struct LocalFileData
    {
        public string Path;
        public string Path_SHA1;
        public string MD5;
        public long Size;
        public uint CRC32;
        public string Slice_MD5;
        public string SHA1;
        public DateTime CTime;
        public DateTime MTime;
    }
    public delegate void LocalFileIOFinishCallback(LocalFileData data);
    public delegate void LocalFileIOAbortedCallback(string path);
    //todo: 增加缓存大小修改
    public class LocalFileCacher : IDisposable
    {
        private const string _CACHE_PATH = "data";
        private const string _LOCAL_CACHE_NAME = _CACHE_PATH + "/local-track.db";


        //文件系统是否大小写敏感（WIN下为false，linux下为true）
        private const bool _FILE_SYSTEM_CASE_SENSITIVE = false;

        private const int _DEFAULT_VALID_TIME_DIFF = 5000; //忽略5s内的时间差

        //sql
        private SQLiteConnection _sql_con;
        private SQLiteCommand _sql_cmd;
        private SQLiteTransaction _sql_trs;
        private object _sql_lock;

        private List<string> _io_queue;
        private object _io_queue_lock;

        private Thread _io_thread;

        private void _initialize_sql_tables()
        {
            const string create_filelist_sql = "create table FileList ("
                + "Path_SHA1 binary(20) primary key,"
                + "Path varchar(300),"
                + "MD5 binary(16),"
                + "Size bigint,"
                + "CRC32 int,"
                + "Slice_MD5 binary(16),"
                + "SHA1 binary(20),"
                + "CTime bigint,"
                + "MTime bigint"
                + ")";
            const string create_dbvar_sql = "create table DbVars(Key varchar(100) primary key, Value varchar(2048))";
            const string create_fileiostate_sql = "create table FileIOState ("
                + "Path_SHA1 binary(20) primary key,"
                + "Path varchar(300),"
                + "MD5_Serialized binary(1024),"
                + "SHA1_Serialized binary(1024),"
                + "CRC32_Serialized binary(1024),"
                + "MD5_Slice_Serialized binary(1024)"
                + ")";
            const string sql_insert_version = "insert into DbVars(Key, Value) values('version', '1.0.0')";
            const string sql_query_table_count = "select count(*) from sqlite_master where type = 'table'";

            lock (_sql_lock)
            {
                if (!Directory.Exists(_CACHE_PATH)) Directory.CreateDirectory(_CACHE_PATH);
                if (!File.Exists(_LOCAL_CACHE_NAME)) File.Create(_LOCAL_CACHE_NAME).Close();

                _sql_con = new SQLiteConnection("Data Source=" + _LOCAL_CACHE_NAME + "; Version=3;");
                _sql_con.Open();
                _sql_cmd = new SQLiteCommand(_sql_con);
                _sql_trs = _sql_con.BeginTransaction();

                _sql_cmd.CommandText = sql_query_table_count;
                var table_count = Convert.ToInt32(_sql_cmd.ExecuteScalar());
                if (table_count == 0)
                {
                    _sql_cmd.CommandText = create_filelist_sql;
                    _sql_cmd.ExecuteNonQuery();
                    _sql_cmd.CommandText = create_fileiostate_sql;
                    _sql_cmd.ExecuteNonQuery();
                    _sql_cmd.CommandText = create_dbvar_sql;
                    _sql_cmd.ExecuteNonQuery();
                    _sql_cmd.CommandText = sql_insert_version;
                    _sql_cmd.ExecuteNonQuery();

                    _sql_trs.Commit();
                    _sql_trs = _sql_con.BeginTransaction();
                }
            }

        }

        //io线程状态标识
        private volatile int _io_thread_flags;
        private const int _IO_THREAD_ABORT_REQUEST = 0x1;
        private const int _IO_THREAD_ABORTED = 0x2;
        private ManualResetEventSlim _io_wait;

        private string _current_io_file;
        private int _current_io_file_flag; //norm:0, pause:1, abort:2
        private void _io_thread_callback()
        {
            while (_io_thread_flags == 0)
            {
                bool queue_empty = true;
                string next_path = string.Empty;
                lock (_io_queue_lock)
                {
                    if (_io_queue.Count > 0)
                    {
                        queue_empty = false;
                        next_path = _io_queue[0];
                        _io_queue.RemoveAt(0);
                    }
                }

                if (queue_empty || _current_io_file_flag == 1)
                {
                    _io_wait.Reset();
                    _io_wait.Wait();
                    continue;
                }

                try
                {
                    var fileinfo = new FileInfo(next_path);
                    _current_io_file = next_path;
                    var origin_file_length = fileinfo.Length;

                    if (fileinfo.Exists == false)
                    {
                        Tracer.GlobalTracer.TraceWarning("File " + next_path + " no longer exists, ignored");
                        continue;
                    }

                    var fs_watcher = new FileSystemWatcher(fileinfo.DirectoryName, fileinfo.Name);
                    bool is_file_changed = false;
                    fs_watcher.Deleted += (s, e) =>
                    {
                        Tracer.GlobalTracer.TraceWarning("File deleted while reading " + next_path);
                        is_file_changed = true;
                    };
                    fs_watcher.Changed += (s, e) =>
                    {
                        Tracer.GlobalTracer.TraceWarning("File changed while reading " + next_path);
                        is_file_changed = true;
                    };
                    fs_watcher.EnableRaisingEvents = true;

                    byte[] path_sha1 = null;
#pragma warning disable CS0162
                    if (_FILE_SYSTEM_CASE_SENSITIVE)
                        path_sha1 = SHA1.ComputeHash(next_path);
                    else
                        path_sha1 = SHA1.ComputeHash(next_path.ToLower());
#pragma warning restore
                    Tracer.GlobalTracer.TraceInfo("File Digest started: " + next_path);

                    //calculation (parallel access)
                    var md5_calc = new MD5(); //speed: ~36s/GB
                    var sha1_calc = new SHA1(); //speed: ~55s/GB
                    var md5_slice_calc = new MD5(); //speed: ~45ms (256KB)
                    var crc32_calc = new Crc32(); //speed: ~11s/GB
                    long md5_pos = 0, sha1_pos = 0, crc32_pos = 0;
                    //loading pre-calculated data from sql
                    lock (_sql_lock)
                    {
                        _sql_cmd.CommandText = "select MD5_Serialized, SHA1_Serialized, MD5_Slice_Serialized, CRC32_serialized from FileIOState where Path_SHA1 = @Path_SHA1";
                        _sql_cmd.Parameters.Add("@Path_SHA1", System.Data.DbType.Binary);
                        _sql_cmd.Parameters["@Path_SHA1"].Value = path_sha1;
                        var dr = _sql_cmd.ExecuteReader();
                        var suc = dr.Read();
                        if (suc)
                        {
                            var ms_md5 = new MemoryStream((byte[])dr[0]);
                            var ms_sha1 = new MemoryStream((byte[])dr[1]);
                            var ms_md5_slice = new MemoryStream((byte[])dr[2]);
                            var ms_crc32 = new MemoryStream((byte[])dr[3]);

                            md5_calc = MD5.Deserialize(ms_md5);
                            sha1_calc = SHA1.Deserialize(ms_sha1);
                            md5_slice_calc = MD5.Deserialize(ms_md5_slice);
                            crc32_calc = Crc32.Deserialize(ms_crc32);
                            md5_pos = md5_calc.Length;
                            sha1_pos = sha1_calc.Length;
                            crc32_pos = crc32_calc.Length;
                        }
                        dr.Close();
                        _sql_cmd.Parameters.Clear();
                    }

                    int io_state = 0;
                    //5 thread parallel read
                    Parallel.For(0, 5, (i) =>
                    {
                        //debug test
                        var str = new string[] { "MD5", "SHA1", "CRC32", "Slice_MD5", "Event" };
                        var sw = new System.Diagnostics.Stopwatch();
                        Tracer.GlobalTracer.TraceInfo(str[i] + " calculation thread started");
                        sw.Start();

                        FileStream fs = null;
                        var buffer = new byte[65536];
                        long length = 0;
                        int current = 0;
                        try
                        {
                            fs = new FileStream(next_path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                            switch (i)
                            {
                                case 0:
                                    if (md5_calc.Length > origin_file_length)
                                        md5_calc.Initialize();
                                    fs.Seek(md5_calc.Length, SeekOrigin.Begin);
                                    break;
                                case 1:
                                    if (sha1_calc.Length > origin_file_length)
                                        sha1_calc.Initialize();
                                    fs.Seek(sha1_calc.Length, SeekOrigin.Begin);
                                    break;
                                case 2:
                                    if (crc32_calc.Length > origin_file_length)
                                        crc32_calc.Initialize();
                                    fs.Seek(crc32_calc.Length, SeekOrigin.Begin);
                                    break;
                                case 3:
                                    if (md5_slice_calc.Length > origin_file_length)
                                        md5_slice_calc.Initialize();
                                    fs.Seek(md5_slice_calc.Length, SeekOrigin.Begin);
                                    break;
                                case 4:
                                    break;
                                default:
                                    throw new InvalidOperationException();
                            }
                            length = fs.Position;

                            do
                            {
                                try
                                {
                                    switch (i)
                                    {
                                        case 0:
                                            current = fs.Read(buffer, 0, buffer.Length);
                                            md5_calc.TransformBlock(buffer, 0, current);
                                            md5_pos += current;
                                            break;
                                        case 1:
                                            current = fs.Read(buffer, 0, buffer.Length);
                                            sha1_calc.TransformBlock(buffer, 0, current);
                                            sha1_pos += current;
                                            break;
                                        case 2:
                                            current = fs.Read(buffer, 0, buffer.Length);
                                            crc32_calc.TransformBlock(buffer, 0, current);
                                            crc32_pos += current;
                                            break;

                                        case 3:
                                            current = fs.Read(buffer, 0, buffer.Length);
                                            if (length >= BaiduPCS.VALIDATE_SIZE)
                                                return;
                                            current = (int)Math.Min(BaiduPCS.VALIDATE_SIZE - length, current);
                                            md5_slice_calc.TransformBlock(buffer, 0, current);
                                            break;

                                        case 4:
                                            if (io_state == 0xf)
                                                return;
                                            long a = md5_pos, b = sha1_pos, c = crc32_pos;
                                            var min = Math.Min(Math.Min(a, b), c);
                                            LocalFileIOUpdate?.Invoke(next_path, min, origin_file_length);
                                            Thread.Sleep(10);
                                            break;
                                        default:
                                            throw new InvalidOperationException();
                                    }

                                    length += current;
                                }
                                catch { break; }


                            } while (is_file_changed == false && _current_io_file_flag == 0 && _io_thread_flags == 0 && current > 0 && fs.CanRead);

                        }
                        finally
                        {
                            try
                            {
                                if (fs != null)
                                {
                                    fs.Close();
                                    fs.Dispose();
                                }
                            }
                            catch { }

                            if ((i != 3 && length == origin_file_length) || (i == 3 && length == Math.Min(BaiduPCS.VALIDATE_SIZE, origin_file_length)))
                            {
                                io_state |= (1 << i);
                            }

                            //debug test
                            sw.Stop();
                            Tracer.GlobalTracer.TraceInfo(str[i] + " calculation finished (" + sw.ElapsedMilliseconds + " ms ellapsed)");
                        }

                    });

                    //handling file changed
                    if (is_file_changed)
                    {
                        try { LocalFileIOAbort?.Invoke(next_path); } catch { }
                        continue;
                    }

                    //handling io finish state
                    if (io_state == 0xf)
                    {
                        //handling file data
                        var temp_zero_bytes = new byte[0];
                        md5_calc.TransformFinalBlock(temp_zero_bytes, 0, 0);
                        sha1_calc.TransformFinalBlock(temp_zero_bytes, 0, 0);
                        md5_slice_calc.TransformFinalBlock(temp_zero_bytes, 0, 0);

                        var local_data = new LocalFileData();
                        local_data.CRC32 = crc32_calc.Hash;
                        local_data.CTime = fileinfo.CreationTime;
                        local_data.MD5 = util.Hex(md5_calc.Hash);
                        local_data.MTime = fileinfo.LastWriteTime;
                        local_data.Path = next_path;
                        local_data.Path_SHA1 = util.Hex(path_sha1);
                        local_data.SHA1 = util.Hex(sha1_calc.Hash);
                        local_data.Size = fileinfo.Length;
                        local_data.Slice_MD5 = util.Hex(md5_slice_calc.Hash);

                        //writing to sql
                        lock (_sql_lock)
                        {
                            _sql_cmd.CommandText = "select count(*) from FileList where Path_SHA1 = @Path_SHA1";
                            _sql_cmd.Parameters.Add("@Path_SHA1", System.Data.DbType.Binary);
                            _sql_cmd.Parameters["@Path_SHA1"].Value = path_sha1;

                            _sql_cmd.Parameters.Add("@CRC32", System.Data.DbType.Int32);
                            _sql_cmd.Parameters["@CRC32"].Value = (int)local_data.CRC32;
                            _sql_cmd.Parameters.Add("@CTime", System.Data.DbType.Int64);
                            _sql_cmd.Parameters["@CTime"].Value = (long)util.ToUnixTimestamp(local_data.CTime);
                            _sql_cmd.Parameters.Add("@MD5", System.Data.DbType.Binary);
                            _sql_cmd.Parameters["@MD5"].Value = md5_calc.Hash;
                            _sql_cmd.Parameters.Add("@MTime", System.Data.DbType.Int64);
                            _sql_cmd.Parameters["@MTime"].Value = (long)util.ToUnixTimestamp(local_data.MTime);
                            _sql_cmd.Parameters.Add("@Path", System.Data.DbType.String);
                            _sql_cmd.Parameters["@Path"].Value = next_path;
                            _sql_cmd.Parameters.Add("@SHA1", System.Data.DbType.Binary);
                            _sql_cmd.Parameters["@SHA1"].Value = sha1_calc.Hash;
                            _sql_cmd.Parameters.Add("@Slice_MD5", System.Data.DbType.Binary);
                            _sql_cmd.Parameters["@Slice_MD5"].Value = md5_slice_calc.Hash;
                            _sql_cmd.Parameters.Add("@Size", System.Data.DbType.Int64);
                            _sql_cmd.Parameters["@Size"].Value = local_data.Size;
                            var count = Convert.ToInt32(_sql_cmd.ExecuteScalar());
                            if (count == 0)
                            {
                                _sql_cmd.CommandText = "insert into FileList(Path, Path_SHA1, CRC32, CTime, MD5, MTime, SHA1, Slice_MD5, Size) values (@Path, @Path_SHA1, @CRC32, @CTime, @MD5, @MTime, @SHA1, @Slice_MD5, @Size)";
                            }
                            else
                            {
                                _sql_cmd.CommandText = "update FileList set Path = @Path, CRC32 = @CRC32, CTime = @CTime, MD5 = @MD5, MTime = @MTime, SHA1 = @SHA1, Slice_MD5 = @Slice_MD5, Size = @Size where Path_SHA1 = @Path_SHA1";
                            }
                            _sql_cmd.ExecuteNonQuery();

                            _sql_cmd.CommandText = "delete from FileIOState where Path_SHA1 = @Path_SHA1";
                            _sql_cmd.ExecuteNonQuery();
                            _sql_cmd.Parameters.Clear();
                        }

                        try { LocalFileIOFinish?.Invoke(local_data); }
                        catch { }
                    }

                    //handling thread flags
                    if (_io_thread_flags == _IO_THREAD_ABORT_REQUEST || _current_io_file_flag == 1)
                    {

                        //handling non-completed io calculation
                        if (io_state != 0xf)
                        {
                            lock (_sql_lock)
                            {
                                _sql_cmd.CommandText = "select count(*) from FileIOState where Path_SHA1 = @Path_SHA1";
                                _sql_cmd.Parameters.Add("@Path_SHA1", System.Data.DbType.Binary);
                                _sql_cmd.Parameters["@Path_SHA1"].Value = path_sha1;

                                _sql_cmd.Parameters.Add("@Path", System.Data.DbType.String);
                                _sql_cmd.Parameters["@Path"].Value = next_path;
                                var ms_md5 = new MemoryStream();
                                md5_calc.Serialize(ms_md5);
                                ms_md5.Position = 0;
                                var ms_sha1 = new MemoryStream();
                                sha1_calc.Serialize(ms_sha1);
                                ms_sha1.Position = 0;
                                var ms_crc32 = new MemoryStream();
                                crc32_calc.Serialize(ms_crc32);
                                ms_crc32.Position = 0;
                                var ms_md5_slice = new MemoryStream();
                                md5_slice_calc.Serialize(ms_md5_slice);
                                ms_md5_slice.Position = 0;
                                _sql_cmd.Parameters.Add("@MD5", System.Data.DbType.Binary);
                                _sql_cmd.Parameters["@MD5"].Value = util.ReadBytes(ms_md5, (int)ms_md5.Length);
                                _sql_cmd.Parameters.Add("@SHA1", System.Data.DbType.Binary);
                                _sql_cmd.Parameters["@SHA1"].Value = util.ReadBytes(ms_sha1, (int)ms_sha1.Length);
                                _sql_cmd.Parameters.Add("@CRC32", System.Data.DbType.Binary);
                                _sql_cmd.Parameters["@CRC32"].Value = util.ReadBytes(ms_crc32, (int)ms_crc32.Length);
                                _sql_cmd.Parameters.Add("@MD5_Slice", System.Data.DbType.Binary);
                                _sql_cmd.Parameters["@MD5_Slice"].Value = util.ReadBytes(ms_md5_slice, (int)ms_md5_slice.Length);

                                var count = Convert.ToInt32(_sql_cmd.ExecuteScalar());
                                if (count == 0)
                                {
                                    _sql_cmd.CommandText = "insert into FileIOState(Path, Path_SHA1, MD5_Serialized, SHA1_Serialized, CRC32_Serialized, MD5_Slice_Serialized) values(@Path, @Path_SHA1, @MD5, @SHA1, @CRC32, @MD5_Slice)";
                                }
                                else
                                {
                                    _sql_cmd.CommandText = "update FileIOState set Path = @Path, MD5_Serialized = @MD5, SHA1_Serialized = @SHA1, CRC32_Serialized = @CRC32, MD5_Slice_Serialized = @MD5_Slice where Path_SHA1 = @Path_SHA1";
                                }
                                _sql_cmd.ExecuteNonQuery();
                                _sql_cmd.Parameters.Clear();
                            }

                            try { LocalFileIOAbort?.Invoke(next_path); }
                            catch { }
                        }

                        //commiting sql data
                        lock (_sql_lock)
                        {
                            _sql_trs.Commit();
                            _sql_trs = _sql_con.BeginTransaction();
                        }

                        _io_thread_flags = _IO_THREAD_ABORTED;
                        return;
                    }
                }
                catch (Exception ex)
                {
                    Tracer.GlobalTracer.TraceError(ex);
                }
            }
        }

        private bool _in_duration(DateTime d1, DateTime d2)
        {
            return (Math.Abs((d1 - d2).TotalMilliseconds) < _DEFAULT_VALID_TIME_DIFF);
        }
        public void Dispose()
        {
            if (_io_thread != null)
            {
                _io_thread_flags = _IO_THREAD_ABORT_REQUEST;
                _io_wait.Set();
                _io_thread.Join();
                _io_thread = null;
            }
            if (_sql_trs != null)
            {
                _sql_trs.Commit();
                _sql_trs.Dispose();
                _sql_trs = null;
            }
            if (_sql_cmd != null)
            {
                _sql_cmd.Dispose();
                _sql_cmd = null;
            }
            if (_sql_con != null)
            {
                _sql_con.Close();
                _sql_con.Dispose();
                _sql_con = null;
            }
        }

        public LocalFileCacher()
        {
            _sql_lock = new object();
            _io_queue_lock = new object();
            _io_queue = new List<string>();
            _io_wait = new ManualResetEventSlim();

            _initialize_sql_tables();
            _io_thread = new Thread(_io_thread_callback);
            _io_thread.IsBackground = true;
            _io_thread.Name = "后台IO线程";
            _io_thread.Start();
        }
        ~LocalFileCacher()
        {
            Dispose();
        }

        public event LocalFileIOCallback LocalFileIOUpdate;
        public event LocalFileIOFinishCallback LocalFileIOFinish;
        public event LocalFileIOAbortedCallback LocalFileIOAbort;
        /// <summary>
        /// 将文件的特征I/O任务添加到执行队列中
        /// </summary>
        /// <param name="path">文件路径</param>
        public void FileIORequest(string path)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentNullException("path");
            if (!File.Exists(path)) throw new ArgumentException("File not exists");

            ThreadPool.QueueUserWorkItem(delegate
            {
                var file_info = new FileInfo(path);
                byte[] path_sha1 = null;

#pragma warning disable CS0162
                if (_FILE_SYSTEM_CASE_SENSITIVE)
                    path_sha1 = SHA1.ComputeHash(path);
                else
                    path_sha1 = SHA1.ComputeHash(path.ToLower());
#pragma warning restore

                var result = new LocalFileData();
                bool sql_hit = false;
                lock (_sql_lock)
                {
                    var query_sql = "select Path, Path_SHA1, MD5, Size, CRC32, Slice_MD5, SHA1, CTime, MTime from FileList where Path_SHA1 = @Path_SHA1";
                    _sql_cmd.CommandText = query_sql;
                    _sql_cmd.Parameters.Add("@Path_SHA1", System.Data.DbType.Binary);
                    _sql_cmd.Parameters["@Path_SHA1"].Value = path_sha1;
                    var dr = _sql_cmd.ExecuteReader();
                    sql_hit = dr.Read();
                    if (sql_hit)
                    {
                        result.Path = (string)dr[0];
                        result.Path_SHA1 = util.Hex((byte[])dr[1]);
                        result.MD5 = util.Hex((byte[])dr[2]);
                        result.Size = (long)dr[3];
                        result.CRC32 = (uint)(int)dr[4];
                        result.Slice_MD5 = util.Hex((byte[])dr[5]);
                        result.SHA1 = util.Hex((byte[])dr[6]);
                        result.CTime = util.FromUnixTimestamp((long)dr[7]);
                        result.MTime = util.FromUnixTimestamp((long)dr[8]);
                    }
                    dr.Close();
                }

                bool sql_cache_dirty = false;
                if (file_info.Length != result.Size) sql_cache_dirty = true;
                if (!_in_duration(file_info.LastWriteTime, result.MTime)) sql_cache_dirty = true;
                if (!_in_duration(file_info.CreationTime, result.CTime)) sql_cache_dirty = true;

                if (sql_hit == true && sql_cache_dirty == false)
                {
                    //cache updated
                    LocalFileIOUpdate?.Invoke(path, 0, result.Size);
                    LocalFileIOUpdate?.Invoke(path, result.Size, result.Size);
                    LocalFileIOFinish?.Invoke(result);
                    return;
                }

                lock (_io_queue_lock)
                {
                    _io_queue.Add(path);
                    _io_wait.Set();
                }
            });
        }
        //TODO: debug check: sync variable boundary
        public void FileIOAbort(string path)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentNullException("path");

#pragma warning disable CS0162
            if (!_FILE_SYSTEM_CASE_SENSITIVE)
                path = path.ToLower();
#pragma warning restore

            if (_current_io_file == path)
                _current_io_file_flag = 2;
            else
            {
                lock (_io_queue_lock)
                {
                    if (_io_queue.Contains(path))
                        _io_queue.Remove(path);
                    ThreadPool.QueueUserWorkItem(delegate
                    {
                        try { LocalFileIOAbort?.Invoke(path); }
                        catch { }
                    });
                }
            }
        }
    }
}
