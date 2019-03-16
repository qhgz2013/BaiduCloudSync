using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace GlobalUtil.cryptography.streamadapter
{
    internal static class StreamHelper
    {

        /// <summary>
        /// 从指定数据流中读取size个字节，并检查返回的字节是否为size，不为size时引发异常
        /// </summary>
        /// <param name="stream">读取的数据流</param>
        /// <param name="size">读取的字节数</param>
        /// <exception cref="IOException">出现过早的End of stream时引发的异常</exception>
        /// <returns></returns>
        public static byte[] ReadBytesAndCheckSize(Stream stream, int size)
        {
            var ret = Util.ReadBytes(stream, size);
            if (ret.Length != size) throw new IOException($"Early end of stream, attempt to read {size} bytes, but read {ret.Length} bytes");
            return ret;
        }

        /// <summary>
        /// 检查两个操作数是否相同，在不相同时引发异常
        /// </summary>
        /// <typeparam name="T">操作数的类型</typeparam>
        /// <param name="actual">实际值</param>
        /// <param name="expected">期望值</param>
        /// <exception cref="FormatException">在实际值不等于期望值时引发的异常</exception>
        public static void AssertEqual<T>(T actual, T expected)
        {
            if (!actual.Equals(expected))
                throw new FormatException($"Bad stream format, expected {expected}, but got {actual}");
        }
        /// <summary>
        /// 检查两个操作数是否相同，在不相同时向日志中写入警告
        /// </summary>
        /// <typeparam name="T">操作数的类型</typeparam>
        /// <param name="actual">实际值</param>
        /// <param name="expected">期望值</param>
        public static void WarnNotEqual<T>(T actual, T expected)
        {
            if (!actual.Equals(expected))
                Tracer.GlobalTracer.TraceWarning($"stream data corrupt, expected {expected}, but got {actual}");
        }
        /// <summary>
        /// 从数据流中读取一个简单类型的数据，默认编码为BigEndian
        /// </summary>
        /// <typeparam name="T">简单的类型，即在BitConverter下有对应的ToXXX的类型（字符串除外）</typeparam>
        /// <param name="stream">读取的数据流</param>
        /// <returns></returns>
        public static T ReadType<T>(Stream stream)
        {
            int size = System.Runtime.InteropServices.Marshal.SizeOf(default(T));
            var data = ReadBytesAndCheckSize(stream, size);
            if (BitConverter.IsLittleEndian)
                data = data.Reverse().ToArray();
            object converted_data = _bit_converter_type_mapper[typeof(T)].DynamicInvoke(data, 0);
            return (T)converted_data;
        }

        public static void WriteType<T>(Stream stream, T data)
        {
            var bytes = _bit_converter_type_inv_mapper[typeof(T)].DynamicInvoke(data) as byte[];
            if (BitConverter.IsLittleEndian)
                bytes = bytes.Reverse().ToArray();
            stream.Write(bytes, 0, bytes.Length);
        }

        private delegate T _bit_converter_func_delegate<T>(byte[] b, int i);
        private delegate byte[] _bit_converter_func_delegate_inv<T>(T i);
        private static Dictionary<Type, Delegate> _bit_converter_type_mapper;
        private static Dictionary<Type, Delegate> _bit_converter_type_inv_mapper;
        static StreamHelper()
        {
            _bit_converter_type_mapper = new Dictionary<Type, Delegate>();
            _bit_converter_type_mapper.Add(typeof(int), new _bit_converter_func_delegate<int>(BitConverter.ToInt32));
            _bit_converter_type_mapper.Add(typeof(uint), new _bit_converter_func_delegate<uint>(BitConverter.ToUInt32));
            _bit_converter_type_mapper.Add(typeof(short), new _bit_converter_func_delegate<short>(BitConverter.ToInt16));
            _bit_converter_type_mapper.Add(typeof(ushort), new _bit_converter_func_delegate<ushort>(BitConverter.ToUInt16));
            _bit_converter_type_mapper.Add(typeof(long), new _bit_converter_func_delegate<long>(BitConverter.ToInt64));
            _bit_converter_type_mapper.Add(typeof(ulong), new _bit_converter_func_delegate<ulong>(BitConverter.ToUInt64));
            _bit_converter_type_mapper.Add(typeof(float), new _bit_converter_func_delegate<float>(BitConverter.ToSingle));
            _bit_converter_type_mapper.Add(typeof(double), new _bit_converter_func_delegate<double>(BitConverter.ToDouble));
            _bit_converter_type_mapper.Add(typeof(bool), new _bit_converter_func_delegate<bool>(BitConverter.ToBoolean));
            _bit_converter_type_mapper.Add(typeof(char), new _bit_converter_func_delegate<char>(BitConverter.ToChar));

            _bit_converter_type_inv_mapper = new Dictionary<Type, Delegate>();
            _bit_converter_type_inv_mapper.Add(typeof(int), new _bit_converter_func_delegate_inv<int>(BitConverter.GetBytes));
            _bit_converter_type_inv_mapper.Add(typeof(uint), new _bit_converter_func_delegate_inv<uint>(BitConverter.GetBytes));
            _bit_converter_type_inv_mapper.Add(typeof(short), new _bit_converter_func_delegate_inv<short>(BitConverter.GetBytes));
            _bit_converter_type_inv_mapper.Add(typeof(ushort), new _bit_converter_func_delegate_inv<ushort>(BitConverter.GetBytes));
            _bit_converter_type_inv_mapper.Add(typeof(long), new _bit_converter_func_delegate_inv<long>(BitConverter.GetBytes));
            _bit_converter_type_inv_mapper.Add(typeof(ulong), new _bit_converter_func_delegate_inv<ulong>(BitConverter.GetBytes));
            _bit_converter_type_inv_mapper.Add(typeof(float), new _bit_converter_func_delegate_inv<float>(BitConverter.GetBytes));
            _bit_converter_type_inv_mapper.Add(typeof(double), new _bit_converter_func_delegate_inv<double>(BitConverter.GetBytes));
            _bit_converter_type_inv_mapper.Add(typeof(bool), new _bit_converter_func_delegate_inv<bool>(BitConverter.GetBytes));
            _bit_converter_type_inv_mapper.Add(typeof(char), new _bit_converter_func_delegate_inv<char>(BitConverter.GetBytes));
        }

    }
}
