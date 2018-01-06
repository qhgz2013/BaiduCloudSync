using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Text;

namespace BaiduCloudSync
{
    /// <summary>
    /// 文件同步跟踪器，用于跟踪本地文件与网盘文件的更改情况
    /// </summary>
    public class SyncTracker : IDisposable
    {
        private const string _HIDDEN_TRACKER_DIR_NAME = ".sync";
        private const string _HIDDEN_TRACKER_FILE_NAME = "track.db";

        private RemoteFileCacher _remote_cacher;
        private LocalFileCacher _local_cacher;
        private string _remote_path;
        private string _local_path;

        private SQLiteConnection _sql_con;
        private SQLiteCommand _sql_cmd;
        private SQLiteTransaction _sql_trs;
        private object _sql_lck;

        public SyncTracker(RemoteFileCacher remote_cacher, LocalFileCacher local_cacher, string remote_path, string local_path)
        {
            if (remote_cacher == null) throw new ArgumentNullException("remote_cacher");
            if (local_cacher == null) throw new ArgumentNullException("local_cacher");
            if (string.IsNullOrEmpty(remote_path)) throw new ArgumentNullException("remote_path");
            if (string.IsNullOrEmpty(local_path)) throw new ArgumentNullException("local_path");

            if (!File.Exists(local_path) && !Directory.Exists(local_path))
            {
                throw new ArgumentException("local path does not exists, check your path!");
            }

            _remote_cacher = remote_cacher;
            _local_cacher = local_cacher;
            _remote_path = remote_path;
            _local_path = local_path;

            var hidden_path = Path.Combine(_local_path, _HIDDEN_TRACKER_DIR_NAME);
            var dir_info = new DirectoryInfo(hidden_path);
            if (!dir_info.Exists)
            {
                dir_info.Create();
                dir_info.Attributes |= FileAttributes.Hidden;
            }
            var sql_path = Path.Combine(hidden_path, _HIDDEN_TRACKER_FILE_NAME);
            if (!File.Exists(sql_path)) File.Create(sql_path).Dispose();

            _sql_lck = new object();
            _initialize_sql_tables();
        }
        private void _initialize_sql_tables()
        {
            _sql_con = new SQLiteConnection("Data Source='" + _HIDDEN_TRACKER_DIR_NAME + "/" + _HIDDEN_TRACKER_FILE_NAME + "'; Version=3;");
            _sql_con.Open();
            _sql_cmd = new SQLiteCommand(_sql_con);
            _sql_trs = _sql_con.BeginTransaction();

            var query_table_count = "select count(*) from sqlite_master where type = 'table'";
            _sql_cmd.CommandText = query_table_count;
            var count = Convert.ToInt32(_sql_cmd.ExecuteScalar());
            var create_local_file_table = "create table LocalFiles (" +
                "Path varchar(300) not null," +
                "Path_SHA1 binary(20) primary key," +
                "CRC32 binary(4) not null," +
                "MD5 binary(16) not null," +
                "SHA1 binary(20) not null," +
                "Size bigint not null)";
            var create_remote_file_table = "create table RemoteFiles (" +
                "Path varchar(300) not null unique,"+
                "FS_ID bigint primary key,"+
                "MD5 binary(16) not null,"+
                "Size bigint not null)";
            var create_mapping_table = "create table FileMapping (" +
                "Src_Path_SHA1 binary(20) primary key,"+
                "Dst_FS_ID bigint not null)";
            var create_dbvar_table = "create table DbVars (Key varchar(512) primary key, Value varchar(65536))";
            var insert_version = "insert into DbVars(Key, Value) values ('Version', '1.0.0')";

            if (count == 0)
            {
                _sql_cmd.CommandText = create_local_file_table;
                _sql_cmd.ExecuteNonQuery();
                _sql_cmd.CommandText = create_mapping_table;
                _sql_cmd.ExecuteNonQuery();
                _sql_cmd.CommandText = create_remote_file_table;
                _sql_cmd.ExecuteNonQuery();
                _sql_cmd.CommandText = create_dbvar_table;
                _sql_cmd.ExecuteNonQuery();
                _sql_cmd.CommandText = insert_version;
                _sql_cmd.ExecuteNonQuery();

                _sql_trs.Commit();
                _sql_trs = _sql_con.BeginTransaction();
            }
        }
        public void Dispose()
        {
            throw new NotImplementedException();
        }
    }
}
