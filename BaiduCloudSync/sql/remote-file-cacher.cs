using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using GlobalUtil;

namespace BaiduCloudSync
{
    //管理多个PCS API，对文件数据进行sql缓存的类（包含多线程优化）
    //todo: 优化管理线程代码
    public class RemoteFileCacher : IDisposable
    {
        private const string _CACHE_PATH = "data";
        private const string _REMOTE_CACHE_NAME = _CACHE_PATH + "/remote-track.db";

        //sql连接
        private SQLiteConnection _sql_con;
        private SQLiteCommand _sql_cmd;
        private SQLiteTransaction _sql_trs;
        private object _sql_lock;
        //需要初始化sql表时调用的初始化函数
        private void _initialize_sql_tables()
        {
            const string sql_create_filelist_table = "create table FileList "
                + "( FS_ID bigint primary key"
                + ", Category int"
                + ", IsDir tinyint"
                + ", LocalCTime bigint"
                + ", LocalMTime bigint"
                + ", OperID int"
                + ", Path varchar(300)"
                + ", ServerCTime bigint"
                + ", ServerFileName varchar(300)"
                + ", ServerMTime bigint"
                + ", Size bigint"
                + ", Unlist int"
                + ", MD5 binary(16)"
                + ", account_id int"
                + ")";
            const string sql_create_account_table = "create table Account "
                + "( account_id int primary key"
                + ", cookie_identifier char(64)"
                + ", cursor varchar(3000)"
                + ", enabled tinyint"
                + ")";
            const string sql_create_extended_filelist_table = "create table FileListExtended "
                + "( FS_ID bigint primary key"
                + ", CRC32 bianry(4)"
                + ", Downloadable tinyint"
                + ", account_id int"
                + ")";
            const string sql_create_dbvar_table = "create table DbVars (Key varchar(100) primary key, Value varchar(2048))";
            const string sql_insert_account_id = "insert into DbVars(Key, Value) values('account_allocated', '-1')";
            const string sql_insert_version = "insert into DbVars(Key, Value) values('version', '1.0.0')";
            const string sql_query_table_count = "select count(*) from sqlite_master where type = 'table'";

            //opening sql connection
            lock (_sql_lock)
            {
                if (!Directory.Exists(_CACHE_PATH)) Directory.CreateDirectory(_CACHE_PATH);
                if (!File.Exists(_REMOTE_CACHE_NAME)) File.Create(_REMOTE_CACHE_NAME).Close();
                _sql_con = new SQLiteConnection("Data Source=" + _REMOTE_CACHE_NAME + "; Version=3;");
                _sql_con.Open();
                _sql_cmd = new SQLiteCommand(_sql_con);
                _sql_trs = _sql_con.BeginTransaction();

                //querying table count
                _sql_cmd.CommandText = sql_query_table_count;
                var table_count = Convert.ToInt32(_sql_cmd.ExecuteScalar());
                if (table_count == 0)
                {
                    //creating tables while table count == 0
                    _sql_cmd.CommandText = sql_create_filelist_table;
                    _sql_cmd.ExecuteNonQuery();
                    _sql_cmd.CommandText = sql_create_account_table;
                    _sql_cmd.ExecuteNonQuery();
                    _sql_cmd.CommandText = sql_create_extended_filelist_table;
                    _sql_cmd.ExecuteNonQuery();
                    _sql_cmd.CommandText = sql_create_dbvar_table;
                    _sql_cmd.ExecuteNonQuery();
                    _sql_cmd.CommandText = sql_insert_account_id;
                    _sql_cmd.ExecuteNonQuery();
                    _sql_cmd.CommandText = sql_insert_version;
                    _sql_cmd.ExecuteNonQuery();
                    _sql_trs.Commit();
                    _sql_trs = _sql_con.BeginTransaction();
                }
                else
                {
                    //loading account data to memory
                    _sql_cmd.CommandText = "select account_id, cursor, cookie_identifier, enabled from Account";
                    var dr = _sql_cmd.ExecuteReader();
                    while (dr.Read())
                    {
                        var account_id = (int)dr[0];
                        var cursor = (string)dr[1];
                        var cookie_id = (string)dr[2];
                        var enabled = (byte)dr[3] != 0;

                        _account_data.Add(account_id, new _AccountData { cursor = cursor, enabled = enabled, pcs = new BaiduPCS(new BaiduOAuth(cookie_id)) });
                    }
                    dr.Close();
                }
            }
        }




