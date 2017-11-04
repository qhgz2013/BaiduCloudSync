using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Text;

namespace BaiduCloudSync
{
    public class LocalFileCacher : IDisposable
    {
        #region vars
        //sql相关
        private SQLiteConnection _sql_con;
        private SQLiteCommand _sql_cmd;
        private SQLiteTransaction _sql_trs;
        //缓存文件的大小
        private long _current_cache_size;
        private string _current_db_version;
        #endregion

        #region initializer
        public LocalFileCacher()
        {
            Tracer.GlobalTracer.TraceInfo("Local file cacher: Connecting to local database");
            if (!File.Exists(_base_directory + _const_cache_name))
            {
                Tracer.GlobalTracer.TraceInfo("Local database not detected, creating new");
                SQLiteConnection.CreateFile(_base_directory + _const_cache_name);
            }

            _sql_con = new SQLiteConnection("Data Source = " + _base_directory + _const_cache_name + "; Version = 3;");
            _sql_con.Open();
            _sql_cmd = new SQLiteCommand(_sql_con);
            _sql_trs = _sql_con.BeginTransaction();

            _init_tables();
        }
        //初始化数据表
        private void _init_tables()
        {
            //checking tables
            var check_table_sql = "select count(*) from sqlite_master where type = 'table'";

            int count = Convert.ToInt32(_exec_scalar(check_table_sql));
            if (count == 0)
            {
                _current_cache_size = _DEFAULT_MAX_CACHE_SIZE;
                _current_db_version = _DEFAULT_DB_VERSION;

                var create_dbvars = "create table DbVars(Key varchar(512) primary key, Value varchar(2048))";
                _exec_nonquery(create_dbvars);
                var insert_cache_size = "insert into DbVars(Key, Value) values ('CacheSize', '" + _current_cache_size + "')";
                _exec_nonquery(insert_cache_size);
                var insert_version = "insert into DbVars(Key, Value) values('Version', '" + _current_db_version + "')";
                _exec_nonquery(insert_version);


                var create_filedata = "create table CacheList(Path varchar(4096) primary key, MD5 binary(16) not null, SHA1 binary(20) not null, CRC32 binary(4) not null, ContentSize bigint not null, CreationTime bigint not null, ModifiedTime bigint not null, DatabaseWriteTime bigint not null)";
                _exec_nonquery(create_filedata);

                //committing data
                _sql_trs.Commit();
                _sql_trs = _sql_con.BeginTransaction();
            }
            else
            {
                var dr = _exec_query("select Value from DbVars where Key = 'CacheSize'");
                if (dr.Read())
                {
                    _current_cache_size = long.Parse(dr.GetString(0));
                    dr.Close();
                }
                else
                {
                    dr.Close();
                    _current_cache_size = _DEFAULT_MAX_CACHE_SIZE;
                    _exec_nonquery("insert into DbVars(Key, Value) values ('CacheSize', '" + _current_cache_size + "')");
                }

                dr = _exec_query("select Value from DbVars where Key = 'Version'");
                if (dr.Read())
                {
                    _current_db_version = dr.GetString(0);
                    dr.Close();
                }
                else
                {
                    dr.Close();
                    _current_db_version = _DEFAULT_DB_VERSION;
                    _exec_nonquery("insert into DbVars(Key, Value) values ('Version', '" + _current_db_version + "')");
                }
            }

            _patch_databse(_current_db_version);
        }
        #endregion

        #region sql util function
        private void _exec_nonquery(string cmd)
        {
            _sql_cmd.CommandText = cmd;
            _sql_cmd.ExecuteNonQuery();
        }
        private object _exec_scalar(string cmd)
        {
            _sql_cmd.CommandText = cmd;
            return _sql_cmd.ExecuteScalar();
        }
        private SQLiteDataReader _exec_query(string cmd)
        {
            _sql_cmd.CommandText = cmd;
            return _sql_cmd.ExecuteReader();
        }
        #endregion

