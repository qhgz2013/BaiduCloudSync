using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

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
    public class CLocalFileCacher : IDisposable //will be replaced to class LocalFileCacher after removing the current cacher
    {
        private const string _CACHE_PATH = "data";
        private const string _LOCAL_CACHE_NAME = _CACHE_PATH + "/local-track.db";

        //sql
        private SQLiteConnection _sql_con;
        private SQLiteCommand _sql_cmd;
        private SQLiteTransaction _sql_trs;
        private object _sql_lock;

        private Queue<string> _io_queue;
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
                + "SHA1 binary(20)"
                + "CTime bigint,"
                + "MTime bigint"
                + ")";
            const string create_dbvar_sql = "create table DbVars(Key varchar(100) primary key, Value varchar(2048))";
            const string create_fileiostate_sql = "create table FileIOState ("
                + "Path_SHA1 binary(20) primay key,"
                + "Path varchar(300),"
                + "Offset bigint not null default 0,"
                + "MD5_Serialized varchar(1024),"
                + "SHA1_Serialized varchar(1024),"
                + "CRC32_Serialized varchar(1024),"
                + "MD5_Slice_Serialized varchar(1024)"
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
                        next_path = _io_queue.Dequeue();
                    }
                }

                if (queue_empty)
                {
                    Thread.Sleep(3000);
                    continue;
                }

                FileStream io_stream = null;
                try
                {
                    var fileinfo = new FileInfo(next_path);
                    if (fileinfo.Exists == false)
                    {
                        Tracer.GlobalTracer.TraceWarning("File " + next_path + " no longer exists, ignored");
                        continue;
                    }

                    var fs_watcher = new FileSystemWatcher(fileinfo.DirectoryName, fileinfo.Name);
                    bool is_file_changed = false;
                    fs_watcher.Deleted += (s, e) => { is_file_changed = true; };
                    fs_watcher.Changed += (s, e) => { is_file_changed = true; };
                    fs_watcher.EnableRaisingEvents = true;

                    var path_sha1 = SHA1.ComputeHash(next_path);
                    io_stream = fileinfo.Open(FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);

                    var buffer = new byte[4096];
                    long length = fileinfo.Length;
                    int current = 0;

                    //calculation
                    var md5_calc = new MD5();
                    var sha1_calc = new SHA1();
                    var md5_slice_calc = new MD5();
                    var crc32_calc = new Crc32();

                    //loading pre-calculated data from sql
                    lock (_sql_lock)
                    {
                        _sql_cmd.CommandText = "select MD5_Serialized, SHA1_Serialized, MD5_Slice_Serialized, CRC32_serialized, Offset from FileIOState where Path_SHA1 = @Path_SHA1";
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

                            var offset = (long)dr[4];
                            md5_calc = MD5.Deserialize(ms_md5);
                            sha1_calc = SHA1.Deserialize(ms_sha1);
                            md5_slice_calc = MD5.Deserialize(ms_md5_slice);
                            crc32_calc = Crc32.Deserialize(ms_crc32);
                        }
                        dr.Close();
                        _sql_cmd.Parameters.Clear();
                    }

                    do
                    {
                        current = io_stream.Read(buffer, 0, 4096);
                        //multi-thread calculation, no hardware acc.
                        Parallel.For(0, 4, (i) => //Lambda for parallel for
                        {
                            if (i == 0)
                            {
                                md5_calc.TransformBlock(buffer, 0, current);
                            }
                            else if (i == 1)
                            {
                                sha1_calc.TransformBlock(buffer, 0, current);
                            }
                            else if (i == 2)
                            {
                                if (length >= BaiduPCS.VALIDATE_SIZE) return;
                                int size = current;
                                if (length + current >= BaiduPCS.VALIDATE_SIZE)
                                    size = (int)(BaiduPCS.VALIDATE_SIZE - length);
                                md5_slice_calc.TransformBlock(buffer, 0, size);
                            }
                            else
                            {
                                crc32_calc.Append(buffer, 0, current);
                            }
                        });

                        length += current;
                        LocalFileIOUpdate?.Invoke(next_path, length, fileinfo.Length);
                    } while (is_file_changed == false && _io_thread_flags == 0 && current > 0 && io_stream.CanRead);
                    
                    //handling file changed
                    if (is_file_changed)
                    {
                        continue;
                    }

                    //handling file data
                    var temp_zero_bytes = new byte[0];
                    md5_calc.TransformFinalBlock(temp_zero_bytes, 0, 0);
                    sha1_calc.TransformFinalBlock(temp_zero_bytes, 0, 0);
                    md5_slice_calc.TransformFinalBlock(temp_zero_bytes, 0, 0);

                    //handling io finish state
                    if (length == fileinfo.Length)
                    {
                        var local_data = new LocalFileData();
                        local_data.CRC32 = crc32_calc.GetCrc32();
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

                            _sql_cmd.Parameters.Add("@CRC32", System.Data.DbType.Binary);
                            _sql_cmd.Parameters["@CRC32"].Value = util.Hex(local_data.CRC32.ToString("X2"));
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
                        }
                    }

                    //handling thread flags
                    if (_io_thread_flags == _IO_THREAD_ABORT_REQUEST)
                    {

                        //handling non-completed io calculation
                        if (length != fileinfo.Length)
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
                                var ms_sha1 = new MemoryStream();
                                sha1_calc.Serialize(ms_sha1);
                                var ms_crc32 = new MemoryStream();
                                crc32_calc.Serialize(ms_crc32);
                                var ms_md5_slice = new MemoryStream();
                                md5_slice_calc.Serialize(ms_md5_slice);
                                _sql_cmd.Parameters.Add("@MD5", System.Data.DbType.Binary);
                                _sql_cmd.Parameters["@MD5"].Value = util.ReadBytes(ms_md5, (int)ms_md5.Length);
                                _sql_cmd.Parameters.Add("@SHA1", System.Data.DbType.Binary);
                                _sql_cmd.Parameters["@SHA1"].Value = util.ReadBytes(ms_sha1, (int)ms_sha1.Length);
                                _sql_cmd.Parameters.Add("@CRC32", System.Data.DbType.Binary);
                                _sql_cmd.Parameters["@CRC32"].Value = util.ReadBytes(ms_crc32, (int)ms_crc32.Length);
                                _sql_cmd.Parameters.Add("@MD5_Slice", System.Data.DbType.Binary);
                                _sql_cmd.Parameters["@MD5_Slice"].Value = util.ReadBytes(ms_md5_slice, (int)ms_md5_slice.Length);
                                _sql_cmd.Parameters.Add("@Offset", System.Data.DbType.Int64);
                                _sql_cmd.Parameters["@Offset"].Value = length;

                                var count = Convert.ToInt32(_sql_cmd.ExecuteScalar());
                                if (count == 0)
                                {
                                    _sql_cmd.CommandText = "insert into FileIOState(Path, Path_SHA1, Offset, MD5_Serialized, SHA1_Serialized, CRC32_Serialized, MD5_Slice_Serialized) values(@Path, @Path_SHA1, @Offset, @MD5, @SHA1, @CRC32, @MD5_Slice)";
                                }
                                else
                                {
                                    _sql_cmd.CommandText = "update FileIOState set Path = @Path, Offset = @Offset, MD5_Serialized = @MD5, SHA1_Serialized = @SHA1, CRC32_Serialized = @CRC32, MD5_Slice_Serialized = @MD5_Slice where Path_SHA1 = @Path_SHA1";
                                }
                                _sql_cmd.ExecuteNonQuery();
                                _sql_cmd.Parameters.Clear();
                            }
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
                finally
                {
                    try { if (io_stream != null) { io_stream.Close(); io_stream.Dispose(); } }
                    catch { }
                }
            }
        }

        private const int _DEFAULT_VALID_TIME_DIFF = 5000; //忽略5s内的时间差
        private bool _in_duration(DateTime d1, DateTime d2)
        {
            return (Math.Abs((d1 - d2).TotalMilliseconds) < _DEFAULT_VALID_TIME_DIFF);
        }
        public void Dispose()
        {
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

        public CLocalFileCacher()
        {
            _sql_lock = new object();
            _io_queue_lock = new object();
            _io_queue = new Queue<string>();

            _initialize_sql_tables();
            _io_thread = new Thread(_io_thread_callback);
            _io_thread.IsBackground = true;
            _io_thread.Name = "后台IO线程";
            _io_thread.Start();
        }

        public event LocalFileIOCallback LocalFileIOUpdate;
        public event LocalFileIOFinishCallback LocalFileIOFinish;

        public void FileIORequest(string path)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentNullException("path");
            if (!File.Exists(path)) throw new ArgumentException("File not exists");

            ThreadPool.QueueUserWorkItem(delegate
            {
                var file_info = new FileInfo(path);
                var path_sha1 = SHA1.ComputeHash(path.ToLower());
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
                    _io_queue.Enqueue(path);
                }
            });
        }
    }
}
