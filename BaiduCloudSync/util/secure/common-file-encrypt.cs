using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace BaiduCloudSync
{
    public class FileEncrypt
    {
        public static TrackedData EncryptFile(string inputFile, string outputFile, TrackedData inputData, byte[] rsaPublic, byte[] aesKey = null, byte[] aesIV = null)
        {
            var fs_in = new FileStream(inputFile, FileMode.Open, FileAccess.Read, FileShare.Read);
            var fs_out = new FileStream(outputFile, FileMode.Create, FileAccess.Write, FileShare.None);
            fs_in.Close();
            fs_out.Close();
            throw new NotImplementedException();
        }
    }
}
