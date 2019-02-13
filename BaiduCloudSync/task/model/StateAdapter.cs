using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BaiduCloudSync.task.model
{
    internal abstract class StateAdapter : ITaskOperator
    {
        public abstract void Cancel();
        public abstract void Pause();
        public abstract void Retry();
        public abstract void Start();
        public abstract bool Wait(int timeout);

        public abstract TaskState State { get; }

        protected Task Parent { get; private set; }

        protected StateAdapter(Task parent)
        {
            Parent = parent;
            Parent.StateAdapter = this;
        }
    }
}
