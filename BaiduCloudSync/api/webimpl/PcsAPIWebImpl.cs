using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BaiduCloudSync.api.webimpl
{
    public class PcsAPIWebImpl : IPcsAPI
    {
        public void Copy(string src_path, string dst_path, PcsOverwriteType overwrite_type)
        {
            throw new NotImplementedException();
        }

        public bool CreateDirectory(string path)
        {
            throw new NotImplementedException();
        }

        public bool Delete(string path)
        {
            throw new NotImplementedException();
        }

        public PcsMetadata ListDir(string path)
        {
            throw new NotImplementedException();
        }

        public void Move(string src_path, string dst_path, PcsOverwriteType overwrite_type)
        {
            throw new NotImplementedException();
        }

        public void Rename(string path, string new_name)
        {
            throw new NotImplementedException();
        }

        PcsMetadata[] IPcsAPI.ListDir(string path)
        {
            throw new NotImplementedException();
        }
    }
}
