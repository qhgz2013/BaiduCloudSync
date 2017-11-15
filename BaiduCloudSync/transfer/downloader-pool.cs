using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace BaiduCloudSync
{
    /// <summary>
    /// 下载的任务池
    /// </summary>
    public class DownloaderPool : IDisposable
    {
        //默认并行下载的任务数
        private const int _DEFAULT_POOL_SIZE = 5;
        //外部的同步锁
        private object _external_lock;
        //下载队列
        private Dictionary<int, Downloader> _queue_data;
        //并行任务数
        private int _pool_size;

        //API的封装类
        private RemoteFileCacher _cacher;
        //对该任务池的总下载速度限制
        private int _speed_limit;
        //每个任务的最大下载线程数
        private int _max_thread;
        //是否在下载完成后自动开始新任务的标识，由函数控制
        private bool _auto_start;
        //分配的任务id
        private int _allocated_index;
        public DownloaderPool(RemoteFileCacher cacher)
        {
            if (cacher == null) throw new ArgumentNullException("cacher");
            _cacher = cacher;
            _queue_data = new Dictionary<int, Downloader>();
            _external_lock = new object();
            _pool_size = _DEFAULT_POOL_SIZE;
            _max_thread = Downloader.DEFAULT_MAX_THREAD;
            _auto_start = false;
            _allocated_index = 0;

        }
        ~DownloaderPool()
        {
            Dispose();
        }

        #region public properties
        /// <summary>
        /// 并行任务数
        /// </summary>
        public int PoolSize { get { return _pool_size; } set { if (value <= 0) throw new ArgumentOutOfRangeException("value"); lock (_external_lock) _pool_size = value; } }
        /// <summary>
        /// 总下载速度，单位：B/s
        /// </summary>
        public int SpeedLimit { get { return _speed_limit; } set { lock (_external_lock) { _speed_limit = value; _set_speed(); } } }
        /// <summary>
        /// 每个任务的最大线程数
        /// </summary>
        public int MaxThread { get { return _max_thread; } set { lock (_external_lock) _max_thread = value; } }
        //routed events
        public event EventHandler TaskStarted, TaskPaused, TaskCancelled, TaskError, TaskFinished;
        #endregion

        private void _set_speed()
        {
            for (int i = 0; i < _queue_data.Count; i++)
            {
                _queue_data[i].SpeedLimit = _speed_limit / _pool_size;
            }
        }

        //event callback
        #region event callback
        private void _on_task_started(object sender, EventArgs e)
        {
            try { TaskStarted?.Invoke(sender, e); }
            catch { }
        }
        private void _on_task_paused(object sender, EventArgs e)
        {
            try { TaskPaused?.Invoke(sender, e); }
            catch { }
        }
        private void _on_task_cancelled(object sender, EventArgs e)
        {
            try { TaskCancelled?.Invoke(sender, e); }
            catch { }
        }
        private void _on_task_error(object sender, EventArgs e)
        {
            try { TaskError?.Invoke(sender, e); }
            catch { }
        }
        private void _on_task_finished(object sender, EventArgs e)
        {
            lock (_external_lock)
            {
                if (_auto_start && _queue_data.Count > _pool_size)
                {
                    _queue_data.ElementAt(_pool_size).Value.Start();
                }
                _queue_data.Remove((int)((Downloader)sender).Tag);
            }
            try { TaskFinished?.Invoke(sender, e); }
            catch { }
        }
        #endregion

        /// <summary>
        /// 将下载任务添加到下载队列中，返回该任务的队列ID
        /// </summary>
        /// <param name="data">网盘数据（要求字段Size和Path不能为空）</param>
        /// <param name="path">本地文件路径（父文件夹要求已创建）</param>
        /// <returns></returns>
        public int QueueTask(ObjectMetadata data, string path)
        {
            lock (_external_lock)
            {
                var downloader = new Downloader(_cacher, data, path, _max_thread, _speed_limit / _pool_size);
                var index = _allocated_index++;
                downloader.Tag = index;
                downloader.TaskStarted += _on_task_started;
                downloader.TaskPaused += _on_task_paused;
                downloader.TaskFinished += _on_task_finished;
                downloader.TaskError += _on_task_error;
                downloader.TaskCancelled += _on_task_cancelled;
                _queue_data.Add(index, downloader);
                if (_auto_start && _queue_data.Count <= _pool_size)
                    downloader.Start();
                return index;
            }
        }

        public void Dispose()
        {
            lock (_external_lock)
            {
                foreach (var item in _queue_data)
                {
                    item.Value.Cancel();
                    item.Value.Dispose();
                }
                _queue_data = null;
            }
        }
        /// <summary>
        /// 开始所有任务
        /// </summary>
        public void Start()
        {
            lock (_external_lock)
            {
                _auto_start = true;
                for (int i = 0; i < _pool_size && i < _queue_data.Count; i++)
                {
                    _queue_data[i].Start();
                }
            }
        }
        /// <summary>
        /// 开始指定任务
        /// </summary>
        /// <param name="id">分配的下载id</param>
        public void Start(int id)
        {
            lock (_external_lock)
            {
                if (!_queue_data.ContainsKey(id)) return;
                _queue_data[id].Start();
            }
        }
        /// <summary>
        /// 暂停所有任务
        /// </summary>
        public void Pause()
        {
            lock (_external_lock)
            {
                _auto_start = false;
                for (int i = 0; i < _queue_data.Count; i++)
                {
                    _queue_data[i].Pause();
                }
            }
        }
        /// <summary>
        /// 暂停指定任务
        /// </summary>
        /// <param name="id">分配的下载id</param>
        public void Pause(int id)
        {
            lock (_external_lock)
            {
                if (!_queue_data.ContainsKey(id)) return;
                _queue_data[id].Pause();
            }
        }
        /// <summary>
        /// 取消所有任务
        /// </summary>
        public void Cancel()
        {
            lock (_external_lock)
            {
                _auto_start = false;
                for (int i = 0; i < _queue_data.Count; i++)
                {
                    _queue_data[i].Cancel();
                }
                _queue_data.Clear();
            }
        }
        /// <summary>
        /// 取消指定任务
        /// </summary>
        /// <param name="id">分配的下载id</param>
        public void Cancel(int id)
        {
            lock (_external_lock)
            {
                if (!_queue_data.ContainsKey(id)) return;
                _queue_data[id].Cancel();
                _queue_data.Remove(id);
            }
        }
    }
}
