using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BaiduCloudSync.task;
using System.Threading;

namespace BaiduCloudSync_Test.task
{
    class TaskImpl : Task
    {
        private int _sleep_time;
        private bool _throw_on_cancel, _throw_on_pause, _throw_on_start;
        private ManualResetEventSlim _start_event = new ManualResetEventSlim();
        private ManualResetEventSlim _pause_event = new ManualResetEventSlim();
        private ManualResetEventSlim _cancel_event = new ManualResetEventSlim();
        public TaskImpl(int sleep_time, bool throw_on_cancel = false, bool throw_on_pause = false, bool throw_on_start = false)
        {
            _sleep_time = sleep_time;
            _throw_on_cancel = throw_on_cancel;
            _throw_on_pause = throw_on_pause;
            _throw_on_start = throw_on_start;
        }
        protected override void _cancel_internal(TaskState previous_state)
        {
            CancelTriggeredTimes++;
            _cancel_event.Set();
            if (_throw_on_cancel)
                throw new Exception("This is a test exception on cancel");
        }

        protected override void _pause_internal(TaskState previous_state)
        {
            PauseTriggeredTimes++;
            _pause_event.Set();
            if (_throw_on_pause)
                throw new Exception("This is a test exception on pause");
        }

        protected override void _start_internal(TaskState previous_state)
        {
            StartTriggeredTimes++;
            if (_throw_on_start)
                throw new Exception("This is a test exception on start");
            ThreadPool.QueueUserWorkItem(delegate
            {
                _start_event.Set();
                Thread.Sleep(_sleep_time);
                try
                {
                    _set_state_finished();
                }
                catch (InvalidTaskStateException) { }
            });
        }

        public int PauseTriggeredTimes { get; private set; }
        public int CancelTriggeredTimes { get; private set; }
        public int StartTriggeredTimes { get; private set; }

        public void WaitStart() { _start_event.Wait(1000); }
        public void WaitPause() { _pause_event.Wait(1000); }
        public void WaitCancel() { _cancel_event.Wait(1000); }
    }
}
