using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BaiduCloudSync.db.mapper
{
    public class DatabaseMapperFactory : IDictionary<Type, IDatabaseMapper>
    {
        private Dictionary<Type, IDatabaseMapper> _mapper;

        public IDatabaseMapper this[Type key]
        {
            get
            {
                return ((IDictionary<Type, IDatabaseMapper>)_mapper)[key];
            }

            set
            {
                ((IDictionary<Type, IDatabaseMapper>)_mapper)[key] = value;
            }
        }

        public int Count
        {
            get
            {
                return ((IDictionary<Type, IDatabaseMapper>)_mapper).Count;
            }
        }

        public bool IsReadOnly
        {
            get
            {
                return ((IDictionary<Type, IDatabaseMapper>)_mapper).IsReadOnly;
            }
        }

        public ICollection<Type> Keys
        {
            get
            {
                return ((IDictionary<Type, IDatabaseMapper>)_mapper).Keys;
            }
        }

        public ICollection<IDatabaseMapper> Values
        {
            get
            {
                return ((IDictionary<Type, IDatabaseMapper>)_mapper).Values;
            }
        }

        public void Add(KeyValuePair<Type, IDatabaseMapper> item)
        {
            ((IDictionary<Type, IDatabaseMapper>)_mapper).Add(item);
        }

        public void Add(Type key, IDatabaseMapper value)
        {
            ((IDictionary<Type, IDatabaseMapper>)_mapper).Add(key, value);
        }

        public void Clear()
        {
            ((IDictionary<Type, IDatabaseMapper>)_mapper).Clear();
        }

        public bool Contains(KeyValuePair<Type, IDatabaseMapper> item)
        {
            return ((IDictionary<Type, IDatabaseMapper>)_mapper).Contains(item);
        }

        public bool ContainsKey(Type key)
        {
            return ((IDictionary<Type, IDatabaseMapper>)_mapper).ContainsKey(key);
        }

        public void CopyTo(KeyValuePair<Type, IDatabaseMapper>[] array, int arrayIndex)
        {
            ((IDictionary<Type, IDatabaseMapper>)_mapper).CopyTo(array, arrayIndex);
        }

        public IEnumerator<KeyValuePair<Type, IDatabaseMapper>> GetEnumerator()
        {
            return ((IDictionary<Type, IDatabaseMapper>)_mapper).GetEnumerator();
        }

        public bool Remove(KeyValuePair<Type, IDatabaseMapper> item)
        {
            return ((IDictionary<Type, IDatabaseMapper>)_mapper).Remove(item);
        }

        public bool Remove(Type key)
        {
            return ((IDictionary<Type, IDatabaseMapper>)_mapper).Remove(key);
        }

        public bool TryGetValue(Type key, out IDatabaseMapper value)
        {
            return ((IDictionary<Type, IDatabaseMapper>)_mapper).TryGetValue(key, out value);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IDictionary<Type, IDatabaseMapper>)_mapper).GetEnumerator();
        }
    }
}