        #region const or static vars
        //程序当前的目录
        private static string _base_directory;
        static LocalFileCacher()
        {
            _base_directory = Directory.GetCurrentDirectory();

            _base_directory += "/.cache/";
            if (!Directory.Exists(_base_directory)) Directory.CreateDirectory(_base_directory);
        }
        private static string _const_cache_name = "cache.db";
        //默认缓存文件的大小
        private const long _DEFAULT_MAX_CACHE_SIZE = 50 << 20; //50MB
        //数据版本
        private const string _DEFAULT_DB_VERSION = "1.0.1";
        //默认忽略的文件时间差异
        private static TimeSpan _DEFAULT_TIME_DIFF = TimeSpan.FromSeconds(5);
        private object _external_lock = new object();
        #endregion

        #region public function for cache size
        //获取当前缓存文件的大小
        public long GetCurrentCacheFileSize()
        {
            var fileinfo = new FileInfo(_base_directory + _const_cache_name);
            if (fileinfo.Exists) return fileinfo.Length;
            else return 0;
        }
        /// <summary>
        /// 设置最大的缓存文件大小（不小于1mb），用于自动清理缓存文件，一般来说，1MB大约能保存2500条文件缓存记录
        /// </summary>
        /// <param name="new_size"></param>
        public void SetMaxCacheFileSize(long new_size)
        {
            if (new_size < (1 << 20)) return; //reject the number less than 1MB
            lock (_external_lock)
            {
                _current_cache_size = new_size;
                var set_cache = "update DbVars set Value = '" + _current_cache_size + "' where Key = 'CacheSize'";
                _exec_nonquery(set_cache);
            }
        }
        /// <summary>
        /// 获取当前缓存文件大小
        /// </summary>
        /// <returns></returns>
        public long GetMaxCacheFileSize()
        {
            return _current_cache_size;
        }
        #endregion

