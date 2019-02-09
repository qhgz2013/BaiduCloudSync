using GlobalUtil;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BaiduCloudSync.task.model
{
    internal sealed class FinishedStateAdapter : StateAdapter
    {
        public FinishedStateAdapter(Task parent) : base(parent) { }
        public override TaskState State => TaskState.Finished;

        public override void Cancel()
        {
        }

        public override void Pause()
        {
        }

        public override void Retry()
        {
        }

        public override void Start()
        {
        }

        public override bool Wait(int timeout)
        {
            return true;
        }
    }
}
