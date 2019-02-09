using BaiduCloudSync.segment;
using BaiduCloudSync.task;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BaiduCloudSync.api
{
    public sealed class PcsFileUploader
    {
        private IPcsAPI _pcsAPI;
        private ISegmentAlgorithm _segmentAlgorithm;

        public PcsFileUploader(IPcsAPI pcsAPI, ISegmentAlgorithm segmentAlgorithm)
        {

        }
    }
}
