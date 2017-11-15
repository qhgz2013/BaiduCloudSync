using GlobalUtil.NetUtils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace BaiduCloudSync
{
    public class Uploader
    {
        //默认并行4线程上传
        public const int DEFAULT_THREAD_SIZE = 4;
        private bool _enable_slice_upload = true;

        private LocalFileCacher _local_cacher;
        private RemoteFileCacher _remote_cacher;
        private int _selected_account_id;

        private string _remote_path;
        private string _local_path;
        private long _file_size;

        private NetStream[] _request;
        private FileStream _local_stream;
        private int _max_thread;

        private LocalFileData _local_data;
        private ObjectMetadata _remote_data;

        private DateTime _start_time;

        private double _average_speed_total;
        private double _average_speed_5s;
        private LinkedList<long> _upload_size_5s;
        private double _uploaded_size;

        private volatile int _upload_thread_flag;

        private Thread _monitor_thread;
        private ManualResetEventSlim _monitor_thread_created;

        private object _external_lock;

        //upload data
        private int _slice_count;
        private List<int> _slice_queue;
        private string _upload_id;

    }
}
