using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace GlobalUtil.hash
{
    public interface ISerializableHashAlgorithm
    {
        void Initialize();
        void TransformBlock(byte[] buffer, int index, int length);
        void TransformFinalBlock(byte[] buffer, int index, int length);
        byte[] Hash { get; }
        long Length { get; }

        void Serialize(Stream stream);
        void Serialize(string file);
    }
}
