using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using System.Text;
using System.Threading;

namespace BaiduCloudSync
{
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
            const string sql_query_table_count = "select count(*) from sqlite_master where type = 'table'";

            //opening sql connection
            lock (_sql_lock)
            {
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
                    _sql_trs.Commit();
                    _sql_trs = _sql_con.BeginTransaction();
                }
                //else
                //{
                //    //loading tracking data to memory
                //}
            }
        }

        //每个pcs api对应的数据
        private List<_AccountData> _account_data;
        private bool _account_changed; //上面的数据是否已经改变
        private object _account_data_external_lock; //外部线程锁

        public RemoteFileCacher()
        {
            _account_data = new List<_AccountData>();
            //todo: initialize account data from sql

            //thread locks
            _sql_lock = new object();
            _file_diff_thread_fetching_head_lock = new object();
            _account_data_external_lock = new object();


            _initialize_sql_tables();
            _file_diff_thread = new Thread(_file_diff_thread_callback);
            _file_diff_thread.IsBackground = true;
            _file_diff_thread.Name = "文件差异比较线程";

            _account_changed = true;
            _file_diff_thread.Start();
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

        private Thread _file_diff_thread;
        private volatile int _file_diff_thread_flag;
        private const int _FILE_DIFF_FLAG_ABORT_REQUEST = 0x1;
        private const int _FILE_DIFF_FLAG_ABORTED = 0x2;
        private List<_AccountData> _file_diff_thread_fetching_head;
        private object _file_diff_thread_fetching_head_lock;
        private void _file_diff_thread_callback()
        {
            while (_file_diff_thread_flag == 0)
            {
                //nothing to do, waiting for next check
                if (!_account_changed)
                {
                    Thread.Sleep(5000);
                    continue;
                }

                //copying account data, in order to prevent change during fetching time
                _file_diff_thread_fetching_head = new List<_AccountData>();
                lock (_account_data_external_lock)
                {
                    foreach (var item in _account_data)
                    {
                        if (item.enabled)
                            _file_diff_thread_fetching_head.Add(item);
                    }
                }

                int account_count = _file_diff_thread_fetching_head.Count;
                int finished_count = 0;
                for (int i = 0; i < account_count; i++)
                {
                    var cursor = _file_diff_thread_fetching_head[i].cursor;
                    lock (_file_diff_thread_fetching_head_lock)
                        _file_diff_thread_fetching_head[i].pcs.GetFileDiffAsync(cursor, _file_diff_data_callback, i);
                }

                while (finished_count < account_count)
                {
                    try
                    {
                        Thread.Sleep(15000); //max waiting 15 seconds to fetch
                    }
                    catch (ThreadInterruptedException) { }
                    catch (Exception ex)
                    {
                        Tracer.GlobalTracer.TraceError(ex);
                    }
                }
                //multi-accessing thread
            }
        }
        private void _file_diff_data_callback(bool suc, bool has_more, bool reset, string next_cursor, BaiduPCS.ObjectMetadata[] result, object state)
        {
            if (!suc)
            {
                //failed: retry in 3 seconds
                ThreadPool.QueueUserWorkItem(delegate
                {
                    Thread.Sleep(3000);
                    _AccountData data;
                    lock (_file_diff_thread_fetching_head_lock)
                    {
                        data = _file_diff_thread_fetching_head[(int)state];
                    }
                    data.pcs.GetFileDiffAsync(data.cursor, _file_diff_data_callback, state);
                });
            }

            if (reset)
            {
                //reset all files from sql database
                string reset_sql = "delete from FileList where account_id = " + (int)state;
                string reset_sql2 = "delete from FileListExtended where account_id = " + (int)state;
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

            if (has_more)
            {
                //fetching the continous data

                //modifying cursor
                _AccountData data;
                lock (_file_diff_thread_fetching_head_lock)
                {
                    data = _file_diff_thread_fetching_head[(int)state];
                    data.cursor = next_cursor;
                    _file_diff_thread_fetching_head[(int)state] = data;
                }
                //updating sql
                var update_sql = "update Account set cursor = " + next_cursor + " where account_id = " + (int)state;
                lock (_sql_lock)
                {
                    _sql_cmd.CommandText = update_sql;
                    _sql_cmd.ExecuteNonQuery();
                }
                //fetching data
                data.pcs.GetFileDiffAsync(next_cursor, _file_diff_data_callback, state);
            }

            //updating data using sql
            var delete_sql = "delete from FileList where account_id = " + (int)state + " and FS_ID = ";
            var delete_sql2 = "delete from FileListExtended where account_id = " + (int)state + " and FS_ID = ";
            var insert_sql = "insert into FileList(FS_ID, Category, IsDir, LocalCTime, LocalMTime, OperID, Path, ServerCTime, ServerFileName, ServerMTime, Size, Unlist, MD5) values " +
                "(@FS_ID, @Category, @IsDir, @LocalCTime, @LocalMTime, @OperID, @Path, @ServerCTime, @ServerFileName, @ServerMTime, @Size, @Unlist, @MD5)";
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
                        _sql_cmd.CommandText = insert_sql;
                        _sql_cmd.Parameters.Add("@FS_ID", System.Data.DbType.Int64);
                        _sql_cmd.Parameters["@FS_ID"].Value = (long)item.FS_ID;
                        _sql_cmd.Parameters.Add("@Category", System.Data.DbType.Int32);
                        _sql_cmd.Parameters["@Category"].Value = (int)item.Category;
                        _sql_cmd.Parameters.Add("@IsDir", System.Data.DbType.Int16);
                        _sql_cmd.Parameters["@IsDir"].Value = (short)(item.IsDir ? 1 : 0);
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
                        _sql_cmd.ExecuteNonQuery();
                        _sql_cmd.Parameters.Clear();
                    }
                }
        }
        #region public functions inherits from pcs api
        public BaiduPCS[] GetAllAccounts()
        {
            lock (_account_data_external_lock)
            {
                var ret = new BaiduPCS[_account_data.Count];
                for (int i = 0; i < _account_data.Count; i++)
                {
                    ret[i] = _account_data[i].pcs;
                }
                return ret;
            }
        }
        public int GetAccountId(BaiduPCS pcs)
        {
            lock (_account_data_external_lock)
            {
                for (int i = 0; i < _account_data.Count; i++)
                {
                    if (_account_data[i].pcs == pcs)
                        return i;
                }
                return -1;
            }
        }
        public bool GetAccountEnabled(int id)
        {
            lock (_account_data_external_lock)
            {
                if (id < 0 || id >= _account_data.Count) throw new ArgumentOutOfRangeException("id");
                return _account_data[id].enabled;
            }
        }
        public void SetAccountEnabled(int id, bool enabled)
        {
            lock (_account_data_external_lock)
            {
                if (id < 0 || id >= _account_data.Count) throw new ArgumentOutOfRangeException("id");
                var data = _account_data[id];
                data.enabled = enabled;
                _account_data[id] = data;
                _account_changed = true;
            }
        }
        public void SetAccountData(int id, BaiduPCS pcs)
        {
            lock (_account_data_external_lock)
            {
                if (id < 0 || id >= _account_data.Count) throw new ArgumentOutOfRangeException("id");
                var data = _account_data[id];
                data.pcs = pcs;
                _account_data[id] = data;
                _account_changed = true;
            }
        }
        #endregion
        #region public functions for accounts
        public void GetFileListAsync(string path, BaiduPCS.MultiObjectMetaCallback callback)
        {

        }
        public void CreateDirectoryAsync(string path, BaiduPCS.ObjectMetaCallback callback)
        {

        }

        public void MovePathAsync(string source, string destination, BaiduPCS.OperationCallback callback)
        {

        }
        public void MovePathAsync(IEnumerable<string> source, IEnumerable<string> destination, BaiduPCS.OperationCallback callback)
        {

        }
        public void CopyPathAsync(string source, string destination, BaiduPCS.OperationCallback callback)
        {

        }
        public void CopyPathAsync(IEnumerable<string> source, IEnumerable<string> destination, BaiduPCS.OperationCallback callback)
        {

        }
        public void RenameAsync(string source, string destination, BaiduPCS.OperationCallback callback)
        {

        }
        public void RenameAsync(IEnumerable<string> source, IEnumerable<string> destination, BaiduPCS.OperationCallback callback)
        {

        }
        public void DeletePathAsync(string path, BaiduPCS.OperationCallback callback)
        {

        }
        public void DeletePathAsync(IEnumerable<string> path, BaiduPCS.OperationCallback callback)
        {

        }

        #endregion
    }
}
