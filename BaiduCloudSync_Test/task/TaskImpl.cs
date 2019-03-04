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
#pragma warning disable CS0067
        public event EventHandler EmitFailure;
        public event EventHandler EmitStartResponse;
        public event EventHandler EmitPauseResponse;
        public event EventHandler EmitCancelResponse;
        public event EventHandler EmitRetryResponse;
        public event EventHandler EmitFinished;
#pragma warning restore
        public int TriggerCancel = 0, TriggerPause = 0, TriggerStarted = 0, TriggerRetry = 0;
        public bool PauseFlag = false, CancelFlag = false;
        private readonly ManualResetEventSlim _interrupt_event = new ManualResetEventSlim();
        public bool EmitFailureInsteadResponse = false;
        public void OnCancelRequested()
        {
            Tracer.GlobalTracer.TraceInfo("hello?");
            TriggerCancel++;
            CancelFlag = true;
            _interrupt_event.Set();
            if (EmitFailureInsteadResponse)
                EmitFailure(this, new EventArgs());
            else
                EmitCancelResponse(this, new EventArgs());
        }

        public void OnPauseRequested()
        {
            Tracer.GlobalTracer.TraceInfo("hello?");
            TriggerPause++;
            PauseFlag = true;
            _interrupt_event.Set();
            if (EmitFailureInsteadResponse)
                EmitFailure(this, new EventArgs());
            else
                EmitPauseResponse(this, new EventArgs());
        }

        public void OnRetryRequested()
        {
            Tracer.GlobalTracer.TraceInfo("hello?");
            if (EmitFailureInsteadResponse)
                EmitFailure(this, new EventArgs());
            else
                EmitRetryResponse(this, new EventArgs());
        }

        public void OnStartRequested()
        {
            Tracer.GlobalTracer.TraceInfo("hello?");
            TriggerStarted++;
            _interrupt_event.Reset();
            PauseFlag = false;
            CancelFlag = false;
            if (EmitFailureInsteadResponse)
                EmitFailure(this, new EventArgs());
            else
                EmitStartResponse(this, new EventArgs());
        }

        public void Run()
        {
            Tracer.GlobalTracer.TraceInfo("hello?");
            if (!_interrupt_event.Wait(10000))
            {
                if (EmitFailureInsteadResponse)
                    EmitFailure(this, new EventArgs());
                else
                    EmitFinished(this, new EventArgs());
            }
        }
    }
    class TaskImpl2 : ITaskExecutor
    {
#pragma warning disable CS0067
        public event EventHandler EmitFailure;
        public event EventHandler EmitStartResponse;
        public event EventHandler EmitPauseResponse;
        public event EventHandler EmitCancelResponse;
        public event EventHandler EmitRetryResponse;
        public event EventHandler EmitFinished;
#pragma warning restore
        private readonly ManualResetEventSlim _interrupt_event = new ManualResetEventSlim();
        public bool ThrowOnCancel = false, ThrowOnPause = false, ThrowOnStart = false, ThrowOnRun = false, ThrowOnRetry = false;
        public void OnCancelRequested()
        {
            Tracer.GlobalTracer.TraceInfo("hello?");
            if (ThrowOnCancel)
                throw new Exception();
            Thread.Sleep(1000);
            _interrupt_event.Set();
            EmitCancelResponse(this, new EventArgs());
        }

        public void OnPauseRequested()
        {
            Tracer.GlobalTracer.TraceInfo("hello?");
            if (ThrowOnPause)
                throw new Exception();
            Thread.Sleep(1000);
            _interrupt_event.Set();
            EmitPauseResponse(this, new EventArgs());
        }

        public void OnRetryRequested()
        {
            Tracer.GlobalTracer.TraceInfo("hello?");
            if (ThrowOnRetry)
                throw new Exception();
            Thread.Sleep(1000);
            _interrupt_event.Set();
            EmitRetryResponse(this, new EventArgs());
        }

        public void OnStartRequested()
        {
            Tracer.GlobalTracer.TraceInfo("hello?");
            if (ThrowOnStart)
                throw new Exception();
            Thread.Sleep(1000);
            _interrupt_event.Reset();
            EmitStartResponse(this, new EventArgs());
        }

        public void Run()
        {
            Tracer.GlobalTracer.TraceInfo("hello?");
            if (ThrowOnRun)
                throw new Exception();
            if (!_interrupt_event.Wait(10000))
                EmitFinished(this, new EventArgs());
        }
    }
}
