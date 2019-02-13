using GlobalUtil;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace BaiduCloudSync.task.model
{
    internal sealed class FailedStateAdapter : StateAdapter
    {
        public FailedStateAdapter(Task parent) : base(parent)
        {
        }
        public override TaskState State => TaskState.Failed;

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
            StateAdapterHelper.SetTaskState(TaskState.RetryRequested, Parent);
        }

        public override void Start()
        {
            throw new InvalidTaskStateException();
        }

        public override bool Wait(int timeout)
        {
            return true;
        }
    }
}
