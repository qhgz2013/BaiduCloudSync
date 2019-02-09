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
                    Parent.StateAdapter = new StartedStateAdapter(Parent);
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
                    Parent.TaskExecutor.OnStartRequested();
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
            ExecutionThread.IsBackground = true;
            ExecutionThread.Name = "Start Interrupt Thread for Task: " + Parent.Name;
            ExecutionThread.Start();
        }
        private readonly ManualResetEventSlim _thread_exited_event = new ManualResetEventSlim();

        public override TaskState State => TaskState.StartRequested;

        public Thread ExecutionThread { get; private set; }
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
