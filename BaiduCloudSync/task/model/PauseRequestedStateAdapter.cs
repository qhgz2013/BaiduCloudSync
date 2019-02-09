﻿using GlobalUtil;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace BaiduCloudSync.task.model
{
    internal sealed class PauseRequestedStateAdapter : StateAdapter
    {
        private readonly ManualResetEventSlim _thread_exited_event = new ManualResetEventSlim();
        public Thread ExecutionThread { get; private set; }
        private readonly EventHandler _response;
        private readonly EventHandler _failure;
        public PauseRequestedStateAdapter(Task parent) : base(parent)
        {
            _response = new EventHandler((sender, e) =>
            {
                if (Parent.State == TaskState.PauseRequested)
                {
                    Parent.StateAdapter = new PausedStateAdapter(Parent, _thread_exited_event);
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
                    Parent.TaskExecutor.OnPauseRequested();
                }
                catch (Exception ex)
                {
                    Tracer.GlobalTracer.TraceError("Unexpected exception while pausing task");
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
            ExecutionThread.Name = "Pause Interrupt Thread for Task: " + Parent.Name;
            ExecutionThread.Start();
        }

        public override TaskState State => TaskState.PauseRequested;

        public override void Cancel()
        {
            Wait(-1);
            Parent.StateAdapter = new CancelRequestedStateAdapter(Parent);
        }

        public override void Pause()
        {
        }

        public override void Retry()
        {
            throw new InvalidTaskStateException();
        }

        public override void Start()
        {
            Wait(-1);
            Parent.StateAdapter = new StartRequestedStateAdapter(Parent);
        }
        
        public override bool Wait(int timeout)
        {
            return _thread_exited_event.Wait(timeout);
        }
    }
}
