using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace GlobalUtil
{
    /// <summary>
    /// NetStream参数
    /// </summary>
    public sealed class Parameters : ICollection<KeyValuePair<string, string>>
    {
        private List<KeyValuePair<string, string>> _list;
        public Parameters()
        {
            _list = new List<KeyValuePair<string, string>>();
        }
        /// <summary>
        /// 添加参数
        /// </summary>
        /// <typeparam name="T">参数类型</typeparam>
        /// <param name="key">参数名称</param>
        /// <param name="value">参数的值</param>
        public void Add<T>(string key, T value)
        {
            _list.Add(new KeyValuePair<string, string>(key, value.ToString()));
        }
        /// <summary>
        /// 对所有参数按名称进行排序
        /// </summary>
        /// <param name="desc">是否使用倒序排序（默认为正序）</param>
        public void SortParameters(bool desc = false)
        {
            var n = new List<KeyValuePair<string, string>>();
            IOrderedEnumerable<KeyValuePair<string, string>> sec = null;
            if (desc) sec = from KeyValuePair<string, string> item in _list orderby item.Key ascending select item;
            else sec = from KeyValuePair<string, string> item in _list orderby item.Key descending select item;
            foreach (var item in sec)
            {
                n.Add(item);
            }
            _list = n;
        }
        /// <summary>
        /// 构造url的查询参数
        /// </summary>
        /// <param name="enableUrlEncode">是否使用url转义</param>
        /// <returns>与参数等价的query string</returns>
        public string BuildQueryString(bool enableUrlEncode = true)
        {
            var sb = new StringBuilder();
            foreach (var item in _list)
            {
                sb.Append(item.Key);
                if (!string.IsNullOrEmpty(item.Key)) sb.Append('=');
                if (enableUrlEncode)
                {
                    int max_i = (int)Math.Ceiling(item.Value.Length / 100.0);
                    for (int i = 0; i < max_i; i++)
                        sb.Append(Uri.EscapeDataString(item.Value.Substring(i * 100, Math.Min(100, item.Value.Length - 100 * i))));
                }
                else sb.Append(item.Value);
                sb.Append('&');
            }
            sb.Remove(sb.Length - 1, 1);
            return sb.ToString();
        }
        /// <summary>
        /// 移除首个匹配项
        /// </summary>
        /// <param name="key">参数名称</param>
        /// <returns>是否移除成功</returns>
        public bool Remove(string key)
        {
            for (int i = 0; i < _list.Count; i++)
            {
                if (_list[i].Key == key)
                {
                    _list.RemoveAt(i);
                    return true;
                }
            }
            return false;
        }
        /// <summary>
        /// 移除指定下标的参数
        /// </summary>
        /// <param name="index">下标编号</param>
        /// <returns>是否移除成功</returns>
        public bool RemoveAt(int index)
        {
            if (index < _list.Count)
            {
                _list.RemoveAt(index);
                return true;
            }
            return false;
        }
        /// <summary>
        /// 移除所有匹配项
        /// </summary>
        /// <param name="key">参数名称</param>
        /// <returns>是否移除成功</returns>
        public bool RemoveAll(string key)
        {
            bool suc = false;
            for (int i = 0; i < _list.Count; i++)
            {
                if (_list[i].Key == key)
                {
                    _list.RemoveAt(i);
                    suc = true;
                }
            }
            return suc;
        }
        /// <summary>
        /// 列表中是否包含指定名称的参数
        /// </summary>
        /// <param name="key">参数名称</param>
        /// <returns>是否存在该参数</returns>
        public bool Contains(string key)
        {
            for (int i = 0; i < _list.Count; i++)
            {
                if (_list[i].Key == key)
                {
                    return true;
                }
            }
            return false;
        }
        /// <summary>
        /// 返回参数个数
        /// </summary>
        int ICollection<KeyValuePair<string, string>>.Count
        {
            get
            {
                return _list.Count;
            }
        }
        /// <summary>
        /// 是否只读
        /// </summary>
        bool ICollection<KeyValuePair<string, string>>.IsReadOnly
        {
            get
            {
                return false;
            }
        }
        /// <summary>
        /// 添加参数
        /// </summary>
        /// <param name="item">要添加的参数</param>
        void ICollection<KeyValuePair<string, string>>.Add(KeyValuePair<string, string> item)
        {
            _list.Add(item);
        }
        /// <summary>
        /// 清空参数列表
        /// </summary>
        void ICollection<KeyValuePair<string, string>>.Clear()
        {
            _list.Clear();
        }
        /// <summary>
        /// 是否包含某个参数（名称和数值全匹配）
        /// </summary>
        /// <param name="item">参数</param>
        /// <returns>是否存在该参数</returns>
        bool ICollection<KeyValuePair<string, string>>.Contains(KeyValuePair<string, string> item)
        {
            return _list.Contains(item);
        }
        /// <summary>
        /// 将列表复制到数组
        /// </summary>
        /// <param name="array">输出的数组</param>
        /// <param name="arrayIndex">要复制的下标开始点</param>
        void ICollection<KeyValuePair<string, string>>.CopyTo(KeyValuePair<string, string>[] array, int arrayIndex)
        {
            _list.CopyTo(array, arrayIndex);
        }
        /// <summary>
        /// 获取枚举器
        /// </summary>
        /// <returns>列表的枚举器</returns>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return _list.GetEnumerator();
        }
        /// <summary>
        /// 获取枚举器
        /// </summary>
        /// <returns>列表的枚举器</returns>
        IEnumerator<KeyValuePair<string, string>> IEnumerable<KeyValuePair<string, string>>.GetEnumerator()
        {
            return _list.GetEnumerator();
        }
        /// <summary>
        /// 移除匹配的参数
        /// </summary>
        /// <param name="item">参数</param>
        /// <returns></returns>
        bool ICollection<KeyValuePair<string, string>>.Remove(KeyValuePair<string, string> item)
        {
            return _list.Remove(item);
        }

        private string GetItems(int index)
        {
            if (index < 0 || index >= _list.Count) return string.Empty;
            return _list[index].Value;
        }
        private string GetItems(string name)
        {
            foreach (var item in _list)
            {
                if (item.Key == name)
                    return item.Value;
            }
            return string.Empty;
        }
        private void SetItem(int index, string value)
        {
            if (index < 0 || index >= _list.Count) return;
            SetItem(index, _list[index].Key, value);
        }
        private void SetItem(int index, string key, string value)
        {
            if (index < 0 || index >= _list.Count) return;
            _list[index] = new KeyValuePair<string, string>(key, value);
        }
        private void SetItem(int index, KeyValuePair<string, string> data)
        {
            SetItem(index, data.Key, data.Value);
        }
        private void SetItem(string key, string value)
        {
            int index = _list.FindIndex((x) => { if (x.Key == key) return true; else return false; });
            if (index == -1) throw new KeyNotFoundException(key);
            _list[index] = new KeyValuePair<string, string>(key, value);
        }
        private void SetItem(KeyValuePair<string, string> data)
        {
            SetItem(data.Key, data.Value);
        }
        public string this[int index]
        {
            get
            {
                return GetItems(index);
            }
            set
            {
                SetItem(index, value);
            }
        }
        public override string ToString()
        {
            return BuildQueryString();
        }
    }
}
