using BaiduCloudSync.segment;
using BaiduCloudSync.task;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BaiduCloudSync.api
{
    public sealed class PcsFileUploader: Task
    {
        private IPcsAPI _pcsAPI;
        private ISegmentAlgorithm _segmentAlgorithm;

        public PcsFileUploader(IPcsAPI pcsAPI, ISegmentAlgorithm segmentAlgorithm)
        {

        }

        protected override void _cancel_internal(TaskState previous_state)
        {
            throw new NotImplementedException();
        }

        protected override void _pause_internal(TaskState previous_state)
        {
            throw new NotImplementedException();
        }

        protected override void _start_internal(TaskState previous_state)
        {
            throw new NotImplementedException();
        }
    }
}
