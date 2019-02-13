using GlobalUtil;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace BaiduCloudSync.task.model
{
    internal sealed class StartRequestedStateAdapter : StateAdapter
    {
        private readonly EventHandler _response;
        private readonly EventHandler _failure;
        public StartRequestedStateAdapter(Task parent) : base(parent)
        {
            _response = new EventHandler((sender, e) =>
            {
                if (Parent.State == TaskState.StartRequested)
                {
                    StateAdapterHelper.SetTaskState(TaskState.Started, Parent);
                    Parent.TaskExecutor.EmitResponse -= _response;
                    _thread_exited_event.Set();
                }
            });
            _failure = new EventHandler((sender, e) =>
            {
                StateAdapterHelper.SetTaskState(TaskState.Failed, Parent);
                _thread_exited_event.Set();
                Parent.TaskExecutor.EmitFailure -= _failure;
                Parent.TaskExecutor.EmitResponse -= _response;
            });
            ExecutionThread = new Thread(new ThreadStart(delegate
            {
                Parent.TaskExecutor.EmitResponse += _response;
                Parent.TaskExecutor.EmitFailure += _failure;
                try
                {
                    Parent.TaskExecutor.OnStartRequested();
                }
                catch (Exception ex)
                {
                    Tracer.GlobalTracer.TraceError("Unexpected exception while executing task");
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
            ExecutionThread.IsBackground = true;
            ExecutionThread.Name = "Start Interrupt Thread for Task: " + Parent.Name;
            ExecutionThread.Start();
        }
        private readonly ManualResetEventSlim _thread_exited_event = new ManualResetEventSlim();

        public override TaskState State => TaskState.StartRequested;

        public Thread ExecutionThread { get; private set; }
        public override void Cancel()
        {
            throw new InvalidTaskStateException();
        }

        public override void Pause()
        {
            throw new InvalidTaskStateException();
        }

        public override void Retry()
        {
            throw new InvalidTaskStateException();
        }

        public override void Start()
        {
        }

        public override bool Wait(int timeout)
        {
            return _thread_exited_event.Wait(timeout);
        }
    }
}
