using GlobalUtil;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BaiduCloudSync.task.model
{
    internal sealed class ReadyStateAdapter : StateAdapter
    {
        public ReadyStateAdapter(Task parent) : base(parent) { }
        public override TaskState State => TaskState.Ready;

        public override void Cancel()
        {
            Parent.StateAdapter = new CancelRequestedStateAdapter(Parent);
        }

        public override void Pause()
        {
            Parent.StateAdapter = new PauseRequestedStateAdapter(Parent);
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
            return true;
        }
    }
}
