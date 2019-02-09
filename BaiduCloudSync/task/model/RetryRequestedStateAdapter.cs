﻿using GlobalUtil;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace BaiduCloudSync.task.model
{
    internal sealed class RetryRequestedStateAdapter : StateAdapter
    {
        private readonly ManualResetEventSlim _thread_exited_event = new ManualResetEventSlim();
        public Thread ExecutionThread { get; private set; }
        private readonly EventHandler _response;
        private readonly EventHandler _failure;
        public RetryRequestedStateAdapter(Task parent) : base(parent)
        {
            _response = new EventHandler((sender, e) =>
            {
                if (Parent.State == TaskState.RetryRequested)
                {
                    Parent.StateAdapter = new ReadyStateAdapter(Parent);
                    Parent.TaskExecutor.EmitResponse -= _response;
                    _thread_exited_event.Set();
                }
            });
            _failure = new EventHandler((sender, e) =>
            {
                _thread_exited_event.Set();
                Parent.TaskExecutor.EmitFailure -= _failure;
            });
            ExecutionThread = new Thread(new ThreadStart(delegate
            {
                Parent.TaskExecutor.EmitResponse += _response;
                Parent.TaskExecutor.EmitFailure += _failure;
                try
                {
                    Parent.TaskExecutor.OnRetryRequested();
                }
                catch (Exception ex)
                {
                    Tracer.GlobalTracer.TraceError("Unexpected exception while executing task");
                    Tracer.GlobalTracer.TraceError(ex);
                    _thread_exited_event.Set();
                    Parent.StateAdapter = new FailedStateAdapter(Parent);
                }
                finally
                {
                    Parent.TaskExecutor.EmitFailure -= _failure;
                }
            }));
            ExecutionThread.IsBackground = false;
            ExecutionThread.Name = "Retry Interrupt Thread for Task: " + Parent.Name;
            ExecutionThread.Start();
        }

        public override TaskState State => TaskState.RetryRequested;

        public override void Cancel()
        {
            Wait(-1);
            Parent.StateAdapter = new CancelRequestedStateAdapter(Parent);
        }

        public override void Pause()
        {
            Wait(-1);
            Parent.StateAdapter = new PauseRequestedStateAdapter(Parent);
        }

        public override void Retry()
        {
        }

        public override void Start()
        {
            Wait(-1);
            Parent.StateAdapter = new StartRequestedStateAdapter(Parent);
        }
        /// <summary>
        /// 等待重试事件被确认（EmitResponse），或触发了异常事件（EmitFailure），亦或是执行重试逻辑时产生了意外的异常
        /// </summary>
        /// <param name="timeout"></param>
        /// <returns></returns>
        public override bool Wait(int timeout)
        {
            return _thread_exited_event.Wait(timeout);
        }
    }
}
