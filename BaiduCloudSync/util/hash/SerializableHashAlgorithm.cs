using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace GlobalUtil.hash
{
    /// <summary>
    /// 可序列化到Stream类的Hash算法接口
    /// </summary>
    [Serializable]
    public abstract class SerializableHashAlgorithm
    {
        /// <summary>
        /// 初始化Hash算法
        /// </summary>
        public abstract void Initialize();
        /// <summary>
        /// 计算输入字节数组指定区域的hash值
        /// </summary>
        /// <param name="buffer">输入字节数组</param>
        /// <param name="index">起始位置偏移</param>
        /// <param name="length">字节长度</param>
        public abstract void TransformBlock(byte[] buffer, int index, int length);
        /// <summary>
        /// 计算输入字节数组指定区域的hash值，并进行最终的hash运算
        /// </summary>
        /// <param name="buffer">输入字节数组</param>
        /// <param name="index">起始位置偏移</param>
        /// <param name="length">字节长度</param>
        public abstract void TransformFinalBlock(byte[] buffer, int index, int length);
        /// <summary>
        /// 获取当前计算得到的hash
        /// </summary>
        public abstract byte[] Hash { get; }
        /// <summary>
        /// 获取计算hash时的字节长度
        /// </summary>
        public abstract long Length { get; }

        // serialization and de-serialization operations
        /// <summary>
        /// 序列化当前hash算法的状态到IO数据流
        /// </summary>
        /// <param name="stream">可写入的数据流，用于写入当前hash状态</param>
        /// <exception cref="ArgumentNullException">当数据流为null时引发的异常</exception>
        /// <exception cref="SerializationException">当数据流不可写入、IO或序列化错误时引发的异常</exception>
        public void Serialize(Stream stream)
        {
            if (stream == null)
                throw new ArgumentNullException("stream");
            try
            {
                if (!stream.CanWrite)
                    throw new SerializationException("stream is not writable");
                var fmt = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
                fmt.Serialize(stream, this);
            }
            catch (Exception ex)
            {
                throw new SerializationException("could not serialize hash state to stream", ex);
            }
        }
        /// <summary>
        /// 序列化当前hash算法的状态到文件中
        /// </summary>
        /// <param name="file">文件路径</param>
        /// <exception cref="ArgumentNullException">文件路径为空时引发的异常</exception>
        /// <exception cref="SerializationException">当数据流不可写入、IO或序列化错误时引发的异常</exception>
        public void Serialize(string file)
        {
            if (string.IsNullOrEmpty(file))
                throw new ArgumentNullException("file");
            FileStream fs = null;
            try
            {
                fs = new FileStream(file, FileMode.Create, FileAccess.Write, FileShare.None);
                Serialize(fs);
            }
            finally
            {
                if (fs != null)
                    fs.Close();
            }
        }
        /// <summary>
        /// 从数据流中读取hash算法的状态，并实例化出该状态下的HashAlgorithm对象
        /// </summary>
        /// <param name="stream">可读取的数据流，用于读取当前hash状态</param>
        /// <returns>逆序列化后实例化的对象</returns>
        /// <exception cref="ArgumentNullException">当数据流为null时引发的异常</exception>
        /// <exception cref="SerializationException">当数据流不可读取、IO或序列化错误时引发的异常</exception>
        public static SerializableHashAlgorithm Deserialize(Stream stream)
        {
            if (stream == null)
                throw new ArgumentNullException("stream");
            try
            {
                if (!stream.CanRead)
                    throw new SerializationException("stream is not readable");
                var fmt = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
                return fmt.Deserialize(stream) as SerializableHashAlgorithm;
            }
            catch (Exception ex)
            {
                throw new SerializationException("could not deserialize hash state from stream", ex);
            }
        }

        /// <summary>
        /// 从文件中逆序列化当前hash算法的状态
        /// </summary>
        /// <param name="file">文件路径</param>
        /// <exception cref="ArgumentNullException">文件路径为空时引发的异常</exception>
        /// <exception cref="SerializationException">当数据流不可读取、IO或序列化错误时引发的异常</exception>
        public static SerializableHashAlgorithm Deserialize(string file)
        {
            if (string.IsNullOrEmpty(file))
                throw new ArgumentNullException("file");
            FileStream fs = null;
            try
            {
                fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read);
                return Deserialize(fs);
            }
            finally
            {
                if (fs != null)
                    fs.Close();
            }
        }
        
    }
}