        #region private functions
        //验证文件大小并自动清除数据
        private void _validating_cache_size()
        {
            var current_size = (new FileInfo(_base_directory + _const_cache_name)).Length;

            if (current_size >= _current_cache_size)
            {
                var data_count = Convert.ToInt32(_exec_scalar("select count(*) from CacheList"));

                var overloaded_percent = 1.0 * current_size / _current_cache_size;
                var overloaded_data_count = (int)((1 - 0.7 / overloaded_percent) * data_count);

                Tracer.GlobalTracer.TraceInfo("Local data cacher: Database size exceeded, removing " + overloaded_data_count + " data entries");

                var delete_sql = "delete from CacheList where Path in (select Path from CacheList order by DatabaseWriteTime asc limit " + overloaded_data_count + ")";
                _exec_nonquery(delete_sql);

                _sql_trs.Commit();
                _sql_cmd.CommandText = "vacuum";
                _sql_cmd.ExecuteNonQuery();
                _sql_trs = _sql_con.BeginTransaction();
            }
        }
        //判定两个时间的时间差是否在某个区间
        private bool _in_duration(DateTime t1, DateTime t2, TimeSpan ts)
        {
            if (t1 > t2)
                return (t1 - t2) <= ts;
            else
                return (t2 - t1) <= ts;
        }
        private void _calc_data(Stream sin, out string MD5, out string CRC32, out string SHA1)
        {
            int buffer_size = 65536;
            var buffer = new byte[buffer_size];
            var out_buffer = new byte[buffer_size];
            var md5_calc = new System.Security.Cryptography.MD5CryptoServiceProvider();
            var crc32_calc = new Crc32();
            var sha1_calc = new System.Security.Cryptography.SHA1CryptoServiceProvider();

            int rbyte = 0;
            long total_byte = 0;
            do
            {
                rbyte = sin.Read(buffer, 0, buffer_size);
                md5_calc.TransformBlock(buffer, 0, rbyte, out_buffer, 0);
                sha1_calc.TransformBlock(buffer, 0, rbyte, out_buffer, 0);
                crc32_calc.TransformBlock(buffer, 0, rbyte);

                total_byte += rbyte;
            } while (rbyte > 0);
            md5_calc.TransformFinalBlock(buffer, 0, 0);
            sha1_calc.TransformFinalBlock(buffer, 0, 0);

            MD5 = util.Hex(md5_calc.Hash);
            SHA1 = util.Hex(sha1_calc.Hash);
            var crc32_int = crc32_calc.Hash;
            var crc_bytes = new byte[] { (byte)((crc32_int >> 24) & 0xff), (byte)((crc32_int >> 16) & 0xff), (byte)((crc32_int >> 8) & 0xff), (byte)(crc32_int & 0xff) };
            CRC32 = util.Hex(crc_bytes);
        }
        //向数据库中添加记录
        private void _insert_data(TrackedData data)
        {
            _sql_cmd.CommandText = "insert into CacheList(Path, MD5, SHA1, CRC32, ContentSize, CreationTime, ModifiedTime, DatabaseWriteTime) values(@Path, @MD5, @SHA1, @CRC32, @ContentSize, @CreationTime, @ModifiedTime, @DatabaseWriteTime)";
            _sql_cmd.Parameters.Add("@Path", System.Data.DbType.String);
            _sql_cmd.Parameters["@Path"].Value = data.Path.Replace(@"\", "/").ToLower();
            _sql_cmd.Parameters.Add("@MD5", System.Data.DbType.Binary);
            _sql_cmd.Parameters["@MD5"].Value = util.Hex(data.MD5);
            _sql_cmd.Parameters.Add("@SHA1", System.Data.DbType.Binary);
            _sql_cmd.Parameters["@SHA1"].Value = util.Hex(data.SHA1);
            _sql_cmd.Parameters.Add("@CRC32", System.Data.DbType.Binary);
            _sql_cmd.Parameters["@CRC32"].Value = util.Hex(data.CRC32);
            _sql_cmd.Parameters.Add("@ContentSize", System.Data.DbType.Int64);
            _sql_cmd.Parameters["@ContentSize"].Value = data.ContentSize;
            _sql_cmd.Parameters.Add("@CreationTime", System.Data.DbType.Int64);
            _sql_cmd.Parameters["@CreationTime"].Value = data.CreationTime.Ticks;
            _sql_cmd.Parameters.Add("@ModifiedTime", System.Data.DbType.Int64);
            _sql_cmd.Parameters["@ModifiedTime"].Value = data.ModifiedTime.Ticks;
            _sql_cmd.Parameters.Add("@DatabaseWriteTime", System.Data.DbType.Int64);
            _sql_cmd.Parameters["@DatabaseWriteTime"].Value = DateTime.Now.Ticks;
            _sql_cmd.ExecuteNonQuery();
            _sql_cmd.Parameters.Clear();
        }
        //更新数据库的记录
        private void _update_data(TrackedData data)
        {
            _sql_cmd.CommandText = "update CacheList set MD5 = @MD5, SHA1 = @SHA1, CRC32 = @CRC32, ContentSize = @ContentSize, CreationTime = @CreationTime, ModifiedTime = @ModifiedTime, DatabaseWriteTime = @DatabaseWriteTime where Path = @Path";
            _sql_cmd.Parameters.Add("@MD5", System.Data.DbType.Binary);
            _sql_cmd.Parameters["@MD5"].Value = util.Hex(data.MD5);
            _sql_cmd.Parameters.Add("@SHA1", System.Data.DbType.Binary);
            _sql_cmd.Parameters["@SHA1"].Value = util.Hex(data.SHA1);
            _sql_cmd.Parameters.Add("@CRC32", System.Data.DbType.Binary);
            _sql_cmd.Parameters["@CRC32"].Value = util.Hex(data.CRC32);
            _sql_cmd.Parameters.Add("@ContentSize", System.Data.DbType.Int64);
            _sql_cmd.Parameters["@ContentSize"].Value = data.ContentSize;
            _sql_cmd.Parameters.Add("@CreationTime", System.Data.DbType.Int64);
            _sql_cmd.Parameters["@CreationTime"].Value = data.CreationTime.Ticks;
            _sql_cmd.Parameters.Add("@ModifiedTime", System.Data.DbType.Int64);
            _sql_cmd.Parameters["@ModifiedTime"].Value = data.ModifiedTime.Ticks;
            _sql_cmd.Parameters.Add("@DatabaseWriteTime", System.Data.DbType.Int64);
            _sql_cmd.Parameters["@DatabaseWriteTime"].Value = DateTime.Now.Ticks;
            _sql_cmd.Parameters.Add("@Path", System.Data.DbType.String);
            _sql_cmd.Parameters["@Path"].Value = data.Path.Replace(@"\", "/").ToLower();
            _sql_cmd.ExecuteNonQuery();
            _sql_cmd.Parameters.Clear();
        }
        //从缓存中获取指定路径的文件数据（路径不带最后/，分隔符为/）
        private TrackedData[] _get_data_from_cache(string path)
        {
            path = path.Replace(@"\", "/");
            var dr = _exec_query("select Path, MD5, SHA1, CRC32, ContentSize, CreationTime, ModifiedTime from CacheList where Path like '" + path.ToLower() + "/_%' and Path not like '" + path + "/_%/%'");
            //查询包含其子文件夹，由代码排除
            var list = new List<TrackedData>();
            while (dr.Read())
            {
                var entry_path = dr.GetString(0);

                var entry_data = new TrackedData();
                entry_data.Path = entry_path;
                var entry_md5 = (byte[])dr[1];
                entry_data.MD5 = util.Hex(entry_md5);
                var entry_sha1 = (byte[])dr[2];
                entry_data.SHA1 = util.Hex(entry_sha1);
                var entry_crc32 = (byte[])dr[3];
                entry_data.CRC32 = util.Hex(entry_crc32);
                entry_data.ContentSize = (ulong)dr.GetInt64(4);
                entry_data.CreationTime = new DateTime(dr.GetInt64(5));
                entry_data.ModifiedTime = new DateTime(dr.GetInt64(6));

                list.Add(entry_data);
            }
            dr.Close();
            return list.ToArray();
        }
        private TrackedData[] _get_data_from_path(string path, bool recursive)
        {
            var path_info = new DirectoryInfo(path);
            var list = new List<TrackedData>();
            if (recursive)
            {
                foreach (var item in path_info.GetDirectories())
                {
                    list.AddRange(_get_data_from_path(item.FullName, recursive));
                }
            }
            var cached_data = _get_data_from_cache(path);

            foreach (var item in path_info.GetFiles())
            {
                //getting datas
                var entry = new TrackedData();
                entry.Path = item.FullName.Replace(@"\", "/");
                entry.ContentSize = (ulong)item.Length;
                entry.CreationTime = item.CreationTimeUtc;
                entry.ModifiedTime = item.LastWriteTimeUtc;
                entry.IsDir = false;
                //update state analysis (todo: improve query speed)
                var cached_entry = cached_data.Where(tvar => tvar.Path == entry.Path.ToLower()).FirstOrDefault();
                if (!string.IsNullOrEmpty(cached_entry.Path))
                {

                    if (cached_entry.ContentSize != entry.ContentSize || !_in_duration(cached_entry.CreationTime, entry.CreationTime, _DEFAULT_TIME_DIFF) || !_in_duration(cached_entry.ModifiedTime, entry.ModifiedTime, _DEFAULT_TIME_DIFF))
                    {
                        //更新MD5
                        var ifs = item.OpenRead();
                        _calc_data(ifs, out entry.MD5, out entry.CRC32, out entry.SHA1);
                        ifs.Close();
                        _update_data(entry);
                    }
                    else
                    {
                        entry.MD5 = cached_entry.MD5;
                        entry.SHA1 = cached_entry.SHA1;
                        entry.CRC32 = cached_entry.CRC32;
                    }
                }
                else
                {
                    //数据不存在，添加到列表中
                    var ifs = item.OpenRead();
                    _calc_data(ifs, out entry.MD5, out entry.CRC32, out entry.SHA1);
                    ifs.Close();
                    _insert_data(entry);
                }
                list.Add(entry);
            }
            //添加文件夹到里面去
            foreach (var item in path_info.GetDirectories())
            {
                list.Add(new TrackedData(item.FullName.Replace(@"\", "/"), item.CreationTimeUtc, item.LastWriteTimeUtc));
            }
            return list.ToArray();
        }
        private TrackedData _get_data_from_file(string path)
        {
            var path_info = new FileInfo(path);
            var cached_data = _get_data_from_cache(path_info.Directory.FullName);

            //getting datas
            var entry = new TrackedData();
            entry.Path = path_info.FullName.Replace(@"\", "/");
            entry.ContentSize = (ulong)path_info.Length;
            entry.CreationTime = path_info.CreationTimeUtc;
            entry.ModifiedTime = path_info.LastWriteTimeUtc;
            entry.IsDir = false;
            //update state analysis (todo: improve query speed)
            var cached_entry = cached_data.Where(tvar => tvar.Path == entry.Path.ToLower()).FirstOrDefault();
            if (!string.IsNullOrEmpty(cached_entry.Path))
            {

                if (cached_entry.ContentSize != entry.ContentSize || !_in_duration(cached_entry.CreationTime, entry.CreationTime, _DEFAULT_TIME_DIFF) || !_in_duration(cached_entry.ModifiedTime, entry.ModifiedTime, _DEFAULT_TIME_DIFF))
                {
                    //更新MD5
                    var ifs = path_info.OpenRead();
                    _calc_data(ifs, out entry.MD5, out entry.CRC32, out entry.SHA1);
                    ifs.Close();
                    _update_data(entry);
                }
                else
                {
                    entry.MD5 = cached_entry.MD5;
                    entry.SHA1 = cached_entry.SHA1;
                    entry.CRC32 = cached_entry.CRC32;
                }
            }
            else
            {
                //数据不存在，添加到列表中
                var ifs = path_info.OpenRead();
                _calc_data(ifs, out entry.MD5, out entry.CRC32, out entry.SHA1);
                ifs.Close();
                _insert_data(entry);
            }
            return entry;
        }
        #endregion

        #region public function for data io
        public void Dispose()
        {
            lock (_external_lock)
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
                    _sql_con.Dispose();
                    _sql_con = null;
                }
            }
            _external_lock = null;
        }
        /// <summary>
        /// 获取指定路径下的文件信息(不含最后的/或\)
        /// </summary>
        /// <param name="path">文件路径</param>
        /// <returns></returns>
        public TrackedData[] GetDataFromPath(string path, bool recursive = false)
        {
            Tracer.GlobalTracer.TraceInfo("LocalFileCacher.GetDataFromPath called: string path=" + path + ", bool recursive=" + recursive);
            if (string.IsNullOrEmpty(path)) return new TrackedData[] { };
            var dir_info = new DirectoryInfo(path);
            if (!dir_info.Exists) return new TrackedData[] { };

            lock (_external_lock)
            {
                var ret = _get_data_from_path(path, recursive);
                if (_sql_trs != null) _sql_trs.Commit();
                _sql_trs = _sql_con.BeginTransaction();
                _validating_cache_size();
                return ret;
            }
        }
        public TrackedData GetDataFromFile(string path)
        {
            Tracer.GlobalTracer.TraceInfo("LocalFileCacher.GetDataFromPath called: string path=" + path);
            if (string.IsNullOrEmpty(path)) return new TrackedData();
            var dir_info = new FileInfo(path);
            if (!dir_info.Exists) return new TrackedData();

            lock (_external_lock)
            {
                var ret = _get_data_from_file(path);
                if (_sql_trs != null) _sql_trs.Commit();
                _sql_trs = _sql_con.BeginTransaction();
                _validating_cache_size();
                return ret;
            }
        }
        /// <summary>
        /// 清除所有缓存
        /// </summary>
        public void ClearAllCache()
        {
            Tracer.GlobalTracer.TraceInfo("LocalFileCacher.ClearAllCache called: void");
            lock (_external_lock)
            {
                _exec_nonquery("delete from CacheList");
                _sql_trs.Commit();
                _sql_cmd.CommandText = "vacuum";
                _sql_cmd.ExecuteNonQuery();
                _sql_trs = _sql_con.BeginTransaction();
            }
        }
        #endregion


        #region Data Patcher
        private void _patch_databse(string version)
        {
            if (version == "1.0.0")
            {
                version = "1.0.1";
                _exec_nonquery("drop table CacheList");
                _exec_nonquery("create table CacheList(Path varchar(4096) primary key, MD5 binary(16) not null, SHA1 binary(20) not null, CRC32 binary(4) not null, ContentSize bigint not null, CreationTime bigint not null, ModifiedTime bigint not null, DatabaseWriteTime bigint not null)");
                _exec_nonquery("update DbVars set Value = '1.0.1' where Key = 'Version'");
                _sql_trs.Commit();
                _exec_nonquery("vacuum");
                _sql_trs = _sql_con.BeginTransaction();
            }
        }
        #endregion
    }

    [Serializable]
    public struct TrackedData : IComparable
    {
        public string Path;
        public string MD5;
        public string CRC32;
        public string SHA1;

        public ulong ContentSize;
        public DateTime CreationTime;
        public DateTime ModifiedTime;
        public bool IsDir;
        public TrackedData(string path, string md5, string sha1, string crc32, ulong contentSize, DateTime creationTime, DateTime modifiedTime)
        {
            Path = path;
            MD5 = md5;
            SHA1 = sha1;
            CRC32 = crc32;
            ContentSize = contentSize;
            CreationTime = creationTime;
            ModifiedTime = modifiedTime;
            IsDir = false;
        }
        public TrackedData(string path, DateTime creationTime, DateTime modifiedTime)
        {
            Path = path;
            MD5 = string.Empty;
            SHA1 = string.Empty;
            CRC32 = string.Empty;
            ContentSize = 0;
            CreationTime = creationTime;
            ModifiedTime = modifiedTime;
            IsDir = true;
        }
        //public byte[] SerializeData()
        //{
        //    var serialized_path = Encoding.UTF8.GetBytes(Path);

        //    ushort len = serialized_path.Length > 0xffff ? (ushort)0xffff : (ushort)serialized_path.Length;
        //    var ret = new byte[len + 43];
        //    // 2+x B
        //    ret[0] = (byte)(len >> 8);
        //    ret[1] = (byte)(len & 0xff);
        //    Array.Copy(serialized_path, 0, ret, 2, len);
        //    // 16 B
        //    if (string.IsNullOrEmpty(MD5)) MD5 = "00000000000000000000000000000000";
        //    var serialized_md5 = util.Hex(MD5);
        //    Array.Copy(serialized_md5, 0, ret, 2 + len, 16);
        //    // 8 B
        //    ulong temp_size = ContentSize;
        //    for (int i = 7; i >= 0; i--)
        //    {
        //        ret[len + 18 + i] = (byte)(temp_size & 0xff);
        //        temp_size >>= 8;
        //    }
        //    // 8 B
        //    temp_size = (ulong)CreationTime.Ticks;
        //    for (int i = 7; i >= 0; i--)
        //    {
        //        ret[len + 26 + i] = (byte)(temp_size & 0xff);
        //        temp_size >>= 8;
        //    }

        //    // 8 B
        //    temp_size = (ulong)ModifiedTime.Ticks;
        //    for (int i = 7; i >= 0; i--)
        //    {
        //        ret[len + 34 + i] = (byte)(temp_size & 0xff);
        //        temp_size >>= 8;
        //    }
        //    // 1 B
        //    ret[len + 42] = IsDir ? (byte)1 : (byte)0;
        //    return ret;
        //}
        //public void WriteSerialiedData(Stream ostream)
        //{
        //    var data = SerializeData();
        //    ostream.Write(data, 0, data.Length);
        //}
        //public void ReadSerializedData(Stream istream)
        //{
        //    var buffer = new byte[16];
        //    //2+x B
        //    int readed_bytes = 0;
        //    while (readed_bytes < 2)
        //    {
        //        readed_bytes += istream.Read(buffer, readed_bytes, 2 - readed_bytes);
        //    }

        //    ushort len = (ushort)(buffer[0] << 8 | buffer[1]);
        //    buffer = new byte[len];
        //    readed_bytes = 0;
        //    while (readed_bytes < len)
        //    {
        //        readed_bytes += istream.Read(buffer, readed_bytes, len - readed_bytes);
        //    }

        //    Path = Encoding.UTF8.GetString(buffer);
        //    //16 B
        //    buffer = new byte[16];
        //    readed_bytes = 0;
        //    while (readed_bytes < 16)
        //    {
        //        readed_bytes += istream.Read(buffer, readed_bytes, 16 - readed_bytes);
        //    }

        //    MD5 = util.Hex(buffer);
        //    //8 B
        //    ContentSize = 0;
        //    readed_bytes = 0;
        //    while (readed_bytes < 8)
        //    {
        //        readed_bytes += istream.Read(buffer, readed_bytes, 8 - readed_bytes);
        //    }
        //    for (int i = 0; i < 8; i++)
        //    {
        //        ContentSize <<= 8;
        //        ContentSize |= buffer[i];
        //    }
        //    //8B
        //    ulong temp_uint64 = 0;
        //    readed_bytes = 0;
        //    while (readed_bytes < 8)
        //    {
        //        readed_bytes += istream.Read(buffer, readed_bytes, 8 - readed_bytes);
        //    }
        //    for (int i = 0; i < 8; i++)
        //    {
        //        temp_uint64 <<= 8;
        //        temp_uint64 |= buffer[i];
        //    }
        //    CreationTime = new DateTime((long)temp_uint64);

        //    //8B
        //    temp_uint64 = 0;
        //    readed_bytes = 0;
        //    while (readed_bytes < 8)
        //    {
        //        readed_bytes += istream.Read(buffer, readed_bytes, 8 - readed_bytes);
        //    }
        //    for (int i = 0; i < 8; i++)
        //    {
        //        temp_uint64 <<= 8;
        //        temp_uint64 |= buffer[i];
        //    }
        //    ModifiedTime = new DateTime((long)temp_uint64);

        //    //1B
        //    temp_uint64 = 0;
        //    readed_bytes = 0;
        //    while (readed_bytes < 1)
        //    {
        //        readed_bytes += istream.Read(buffer, readed_bytes, 1 - readed_bytes);
        //    }
        //    IsDir = buffer[0] != 0;
        //}

        public int CompareTo(object obj)
        {
            return Path.CompareTo(obj);
        }

        //public TrackedData(Stream istream)
        //{
        //    Path = null; MD5 = null; SHA1 = null;CRC32 = null; ContentSize = 0; CreationTime = new DateTime(0); ModifiedTime = new DateTime(0); IsDir = false;
        //    ReadSerializedData(istream);
        //}
    }

    /// <summary>
    /// 更新本地文件信息的事件
    /// </summary>
    public class LocalFileUpdateEventArgs : EventArgs
    {
        public readonly TrackedData Data;
        public LocalFileUpdateEventArgs() { Data = new TrackedData(); }
        public LocalFileUpdateEventArgs(TrackedData data) { Data = data; }
    }

}
