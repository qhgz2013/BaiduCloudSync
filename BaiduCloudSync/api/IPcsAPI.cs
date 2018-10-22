
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BaiduCloudSync.api
{
    public interface IPcsAPI<PcsMetaData> where PcsMetaData : IPcsMetadata
    {
        PcsMetaData ListDir(string path);
        bool CreateDir(string path);
        
    }

}
