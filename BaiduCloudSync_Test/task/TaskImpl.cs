using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BaiduCloudSync.task;
using System.Threading;
using GlobalUtil;

namespace BaiduCloudSync_Test.task
{
    class TaskImpl1 : ITaskExecutor
    {
        public event EventHandler EmitResponse;
#pragma warning disable CS0067
        public event EventHandler EmitFailure;
#pragma warning restore
        public int TriggerCancel = 0, TriggerPause = 0, TriggerStarted = 0;
        public bool PauseFlag = false, CancelFlag = false;
        private readonly ManualResetEventSlim _interrupt_event = new ManualResetEventSlim();
        public void OnCancelRequested()
        {
            Tracer.GlobalTracer.TraceInfo("hello?");
            TriggerCancel++;
            CancelFlag = true;
            _interrupt_event.Set();
            EmitResponse(this, new EventArgs());
        }

        public void OnPauseRequested()
        {
            Tracer.GlobalTracer.TraceInfo("hello?");
            TriggerPause++;
            PauseFlag = true;
            _interrupt_event.Set();
            EmitResponse(this, new EventArgs());
        }

        public void OnRetryRequested()
        {
        }

        public void OnStartRequested()
        {
            Tracer.GlobalTracer.TraceInfo("hello?");
            TriggerStarted++;
            _interrupt_event.Reset();
            PauseFlag = false;
            CancelFlag = false;
            EmitResponse(this, new EventArgs());
        }

        public void Run()
        {
            Tracer.GlobalTracer.TraceInfo("hello?");
            if (!_interrupt_event.Wait(10000))
                EmitResponse(this, new EventArgs());
        }
    }
    class TaskImpl2 : ITaskExecutor
    {
        public event EventHandler EmitResponse;
#pragma warning disable CS0067
        public event EventHandler EmitFailure;
#pragma warning restore
        private readonly ManualResetEventSlim _interrupt_event = new ManualResetEventSlim();
        public bool ThrowOnCancel = false, ThrowOnPause = false, ThrowOnStart = false, ThrowOnRun = false, ThrowOnRetry = false;
        public void OnCancelRequested()
        {
            if (ThrowOnCancel)
                throw new Exception();
            _interrupt_event.Set();
            EmitResponse(this, new EventArgs());
        }

        public void OnPauseRequested()
        {
            if (ThrowOnPause)
                throw new Exception();
            _interrupt_event.Set();
            EmitResponse(this, new EventArgs());
        }

        public void OnRetryRequested()
        {
            if (ThrowOnRetry)
                throw new Exception();
            EmitResponse(this, new EventArgs());
        }

        public void OnStartRequested()
        {
            if (ThrowOnStart)
                throw new Exception();
            _interrupt_event.Reset();
            EmitResponse(this, new EventArgs());
        }

        public void Run()
        {
            if (ThrowOnRun)
                throw new Exception();
            if (!_interrupt_event.Wait(10000))
                EmitResponse(this, new EventArgs());
        }
    }
}
