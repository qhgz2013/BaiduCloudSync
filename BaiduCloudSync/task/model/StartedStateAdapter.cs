using GlobalUtil;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace BaiduCloudSync.task.model
{
    internal sealed class StartedStateAdapter : StateAdapter
    {
        private readonly ManualResetEventSlim _thread_exited_event = new ManualResetEventSlim();
        public Thread ExecutionThread { get; private set; }
        private readonly EventHandler _response;
        private readonly EventHandler _failure;
        public StartedStateAdapter(Task parent) : base(parent)
        {
            _response = new EventHandler((sender, e) =>
            {
                if (Parent.State == TaskState.Started)
                {
                    StateAdapterHelper.SetTaskState(TaskState.Finished, Parent);
                    Parent.TaskExecutor.EmitFinished -= _response;
                    _thread_exited_event.Set();
                }
            });
            _failure = new EventHandler((sender, e) =>
            {
                StateAdapterHelper.SetTaskState(TaskState.Failed, Parent);
                _thread_exited_event.Set();
                Parent.TaskExecutor.EmitFailure -= _failure;
                Parent.TaskExecutor.EmitFinished -= _response;
            });
            ExecutionThread = new Thread(new ThreadStart(delegate
            {
                Parent.TaskExecutor.EmitFinished += _response;
                Parent.TaskExecutor.EmitFailure += _failure;
                try
                {
                    Parent.TaskExecutor.Run();
                }
                catch (Exception ex)
                {
                    Tracer.GlobalTracer.TraceError("Unexpected exception while executing task");
                    Tracer.GlobalTracer.TraceError(ex);
                    Parent.TaskExecutor.EmitFinished -= _response;
                    _thread_exited_event.Set();
                    StateAdapterHelper.SetTaskState(TaskState.Failed, Parent);
                }
                finally
                {
                    Parent.TaskExecutor.EmitFailure -= _failure;
                    if (Parent.State != TaskState.Started)
                    {
                        _thread_exited_event.Set();
                        Parent.TaskExecutor.EmitFinished -= _response;
                    }
                }
            }));
            ExecutionThread.IsBackground = Parent.IsBackground;
            ExecutionThread.Name = "Execution Thread for Task: " + Parent.Name;
            ExecutionThread.Start();
        }

        public override TaskState State => TaskState.Started;

        public override void Cancel()
        {
            StateAdapterHelper.SetTaskState(TaskState.CancelRequested, Parent);
        }

        public override void Pause()
        {
            StateAdapterHelper.SetTaskState(TaskState.PauseRequested, Parent);
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
