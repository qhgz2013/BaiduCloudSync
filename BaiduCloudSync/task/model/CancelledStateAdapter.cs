using GlobalUtil;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace BaiduCloudSync.task.model
{
    internal sealed class CancelledStateAdapter : StateAdapter
    {
        private readonly ManualResetEventSlim _interrupt_thread_exited_event;
        public CancelledStateAdapter(Task parent, ManualResetEventSlim interrupt_thread_exited_event) : base(parent)
        {
            _interrupt_thread_exited_event = interrupt_thread_exited_event;
        }

        public override TaskState State => TaskState.Cancelled;

        public override void Cancel()
        {
        }

        public override void Pause()
        {
            throw new InvalidTaskStateException();
        }

        public override void Retry()
        {
            Parent.StateAdapter = new RetryRequestedStateAdapter(Parent);
        }

        public override void Start()
        {
            throw new InvalidTaskStateException();
        }

        public override bool Wait(int timeout)
        {
            return _interrupt_thread_exited_event.Wait(timeout);
        }
    }
}
