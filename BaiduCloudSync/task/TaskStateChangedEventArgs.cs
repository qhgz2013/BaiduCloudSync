using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BaiduCloudSync.task
{
    public sealed class TaskStateChangedEventArgs : EventArgs
    {
        public TaskState CurrentState { get; private set; }
        public TaskState PreviousState { get; private set; }
        public TaskStateChangedEventArgs(TaskState current_state, TaskState previous_state)
        {
            CurrentState = current_state;
            PreviousState = previous_state;
        }
    }
}
