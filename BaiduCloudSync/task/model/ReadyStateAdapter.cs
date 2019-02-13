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
            StateAdapterHelper.SetTaskState(TaskState.StartRequested, Parent);
        }

        public override bool Wait(int timeout)
        {
            return true;
        }
    }
}
