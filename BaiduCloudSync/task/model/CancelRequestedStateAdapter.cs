using GlobalUtil;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace BaiduCloudSync.task.model
{
    internal sealed class CancelRequestedStateAdapter : StateAdapter
    {
        private readonly ManualResetEventSlim _thread_exited_event = new ManualResetEventSlim();
        public Thread ExecutionThread { get; private set; }
        private readonly EventHandler _response;
        private readonly EventHandler _failure;
        public CancelRequestedStateAdapter(Task parent) : base(parent)
        {
            _response = new EventHandler((sender, e) =>
            {
                if (Parent.State == TaskState.CancelRequested)
                {
                    StateAdapterHelper.SetTaskState(TaskState.Cancelled, Parent, _thread_exited_event);
                    Parent.TaskExecutor.EmitResponse -= _response;
                    _thread_exited_event.Set();
                }
            });
            _failure = new EventHandler((sender, e) =>
            {
                StateAdapterHelper.SetTaskState(TaskState.Failed, Parent);
                _thread_exited_event.Set();
                Parent.TaskExecutor.EmitResponse -= _response;
                Parent.TaskExecutor.EmitFailure -= _failure;
            });
            ExecutionThread = new Thread(new ThreadStart(delegate
            {
                Parent.TaskExecutor.EmitResponse += _response;
                Parent.TaskExecutor.EmitFailure += _failure;
                try
                {
                    Parent.TaskExecutor.OnCancelRequested();
                }
                catch (Exception ex)
                {
                    Tracer.GlobalTracer.TraceError("Unexpected exception while cancelling task");
                    Tracer.GlobalTracer.TraceError(ex);
                    Parent.TaskExecutor.EmitResponse -= _response;
                    _thread_exited_event.Set();
                    StateAdapterHelper.SetTaskState(TaskState.Failed, Parent);
                }
                finally
                {
                    Parent.TaskExecutor.EmitFailure -= _failure;
                }
            }));
            ExecutionThread.IsBackground = false;
            ExecutionThread.Name = "Cancel Interrupt Thread for Task: " + Parent.Name;
            ExecutionThread.Start();
        }

        public override TaskState State => TaskState.CancelRequested;

        public override void Cancel()
        {
        }

        public override void Pause()
        {
            throw new InvalidTaskStateException();
        }

        public override void Retry()
        {
            Wait(-1);
            StateAdapterHelper.SetTaskState(TaskState.RetryRequested, Parent);
        }

        public override void Start()
        {
            throw new InvalidTaskStateException();
        }
        public override bool Wait(int timeout)
        {
            return _thread_exited_event.Wait(timeout);
        }
    }
}
