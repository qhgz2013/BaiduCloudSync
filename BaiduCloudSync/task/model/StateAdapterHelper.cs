using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace BaiduCloudSync.task.model
{
    internal static class StateAdapterHelper
    {
        public static void SetTaskState(TaskState state, Task parent, ManualResetEventSlim previous_wait_event = null)
        {
            switch (state)
            {
                case TaskState.Ready:
                    new ReadyStateAdapter(parent);
                    break;
                case TaskState.StartRequested:
                    new StartRequestedStateAdapter(parent);
                    break;
                case TaskState.Started:
                    new StartedStateAdapter(parent);
                    break;
                case TaskState.PauseRequested:
                    new PauseRequestedStateAdapter(parent);
                    break;
                case TaskState.Paused:
                    new PausedStateAdapter(parent, previous_wait_event);
                    break;
                case TaskState.CancelRequested:
                    new CancelRequestedStateAdapter(parent);
                    break;
                case TaskState.Cancelled:
                    new CancelledStateAdapter(parent, previous_wait_event);
                    break;
                case TaskState.Finished:
                    new FinishedStateAdapter(parent);
                    break;
                case TaskState.Failed:
                    new FailedStateAdapter(parent);
                    break;
                case TaskState.RetryRequested:
                    new RetryRequestedStateAdapter(parent);
                    break;
                default:
                    throw new InvalidTaskStateException("The state is invalid");
            }
        }
    }
}
