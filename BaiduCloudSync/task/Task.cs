using GlobalUtil;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using BaiduCloudSync.task.model;

namespace BaiduCloudSync.task
{
    public sealed class Task : ITaskOperator
    {
        private volatile StateAdapter _state_adapter;
        // global writer lock of _state_adapter
        private readonly object _lock = new object();

        private string _name;

        #region Properties
        /// <summary>
        /// 任务的名称
        /// </summary>
        public string Name
        {
            get => string.IsNullOrEmpty(_name) ? $"Noname_{ID}" : _name;
            set => _name = value;
        }

        /// <summary>
        /// 任务的状态
        /// </summary>
        public TaskState State => StateAdapter.State;
        /// <summary>
        /// 任务是否为背景任务，与Thread.IsBackground属性一致
        /// </summary>
        public bool IsBackground { get; set; }
        /// <summary>
        /// 获取任务执行逻辑接口
        /// </summary>
        public ITaskExecutor TaskExecutor { get; }
        /// <summary>
        /// 获取或设置相应状态下的操作处理逻辑，由BaiduCloudSync.task.model提供
        /// </summary>
        internal StateAdapter StateAdapter
        {
            get => _state_adapter;
            set
            {
                TaskState origin_state = TaskState.Ready, new_state;
                lock (_lock)
                {
                    if (_state_adapter != null)
                        origin_state = _state_adapter.State;
                    _state_adapter = value;
                    new_state = value.State;
                }
                if (origin_state != new_state)
                    StateChanged?.Invoke(this, new TaskStateChangedEventArgs(new_state, origin_state));
            }
        }
        #endregion
        public Task(ITaskExecutor task_executor)
        {
            ID = Interlocked.Increment(ref _global_id);
            TaskExecutor = task_executor;
            StateAdapter = new ReadyStateAdapter(this);
            IsBackground = true;
        }

        public void Start()
        {
            ((ITaskOperator)StateAdapter).Start();
        }

        public void Pause()
        {
            ((ITaskOperator)StateAdapter).Pause();
        }

        public void Cancel()
        {
            ((ITaskOperator)StateAdapter).Cancel();
        }

        public void Retry()
        {
            ((ITaskOperator)StateAdapter).Retry();
        }

        public bool Wait(int timeout = -1)
        {
            StateAdapter adapter = StateAdapter;
            DateTime dst_time = DateTime.Now;
            if (timeout > 0)
                dst_time += TimeSpan.FromMilliseconds(timeout);
            //todo: modify this condition to the property in StateAdapter.IsInStableState
            while (adapter.State == TaskState.Started || adapter.State == TaskState.StartRequested ||
                adapter.State == TaskState.PauseRequested || adapter.State == TaskState.CancelRequested ||
                adapter.State == TaskState.RetryRequested)
            {
                if (timeout < 0)
                    adapter.Wait(timeout);
                else
                {
                    var dt = (int)(dst_time - DateTime.Now).TotalMilliseconds;
                    if (dt <= 0)
                        return false;
                    if (!adapter.Wait(dt))
                        return false;
                }
                adapter = StateAdapter;
            }
            return true;
        }

        public event EventHandler<TaskStateChangedEventArgs> StateChanged;

        private static long _global_id;
        public long ID { get; private set; }
        public override string ToString()
        {
            return $"Task #{ID} ({State}): {Name}";
        }
    }
}
