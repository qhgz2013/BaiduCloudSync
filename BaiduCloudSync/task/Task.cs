using GlobalUtil;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace BaiduCloudSync.task
{
    public abstract class Task : ITask
    {
        private object _external_lock = new object();
        private volatile TaskState _state;
        private ManualResetEventSlim _lock;
        /// <summary>
        /// 内部的异步Start回调函数，在任务完成后调用_set_state_finished()将状态置为Finished的终止状态
        /// </summary>
        /// <param name="previous_state">上一个任务状态，Ready或Paused</param>
        protected abstract void _start_internal(TaskState previous_state);
        /// <summary>
        /// 内部的异步Pause回调函数
        /// </summary>
        /// <param name="previous_state">上一个任务状态，只为Started</param>
        protected abstract void _pause_internal(TaskState previous_state);
        /// <summary>
        /// 内部的异步Cancel回调函数
        /// </summary>
        /// <param name="previous_state">上一个任务状态，Started或Paused</param>
        protected abstract void _cancel_internal(TaskState previous_state);

        public string Name { get; set; }
        public TaskState State { get { return _state; } }
        protected void _set_state_finished()
        {
            if (_state == TaskState.Started) _state = TaskState.Finished; else throw new InvalidTaskStateException();
            //Tracer.GlobalTracer.TraceInfo("[State] -> " + _state);
        }
        public Task()
        {
            // alternative arch: change this FSM model into separable class
            _state = TaskState.Ready;
            //Tracer.GlobalTracer.TraceInfo("[State] -> " + _state);
            _lock = new ManualResetEventSlim();
        }
        public void Cancel()
        {
        }

        public void Pause()
        {
        }

        public void Start()
        {
        }
    }
}
