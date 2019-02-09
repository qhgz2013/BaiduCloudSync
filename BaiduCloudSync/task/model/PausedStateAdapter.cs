using GlobalUtil;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace BaiduCloudSync.task.model
{
    internal sealed class PausedStateAdapter : StateAdapter
    {
        private readonly ManualResetEventSlim _interrupt_thread_exited_event;
        public PausedStateAdapter(Task parent, ManualResetEventSlim interrupt_thread_exited_event) : base(parent)
        {
            _interrupt_thread_exited_event = interrupt_thread_exited_event;
        }
        public override TaskState State => TaskState.Paused;

        public override void Cancel()
        {
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
            Parent.StateAdapter = new StartRequestedStateAdapter(Parent);
        }

        public override bool Wait(int timeout)
        {
            return _interrupt_thread_exited_event.Wait(timeout);
        }
    }
}
