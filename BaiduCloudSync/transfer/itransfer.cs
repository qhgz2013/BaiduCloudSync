using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BaiduCloudSync
{
    public interface ITransfer
    {
        void Start();
        void Pause();
        void Cancel();
        event EventHandler TaskStarted, TaskFinished, TaskPaused, TaskCancelled, TaskError;
    }
}
