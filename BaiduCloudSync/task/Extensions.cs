using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BaiduCloudSync.task
{
    public static class Extensions
    {
        public static Task ToTask<T> (this T executor) where T : ITaskExecutor
        {
            return new Task(executor);
        }
    }
}
