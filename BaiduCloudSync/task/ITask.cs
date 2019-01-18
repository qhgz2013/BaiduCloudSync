using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BaiduCloudSync.task
{
    public interface ITask
    {
        void Start();
        void Pause();
        void Cancel();

        string Name { get; set; }

        TaskState State { get; }
    }
}