        public RemoteFileCacher()
        {
            _account_data = new Dictionary<int, _AccountData>();

            //thread locks
            _sql_lock = new object();
            _file_diff_thread_fetching_head_lock = new object();
            _account_data_external_lock = new object();

            _sql_cache_path = string.Empty;

            _initialize_sql_tables();
            _file_diff_thread = new Thread(_file_diff_thread_callback);
            _file_diff_thread.IsBackground = true;
            _file_diff_thread.Name = "文件差异比较线程";

            _account_changed = true;
            _file_diff_thread_started = new ManualResetEventSlim();
            _file_diff_thread.Start();
            _file_diff_thread_started.Wait();
        }
        //释放所有资源
        public void Dispose()
        {
            if (_file_diff_thread != null)
            {
                _file_diff_thread_flag = _FILE_DIFF_FLAG_ABORT_REQUEST;
                _file_diff_thread.Join();
                _file_diff_thread = null;
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
        ~RemoteFileCacher()
        {
            Dispose();
        }


        public event EventHandler FileDiffStart, FileDiffFinish;
        public bool IsFileDiffWorking { get { return _is_file_diff_working; } }


        //每个pcs api对应的数据
        private Dictionary<int, _AccountData> _account_data;
        private bool _account_changed; //上面的数据是否已经改变
        private object _account_data_external_lock; //外部线程锁
        //保存包含有账号数据的结构
        private struct _AccountData
        {
            public BaiduPCS pcs;
            public string cursor;
            public bool enabled;
            public override bool Equals(object other)
            {
                if (other.GetType() != typeof(_AccountData)) return false;
                return pcs.Equals(((_AccountData)other).pcs);
            }
            public override int GetHashCode()
            {
                return base.GetHashCode();
            }
            public override string ToString()
            {
                return base.ToString();
            }
            public _AccountData(BaiduPCS data)
            {
                pcs = data;
                cursor = string.Empty;
                enabled = true;
            }
            public static bool operator ==(_AccountData a, _AccountData b) { return a.Equals(b); }
            public static bool operator !=(_AccountData a, _AccountData b) { return !a.Equals(b); }
        }




        private Thread _file_diff_thread; //进行文件差异请求的线程
        private ManualResetEventSlim _file_diff_thread_started; //文件差异线程是否已经开始
        private volatile int _file_diff_thread_flag; //线程标志符
        private const int _FILE_DIFF_FLAG_ABORT_REQUEST = 0x1;
        private const int _FILE_DIFF_FLAG_ABORTED = 0x2;
        private Dictionary<int, _AccountData> _file_diff_thread_fetching_head; //当前获取的账号
        private Dictionary<int, bool> _file_diff_thread_data_dirty; //当前账号的数据是否需要更新
        private bool _is_file_diff_working; //差异线程是否在互联网上下载数据
        private object _file_diff_thread_fetching_head_lock;

        //sql数据缓存
        private string _sql_cache_path; //缓存路径
        private List<ObjectMetadata> _sql_query_result;

        #region comparison classes implement
        private class _sort_meta : IComparer<ObjectMetadata>
        {
            private BaiduPCS.FileOrder _order;
            private bool _asc;
            public _sort_meta(BaiduPCS.FileOrder order, bool asc)
            {
                _order = order;
                _asc = asc;
            }
            int IComparer<ObjectMetadata>.Compare(ObjectMetadata x, ObjectMetadata y)
            {
                if (x.IsDir != y.IsDir)
                {
                    if (x.IsDir) return -1;
                    if (y.IsDir) return 1;
                }
                switch (_order)
                {
                    case BaiduPCS.FileOrder.time:
                        if (x.ServerMTime == y.ServerMTime)
                            return x.ServerFileName.CompareTo(y.ServerFileName);
                        if (_asc)
                            return x.ServerMTime.CompareTo(y.ServerMTime);
                        else
                            return y.ServerMTime.CompareTo(x.ServerMTime);
                    case BaiduPCS.FileOrder.name:
                        if (_asc)
                            return x.ServerFileName.CompareTo(y.ServerFileName);
                        else
                            return y.ServerFileName.CompareTo(x.ServerFileName);
                    case BaiduPCS.FileOrder.size:
                        if (x.Size == y.Size)
                            return x.ServerFileName.CompareTo(y.ServerFileName);
                        if (_asc)
                            return x.Size.CompareTo(y.Size);
                        else
                            return y.Size.CompareTo(x.Size);
                    default:
                        break;
                }
                throw new NotSupportedException();
            }
        }

        #endregion

        //主监控线程回调
        private void _file_diff_thread_callback()
        {
            _file_diff_thread_started.Set();
            while (_file_diff_thread_flag == 0)
            {

                lock (_account_data_external_lock)
                {
                    if (_account_changed)
                    {
                        //copying account data, in order to prevent change during fetching time
                        _file_diff_thread_fetching_head = new Dictionary<int, _AccountData>();
                        _file_diff_thread_data_dirty = new Dictionary<int, bool>();
                        foreach (var item in _account_data)
                        {
                            if (item.Value.enabled)
                            {
                                _file_diff_thread_fetching_head.Add(item.Key, item.Value);
                                _file_diff_thread_data_dirty.Add(item.Key, true);
                            }
                        }
                        _is_file_diff_working = true;
                        try { FileDiffStart?.Invoke(this, new EventArgs()); } catch { }
                        _account_changed = false;
                    }
                }

                //async execution for multi thread
                int account_count = _file_diff_thread_fetching_head.Count;
                int finished_count = 0;
                bool is_data_dirty = false;
                var rst_event = new ManualResetEventSlim();

                foreach (var item in _file_diff_thread_fetching_head)
                {
                    if (_file_diff_thread_data_dirty[item.Key])
                    {
                        is_data_dirty = true;
                        var cursor = item.Value.cursor;
                        lock (_file_diff_thread_fetching_head_lock)
                        {
                            item.Value.pcs.GetFileDiffAsync(cursor, _file_diff_data_callback, new KeyValuePair<object, object>(item.Key, rst_event));
                            _file_diff_thread_data_dirty[item.Key] = false;
                        }
                        _is_file_diff_working = true;
                        try { FileDiffStart?.Invoke(this, new EventArgs()); } catch { }
                    }
                    else
                        finished_count++;
                }

                //join all threads
                while (finished_count < account_count)
                {
                    rst_event.Wait();
                    rst_event.Reset();
                    finished_count++;
                }
                _is_file_diff_working = false;
                try { FileDiffFinish?.Invoke(this, new EventArgs()); } catch { }

                if (!is_data_dirty)
                {
                    //data cleared, ignoring
                    Thread.Sleep(1000);
                }

            }
        }
        //http异步请求线程回调
        private void _file_diff_data_callback(bool suc, bool has_more, bool reset, string next_cursor, ObjectMetadata[] result, object state)
        {
            var index = (int)((KeyValuePair<object, object>)state).Key;
            if (!suc)
            {
                //failed: retry in 3 seconds
                ThreadPool.QueueUserWorkItem(delegate
                {
                    Thread.Sleep(3000);
                    _AccountData data2;
                    lock (_file_diff_thread_fetching_head_lock)
                    {
                        data2 = _file_diff_thread_fetching_head[index];
                    }
                    data2.pcs.GetFileDiffAsync(data2.cursor, _file_diff_data_callback, state);
                });
            }

            if (reset)
            {
                //reset all files from sql database
                string reset_sql = "delete from FileList where account_id = " + index;
                string reset_sql2 = "delete from FileListExtended where account_id = " + index;
                lock (_sql_lock)
                {
                    _sql_cmd.CommandText = reset_sql;
                    _sql_cmd.ExecuteNonQuery();
                    _sql_cmd.CommandText = reset_sql2;
                    _sql_cmd.ExecuteNonQuery();
                    _sql_trs.Commit();
                    _sql_trs = _sql_con.BeginTransaction();
                }
            }

            //modifying cursor
            _AccountData data;
            lock (_file_diff_thread_fetching_head_lock)
            {
                data = _file_diff_thread_fetching_head[index];
                data.cursor = next_cursor;
                _file_diff_thread_fetching_head[index] = data;
            }
            //updating sql
            var update_account_sql = "update Account set cursor = @next_cursor where account_id = " + index;
            lock (_sql_lock)
            {
                _sql_cmd.CommandText = update_account_sql;
                _sql_cmd.Parameters.Add("@next_cursor", System.Data.DbType.String);
                _sql_cmd.Parameters["@next_cursor"].Value = next_cursor;
                _sql_cmd.ExecuteNonQuery();
                _sql_cmd.Parameters.Clear();
            }

            if (has_more)
            {
                //fetching the continous data
                data.pcs.GetFileDiffAsync(next_cursor, _file_diff_data_callback, state);
            }

            //updating data using sql
            var delete_sql = "delete from FileList where account_id = " + index + " and FS_ID = ";
            var delete_sql2 = "delete from FileListExtended where account_id = " + index + " and FS_ID = ";
            var insert_sql = "insert into FileList(FS_ID, Category, IsDir, LocalCTime, LocalMTime, OperID, Path, ServerCTime, ServerFileName, ServerMTime, Size, Unlist, MD5, account_id) values " +
                "(@FS_ID, @Category, @IsDir, @LocalCTime, @LocalMTime, @OperID, @Path, @ServerCTime, @ServerFileName, @ServerMTime, @Size, @Unlist, @MD5, @account_id)";
            var update_sql = "update FileList set Category = @Category, IsDir = @IsDir, LocalCTime = @LocalCTime, LocalMTime = @LocalMTime, OperID = @OperID, Path = @Path, ServerCTime = @ServerCTime, ServerFileName = @ServerFileName, ServerMTime = @ServerMTime, Size = @Size, Unlist = @Unlist, MD5 = @MD5, account_id = @account_id where FS_ID = @FS_ID";
            var query_sql = "select count(*) from FileList where FS_ID = @FS_ID";
            lock (_sql_lock)
                foreach (var item in result)
                {
                    if (item.IsDelete)
                    {
                        _sql_cmd.CommandText = delete_sql + item.FS_ID;
                        _sql_cmd.ExecuteNonQuery();
                        _sql_cmd.CommandText = delete_sql2 + item.FS_ID;
                        _sql_cmd.ExecuteNonQuery();
                    }
                    else
                    {
                        _sql_cmd.Parameters.Add("@FS_ID", System.Data.DbType.Int64);
                        _sql_cmd.Parameters["@FS_ID"].Value = (long)item.FS_ID;
                        _sql_cmd.CommandText = query_sql;
                        var count = Convert.ToInt32(_sql_cmd.ExecuteScalar());
                        if (count == 0)
                            _sql_cmd.CommandText = insert_sql;
                        else
                        {
                            _sql_cmd.CommandText = update_sql;
                            Tracer.GlobalTracer.TraceWarning("[W] FS_ID " + item.FS_ID + "(" + item.Path + ") has already cached in sql, overwriting data!");
                        }
                        _sql_cmd.Parameters.Add("@Category", System.Data.DbType.Int32);
                        _sql_cmd.Parameters["@Category"].Value = (int)item.Category;
                        _sql_cmd.Parameters.Add("@IsDir", System.Data.DbType.Byte);
                        _sql_cmd.Parameters["@IsDir"].Value = (byte)(item.IsDir ? 1 : 0);
                        _sql_cmd.Parameters.Add("@LocalCTime", System.Data.DbType.Int64);
                        _sql_cmd.Parameters["@LocalCTime"].Value = (long)item.LocalCTime;
                        _sql_cmd.Parameters.Add("@LocalMTime", System.Data.DbType.Int64);
                        _sql_cmd.Parameters["@LocalMTime"].Value = (long)item.LocalMTime;
                        _sql_cmd.Parameters.Add("@OperID", System.Data.DbType.Int32);
                        _sql_cmd.Parameters["@OperID"].Value = (int)item.OperID;
                        _sql_cmd.Parameters.Add("@Path", System.Data.DbType.String);
                        _sql_cmd.Parameters["@Path"].Value = item.Path;
                        _sql_cmd.Parameters.Add("@ServerCTime", System.Data.DbType.Int64);
                        _sql_cmd.Parameters["@ServerCTime"].Value = (long)item.ServerCTime;
                        _sql_cmd.Parameters.Add("@ServerFileName", System.Data.DbType.String);
                        _sql_cmd.Parameters["@ServerFileName"].Value = item.ServerFileName;
                        _sql_cmd.Parameters.Add("@ServerMTime", System.Data.DbType.Int64);
                        _sql_cmd.Parameters["@ServerMTime"].Value = (long)item.ServerMTime;
                        _sql_cmd.Parameters.Add("@Size", System.Data.DbType.Int64);
                        _sql_cmd.Parameters["@Size"].Value = (long)item.Size;
                        _sql_cmd.Parameters.Add("@Unlist", System.Data.DbType.Int32);
                        _sql_cmd.Parameters["@Unlist"].Value = (int)item.Unlist;
                        _sql_cmd.Parameters.Add("@MD5", System.Data.DbType.Binary);
                        _sql_cmd.Parameters["@MD5"].Value = util.Hex(item.MD5);
                        _sql_cmd.Parameters.Add("@account_id", System.Data.DbType.Int32);
                        _sql_cmd.Parameters["@account_id"].Value = index;
                        _sql_cmd.ExecuteNonQuery();
                        _sql_cmd.Parameters.Clear();
                    }
                }

            //completed fetch, interrupting monitor thread
            if (!has_more)
            {
                lock (_sql_lock)
                {
                    _sql_trs.Commit();
                    _sql_trs = _sql_con.BeginTransaction();
                }
                if (_file_diff_thread != null)
                    ((ManualResetEventSlim)((KeyValuePair<object, object>)state).Value).Set();
            }
        }
        //在sql数据库中读取文件数据
        private void _get_file_list_from_sql(string path, BaiduPCS.MultiObjectMetaCallback callback, int account_id, BaiduPCS.FileOrder order = BaiduPCS.FileOrder.name, bool asc = true, int page = 1, int size = 1000, object state = null)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentNullException("path");
            if (size <= 0) throw new ArgumentOutOfRangeException("size");
            if (page <= 0) throw new ArgumentOutOfRangeException("page");
            if (!path.EndsWith("/")) path += "/";
            var sql_text = "select FS_ID, Category, IsDir, LocalCTime, LocalMTime, OperID, Path, ServerCTime, ServerFileName, ServerMTime, Size, Unlist, MD5" +
                " from FileList where account_id = " + account_id + " and path like '" + path + "%' and path not like '" + path + "%/%'";

            ThreadPool.QueueUserWorkItem(delegate
            {
                var ret = new List<ObjectMetadata>();
                //to memory cache
                lock (_sql_lock)
                {
                    if (_sql_cache_path != path)
                    {
                        _sql_cache_path = path;
                        _sql_cmd.CommandText = sql_text;
                        var dr = _sql_cmd.ExecuteReader();
                        var meta_list = new List<ObjectMetadata>();
                        while (dr.Read())
                        {
                            var new_meta = new ObjectMetadata();
                            if (!dr.IsDBNull(0)) new_meta.FS_ID = (ulong)(long)dr[0];
                            if (!dr.IsDBNull(1)) new_meta.Category = (uint)(int)dr[1];
                            if (!dr.IsDBNull(2)) new_meta.IsDir = (byte)dr[2] != 0;
                            if (!dr.IsDBNull(3)) new_meta.LocalCTime = (ulong)(long)dr[3];
                            if (!dr.IsDBNull(4)) new_meta.LocalMTime = (ulong)(long)dr[4];
                            if (!dr.IsDBNull(5)) new_meta.OperID = (uint)(int)dr[5];
                            if (!dr.IsDBNull(6)) new_meta.Path = (string)dr[6];
                            if (!dr.IsDBNull(7)) new_meta.ServerCTime = (ulong)(long)dr[7];
                            if (!dr.IsDBNull(8)) new_meta.ServerFileName = (string)dr[8];
                            if (!dr.IsDBNull(9)) new_meta.ServerMTime = (ulong)(long)dr[9];
                            if (!dr.IsDBNull(10)) new_meta.Size = (ulong)(long)dr[10];
                            if (!dr.IsDBNull(11)) new_meta.Unlist = (uint)(int)dr[11];
                            if (!dr.IsDBNull(12)) new_meta.MD5 = util.Hex((byte[])dr[12]);
                            new_meta.AccountID = account_id;
                            meta_list.Add(new_meta);
                        }
                        dr.Close();

                        //sorting
                        meta_list.Sort(new _sort_meta(order, asc));

                        _sql_query_result = meta_list;
                    }

                    //return from cache
                    int offset = page * size;
                    for (int i = offset - size; i < _sql_query_result.Count && i < offset; i++)
                    {
                        ret.Add(_sql_query_result[i]);
                    }
                }

                callback?.Invoke(true, ret.ToArray(), state);
            });
        }

        #region public functions inherits from pcs api
        /// <summary>
        /// 获取所有账号信息
        /// </summary>
        /// <returns></returns>
        public BaiduPCS[] GetAllAccounts()
        {
            lock (_account_data_external_lock)
            {
                var ret = new BaiduPCS[_account_data.Count];
                int i = 0;
                foreach (var item in _account_data)
                {
                    ret[i++] = item.Value.pcs;
                }
                return ret;
            }
        }
        /// <summary>
        /// 获取指定账号的id
        /// </summary>
        /// <param name="pcs">pcs api</param>
        /// <returns></returns>
        public int GetAccountId(BaiduPCS pcs)
        {
            lock (_account_data_external_lock)
            {
                var tmp_data = new _AccountData { pcs = pcs, cursor = null, enabled = false };
                foreach (var item in _account_data)
                {
                    if (item.Value == tmp_data)
                        return item.Key;
                }
                return -1;
            }
        }
        /// <summary>
        /// 获取该账号是否开启文件读写权限
        /// </summary>
        /// <param name="id">账号id</param>
        /// <returns></returns>
        public bool GetAccountEnabled(int id)
        {
            lock (_account_data_external_lock)
            {
                if (!_account_data.ContainsKey(id)) throw new ArgumentOutOfRangeException("id");
                return _account_data[id].enabled;
            }
        }
        /// <summary>
        /// 获取指定id下的pcs api
        /// </summary>
        /// <param name="id">账号id</param>
        /// <returns></returns>
        public BaiduPCS GetAccount(int id)
        {
            lock (_account_data_external_lock)
            {
                if (!_account_data.ContainsKey(id)) throw new KeyNotFoundException("id不存在");
                return _account_data[id].pcs;
            }
        }

        public BaiduPCS this[int id]
        {
            get
            {
                return GetAccount(id);
            }
            set
            {
                SetAccountData(id, value);
            }
        }

        /// <summary>
        /// 设置该账号是否开启文件读写权限
        /// </summary>
        /// <param name="id">账号id</param>
        /// <param name="enabled">是否开启读写权限</param>
        public void SetAccountEnabled(int id, bool enabled)
        {
            lock (_account_data_external_lock)
            {
                if (!_account_data.ContainsKey(id)) throw new ArgumentOutOfRangeException("id");
                var data = _account_data[id];
                data.enabled = enabled;
                _account_data[id] = data;
                _account_changed = true;

                lock (_sql_lock)
                {
                    _sql_cmd.CommandText = "update Account set enabled = " + (enabled ? 1 : 0);
                    _sql_cmd.ExecuteNonQuery();
                }
            }
        }
        /// <summary>
        /// 更改指定id的pcs api
        /// </summary>
        /// <param name="id">账号id</param>
        /// <param name="pcs">pcs api</param>
        public void SetAccountData(int id, BaiduPCS pcs)
        {
            lock (_account_data_external_lock)
            {
                if (!_account_data.ContainsKey(id)) throw new ArgumentOutOfRangeException("id");
                var data = _account_data[id];
                data.pcs = pcs;
                _account_data[id] = data;
                _account_changed = true;

                lock (_sql_lock)
                {
                    _sql_cmd.CommandText = "update Account set cookie_identifier = " + pcs.Auth.CookieIdentifier;
                    _sql_cmd.ExecuteNonQuery();
                }
            }
        }
        /// <summary>
        /// 增加账号
        /// </summary>
        /// <param name="pcs">pcs api</param>
        /// <returns>返回账号id</returns>
        public int AddAccount(BaiduPCS pcs)
        {
            lock (_account_data_external_lock)
            {
                lock (_sql_lock)
                {
                    _sql_cmd.CommandText = "select Value from DbVars where Key = 'account_allocated'";
                    var dr = _sql_cmd.ExecuteReader();
                    dr.Read();
                    var result = int.Parse((string)dr[0]) + 1;
                    dr.Close();

                    _sql_cmd.CommandText = "update DbVars set Value = '" + result + "' where Key = 'account_allocated'";
                    _sql_cmd.ExecuteNonQuery();

                    _account_data.Add(result, new _AccountData { cursor = string.Empty, enabled = true, pcs = pcs });

                    _sql_cmd.CommandText = "insert into Account(account_id, cookie_identifier, cursor, enabled) values(@account_id, @cookie_identifier, @cursor, @enabled)";
                    _sql_cmd.Parameters.Add("@account_id", System.Data.DbType.Int32);
                    _sql_cmd.Parameters["@account_id"].Value = result;
                    _sql_cmd.Parameters.Add("@cookie_identifier", System.Data.DbType.String);
                    _sql_cmd.Parameters["@cookie_identifier"].Value = pcs.Auth.CookieIdentifier;
                    _sql_cmd.Parameters.Add("@cursor", System.Data.DbType.String);
                    _sql_cmd.Parameters["@cursor"].Value = "null";
                    _sql_cmd.Parameters.Add("@enabled", System.Data.DbType.Byte);
                    _sql_cmd.Parameters["@enabled"].Value = (byte)1;
                    _sql_cmd.ExecuteNonQuery();
                    _sql_cmd.Parameters.Clear();

                    _account_changed = true;
                }
            }
            return -1;
        }
        /// <summary>
        /// 移除账号
        /// </summary>
        /// <param name="id">账号id</param>
        public void RemoveAccount(int id)
        {
            if (!_account_data.ContainsKey(id)) throw new ArgumentOutOfRangeException("id");
            lock (_account_data_external_lock)
            {
                _account_data.Remove(id);
                lock (_sql_lock)
                {
                    _sql_cmd.CommandText = "delete from Account where account_id = " + id;
                    _sql_cmd.ExecuteNonQuery();
                }
            }
        }

        /// <summary>
        /// 重置账号缓存
        /// </summary>
        /// <param name="id">账号id</param>
        public void ResetCache(int id)
        {
            if (!_account_data.ContainsKey(id)) throw new ArgumentOutOfRangeException("id");
            lock (_account_data_external_lock)
            {
                while (_is_file_diff_working || _account_changed) Thread.Sleep(10);
                lock (_sql_lock)
                {
                    _sql_cmd.CommandText = "update Account set cursor = 'null' where account_id = " + id;
                    _sql_cmd.ExecuteNonQuery();
                    _sql_cmd.CommandText = "delete from FileList where account_id = " + id;
                    _sql_cmd.ExecuteNonQuery();
                    _sql_cmd.CommandText = "delete from FileListExtended where account_id = " + id;
                    _sql_cmd.ExecuteNonQuery();

                    _sql_trs.Commit();
                    _sql_trs = _sql_con.BeginTransaction();
                }
            }
        }
        #endregion




        #region public functions for accounts
        /// <summary>
        /// 获取文件列表
        /// </summary>
        /// <param name="path">路径</param>
        /// <param name="callback">回调函数</param>
        /// <param name="account_id">账号id</param>
        /// <param name="order">排序顺序</param>
        /// <param name="asc">正序</param>
        /// <param name="page">页数</param>
        /// <param name="size">每页大小</param>
        public void GetFileListAsync(string path, BaiduPCS.MultiObjectMetaCallback callback, int account_id = 0, BaiduPCS.FileOrder order = BaiduPCS.FileOrder.name, bool asc = true, int page = 1, int size = 1000, object state = null)
        {
            if (path.EndsWith("/") && path != "/") path = path.Substring(0, path.Length - 1);
            if (_is_file_diff_working || _account_changed)
            {
                if (!_account_data.ContainsKey(account_id)) throw new ArgumentOutOfRangeException("account_id");
                _account_data[account_id].pcs.GetFileListAsync(path, (suc, data, s) =>
                {
                    for (int i = 0; suc && i < data.Length; i++)
                    {
                        data[i].AccountID = account_id;
                    }
                    try { callback?.Invoke(suc, data, s); }
                    catch { }
                }, order, asc, page, size, state);
            }
            else
            {
                _get_file_list_from_sql(path, callback, account_id, order, asc, page, size, state);
            }
        }
        /// <summary>
        /// 创建文件夹
        /// </summary>
        /// <param name="path">文件路径</param>
        /// <param name="callback">回调函数</param>
        /// <param name="account_id">账号id</param>
        public void CreateDirectoryAsync(string path, BaiduPCS.ObjectMetaCallback callback, int account_id = 0, object state = null)
        {
            if (path.EndsWith("/")) path = path.Substring(0, path.Length - 1);
            lock (_account_data_external_lock)
            {
                if (!_account_data.ContainsKey(account_id)) throw new ArgumentOutOfRangeException("account_id");
                _account_data[account_id].pcs.CreateDirectoryAsync(path, callback, state);
                lock (_file_diff_thread_fetching_head_lock)
                {
                    if (_file_diff_thread_data_dirty.ContainsKey(account_id))
                    {
                        _file_diff_thread_data_dirty[account_id] = true;
                        _is_file_diff_working = true;
                    }
                }
            }
        }

        public void MovePathAsync(string source, string destination, BaiduPCS.OperationCallback callback, int account_id = 0, BaiduPCS.ondup ondup = BaiduPCS.ondup.overwrite, object state = null)
        {
            if (source.EndsWith("/")) source = source.Substring(0, source.Length - 1);
            if (destination.EndsWith("/")) destination = destination.Substring(0, destination.Length - 1);
            MovePathAsync(new string[] { source }, new string[] { destination }, callback, account_id, ondup, state);
        }
        public void MovePathAsync(IEnumerable<string> source, IEnumerable<string> destination, BaiduPCS.OperationCallback callback, int account_id = 0, BaiduPCS.ondup ondup = BaiduPCS.ondup.overwrite, object state = null)
        {
            lock (_account_data_external_lock)
            {
                if (!_account_data.ContainsKey(account_id)) throw new ArgumentOutOfRangeException("account_id");
                _account_data[account_id].pcs.MovePathAsync(source, destination, callback, ondup, state);
                lock (_file_diff_thread_fetching_head_lock)
                {
                    if (_file_diff_thread_data_dirty.ContainsKey(account_id))
                        _file_diff_thread_data_dirty[account_id] = true;
                    _is_file_diff_working = true;
                }
            }
        }
        public void CopyPathAsync(string source, string destination, BaiduPCS.OperationCallback callback, int account_id = 0, BaiduPCS.ondup ondup = BaiduPCS.ondup.overwrite, object state = null)
        {
            if (source.EndsWith("/")) source = source.Substring(0, source.Length - 1);
            if (destination.EndsWith("/")) destination = destination.Substring(0, destination.Length - 1);
            CopyPathAsync(new string[] { source }, new string[] { destination }, callback, account_id, ondup, state);
        }
        public void CopyPathAsync(IEnumerable<string> source, IEnumerable<string> destination, BaiduPCS.OperationCallback callback, int account_id = 0, BaiduPCS.ondup ondup = BaiduPCS.ondup.overwrite, object state = null)
        {
            lock (_account_data_external_lock)
            {
                if (!_account_data.ContainsKey(account_id)) throw new ArgumentOutOfRangeException("account_id");
                _account_data[account_id].pcs.CopyPathAsync(source, destination, callback, ondup, state);
                lock (_file_diff_thread_fetching_head_lock)
                {
                    if (_file_diff_thread_data_dirty.ContainsKey(account_id))
                        _file_diff_thread_data_dirty[account_id] = true;
                    _is_file_diff_working = true;
                }
            }
        }
        public void RenameAsync(string source, string new_name, BaiduPCS.OperationCallback callback, int account_id = 0, object state = null)
        {
            if (source.EndsWith("/")) source = source.Substring(0, source.Length - 1);
            if (!_account_data.ContainsKey(account_id)) throw new ArgumentOutOfRangeException("account_id");
            RenameAsync(new string[] { source }, new string[] { new_name }, callback, account_id, state);
        }
        public void RenameAsync(IEnumerable<string> source, IEnumerable<string> new_name, BaiduPCS.OperationCallback callback, int account_id = 0, object state = null)
        {
            lock (_account_data_external_lock)
            {
                if (!_account_data.ContainsKey(account_id)) throw new ArgumentOutOfRangeException("account_id");
                _account_data[account_id].pcs.RenameAsync(source, new_name, callback, state);
                lock (_file_diff_thread_fetching_head_lock)
                {
                    if (_file_diff_thread_data_dirty.ContainsKey(account_id))
                        _file_diff_thread_data_dirty[account_id] = true;
                    _is_file_diff_working = true;
                }
            }
        }
        public void DeletePathAsync(string path, BaiduPCS.OperationCallback callback, int account_id = 0, object state = null)
        {
            if (path.EndsWith("/")) path = path.Substring(0, path.Length - 1);
            DeletePathAsync(new string[] { path }, callback, account_id, state);
        }
        public void DeletePathAsync(IEnumerable<string> path, BaiduPCS.OperationCallback callback, int account_id = 0, object state = null)
        {
            lock (_account_data_external_lock)
            {
                if (!_account_data.ContainsKey(account_id)) throw new ArgumentOutOfRangeException("account_id");
                _account_data[account_id].pcs.DeletePathAsync(path, callback, state);
                lock (_file_diff_thread_fetching_head_lock)
                {
                    if (_file_diff_thread_data_dirty.ContainsKey(account_id))
                        _file_diff_thread_data_dirty[account_id] = true;
                    _is_file_diff_working = true;
                }
            }
        }

        public void RapidUploadAsync(string path, ulong content_length, string content_md5, string content_crc32, string slice_md5, BaiduPCS.ObjectMetaCallback callback, BaiduPCS.ondup ondup = BaiduPCS.ondup.overwrite, int account_id = 0, object state = null)
        {
            lock (_account_data_external_lock)
            {
                if (!_account_data.ContainsKey(account_id)) throw new ArgumentOutOfRangeException("account_id");
                _account_data[account_id].pcs.RapidUploadAsync(path, content_length, content_md5, content_crc32, slice_md5, callback, ondup, state);
                lock (_file_diff_thread_fetching_head_lock)
                {
                    if (_file_diff_thread_data_dirty.ContainsKey(account_id))
                        _file_diff_thread_data_dirty[account_id] = true;
                    _is_file_diff_working = true;
                }
            }
        }

        public void PreCreateFileAsync(string path, int block_count, BaiduPCS.PreCreateCallback callback, int account_id = 0, object state = null)
        {
            lock (_account_data_external_lock)
            {
                if (!_account_data.ContainsKey(account_id)) throw new ArgumentOutOfRangeException("account_id");
                _account_data[account_id].pcs.PreCreateFileAsync(path, block_count, callback, state);
            }
        }

        public void UploadSliceBeginAsync(ulong content_length, string path, string uploadid, int sequence, BaiduPCS.UploadCallback callback, int account_id = 0, object state = null)
        {
            lock (_account_data_external_lock)
            {
                if (!_account_data.ContainsKey(account_id)) throw new ArgumentOutOfRangeException("account_id");
                _account_data[account_id].pcs.UploadSliceBeginAsync(content_length, path, uploadid, sequence, callback, state);
            }
        }
        public void UploadSliceCancelAsync(Guid task_id, int account_id = 0)
        {
            lock (_account_data_external_lock)
            {
                if (!_account_data.ContainsKey(account_id)) throw new ArgumentOutOfRangeException("account_id");
                _account_data[account_id].pcs.UploadSliceCancelAsync(task_id);
            }
        }
        public void UploadSliceEndAsync(Guid task_id, BaiduPCS.SliceUploadCallback callback, int account_id = 0, object state = null)
        {
            lock (_account_data_external_lock)
            {
                if (!_account_data.ContainsKey(account_id)) throw new ArgumentOutOfRangeException("account_id");
                _account_data[account_id].pcs.UploadSliceEndAsync(task_id, callback, state);
            }
        }

        public void UploadBeginAsync(ulong content_length, string path, BaiduPCS.UploadCallback callback, BaiduPCS.ondup ondup = BaiduPCS.ondup.overwrite, int account_id = 0, object state = null)
        {
            lock (_account_data_external_lock)
            {
                if (!_account_data.ContainsKey(account_id)) throw new ArgumentOutOfRangeException("account_id");
                _account_data[account_id].pcs.UploadBeginAsync(content_length, path, callback, ondup, state);
            }
        }
        public void UploadEndAsync(Guid task_id, BaiduPCS.ObjectMetaCallback callback, int account_id = 0, object state = null)
        {
            lock (_account_data_external_lock)
            {
                if (!_account_data.ContainsKey(account_id)) throw new ArgumentOutOfRangeException("account_id");
                _account_data[account_id].pcs.UploadEndAsync(task_id, callback, state);
                lock (_file_diff_thread_fetching_head_lock)
                {
                    if (_file_diff_thread_data_dirty.ContainsKey(account_id))
                        _file_diff_thread_data_dirty[account_id] = true;
                    _is_file_diff_working = true;
                }
            }
        }

        public void CreteSuperFileAsync(string path, string uploadid, IEnumerable<string> block_list, ulong file_size, BaiduPCS.ObjectMetaCallback callback, int account_id = 0, object state = null)
        {
            lock (_account_data_external_lock)
            {
                if (!_account_data.ContainsKey(account_id)) throw new ArgumentOutOfRangeException("account_id");
                _account_data[account_id].pcs.CreateSuperFileAsync(path, uploadid, block_list, file_size, callback, state);
                lock (_file_diff_thread_fetching_head_lock)
                {
                    if (_file_diff_thread_data_dirty.ContainsKey(account_id))
                        _file_diff_thread_data_dirty[account_id] = true;
                    _is_file_diff_working = true;
                }
            }
        }

        //extended functions
        public void ConvertFromSymbolLinkAsync(string path, BaiduPCS.ObjectMetaCallback callback, string dst_path = null, int account_id = 0, object state = null)
        {
            lock (_account_data_external_lock)
            {
                if (!_account_data.ContainsKey(account_id)) throw new ArgumentOutOfRangeException("account_id");
                _account_data[account_id].pcs.ConvertFromSymbolLinkAsync(path, callback, dst_path, state);
                lock (_file_diff_thread_fetching_head_lock)
                {
                    if (_file_diff_thread_data_dirty.ContainsKey(account_id))
                        _file_diff_thread_data_dirty[account_id] = true;
                    _is_file_diff_working = true;
                }
            }
        }
        public void ConvertToSymbolLinkAsync(string path, BaiduPCS.ObjectMetaCallback callback, string dst_path = null, int account_id = 0, object state = null)
        {
            lock (_account_data_external_lock)
            {
                if (!_account_data.ContainsKey(account_id)) throw new ArgumentOutOfRangeException("account_id");
                _account_data[account_id].pcs.ConvertToSymbolLinkAsync(path, callback, dst_path, state);
                lock (_file_diff_thread_fetching_head_lock)
                {
                    if (_file_diff_thread_data_dirty.ContainsKey(account_id))
                        _file_diff_thread_data_dirty[account_id] = true;
                    _is_file_diff_working = true;
                }
            }
        }
        #endregion
    }
}
