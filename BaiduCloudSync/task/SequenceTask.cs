using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BaiduCloudSync.task
{
    public class SequenceTask : Task, IList<ITask>
    {
        private List<ITask> _execution_task;
        private int _exeution_index;

        #region interface IList<ITask>
        public ITask this[int index]
        {
            get
            {
                return ((IList<ITask>)_execution_task)[index];
            }

            set
            {
                ((IList<ITask>)_execution_task)[index] = value;
            }
        }

        public int Count
        {
            get
            {
                return ((IList<ITask>)_execution_task).Count;
            }
        }

        public bool IsReadOnly
        {
            get
            {
                return ((IList<ITask>)_execution_task).IsReadOnly;
            }
        }

        public void Add(ITask item)
        {
            ((IList<ITask>)_execution_task).Add(item);
        }

        public void Clear()
        {
            ((IList<ITask>)_execution_task).Clear();
        }

        public bool Contains(ITask item)
        {
            return ((IList<ITask>)_execution_task).Contains(item);
        }

        public void CopyTo(ITask[] array, int arrayIndex)
        {
            ((IList<ITask>)_execution_task).CopyTo(array, arrayIndex);
        }

        public IEnumerator<ITask> GetEnumerator()
        {
            return ((IList<ITask>)_execution_task).GetEnumerator();
        }

        public int IndexOf(ITask item)
        {
            return ((IList<ITask>)_execution_task).IndexOf(item);
        }

        public void Insert(int index, ITask item)
        {
            ((IList<ITask>)_execution_task).Insert(index, item);
        }

        public bool Remove(ITask item)
        {
            return ((IList<ITask>)_execution_task).Remove(item);
        }

        public void RemoveAt(int index)
        {
            ((IList<ITask>)_execution_task).RemoveAt(index);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IList<ITask>)_execution_task).GetEnumerator();
        }

        #endregion

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
