// task-dispatcher.cs
//
// 用于给多线程下载分配下载区块以及追踪下载状态的类
//
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GlobalUtil;

namespace BaiduCloudSync
{
    //用来给多线程下载分配文件位置的类
    public class TaskDispatcher
    {
        private const bool _enable_tracing = false;
        //文件大小
        private ulong _length;
        //已经记录的下载完成的长度
        private ulong _complete_length;
        //线程锁
        private object _data_lck;
        //最小分配大小，小于该值的区段将不会分配地址
        private const ulong _MIN_DISPATCH_LENGTH = 16384;
        //临时结构，用来保存该分段的申请id和结束位置
        private struct _t_struct
        {
            public Guid id;
            public ulong end_pos;
            public _t_struct(Guid _id, ulong _end_pos) { id = _id; end_pos = _end_pos; }
            public override string ToString()
            {
                return end_pos.ToString() + "{" + id.ToString() + "}";
            }
        }
        //文件分段表,范围是[beg_pos, end_pos),这里定义[a,a)为空集
        private SortedList<ulong, _t_struct> _segment_list;
        //id到开始位置的映射
        private SortedList<Guid, ulong> _guid_begpos_mapping;

        public TaskDispatcher(ulong length)
        {
            if (length == 0) throw new ArgumentOutOfRangeException("length");
            _length = length;
            _complete_length = 0;
            _data_lck = new object();

            _segment_list = new SortedList<ulong, _t_struct>();
            _guid_begpos_mapping = new SortedList<Guid, ulong>();

            //分段表默认最后一个元素为文件结尾，无法分配也无法去除
            _segment_list.Add(_length, new _t_struct(Guid.Empty, _length));
        }
        /// <summary>
        /// 分配新的任务，并返回该任务的标识id，若分配失败会返回Guid.Empty
        /// </summary>
        /// <param name="beg_position">文件的开始位置</param>
        /// <returns></returns>
        public Guid AllocateNewTask(out ulong beg_position)
        {
            try
            {
                //todo:优化停止段的重新分配
                lock (_data_lck)
                {
                    if (!_debug_check())
                    {
                        throw new Exception("Debug check failed");
                    }

                    ulong max_length = _segment_list.First().Key;
                    beg_position = 0;
                    int last_index = -1;
                    //减小分段的优化模式
                    if (_segment_list.Count == _guid_begpos_mapping.Count + 1)
                    {

                        //找到最大的空间进行分配
                        for (int i = 1; i < _segment_list.Count; i++)
                        {
                            var tmp_length = _segment_list.ElementAt(i).Key - _segment_list.ElementAt(i - 1).Value.end_pos;
                            //last_index = i - 1;
                            var last_index_has_task = _segment_list.ElementAt(i - 1).Value.id != Guid.Empty;
                            if (last_index_has_task)
                            {
                                //上一片段已经有执行中的任务就从中间分配
                                var allocated_length = tmp_length >> 1;
                                if (allocated_length > max_length)
                                {
                                    last_index = -1;
                                    //beg_position = _segment_list.ElementAt(i - 1).Key;
                                    beg_position = _segment_list.ElementAt(i - 1).Value.end_pos;
                                    beg_position += (tmp_length - allocated_length);
                                    max_length = allocated_length;
                                }
                            }
                            else
                            {
                                //没有的话从头开始分配
                                if (tmp_length > max_length)
                                {
                                    last_index = i - 1;
                                    beg_position = _segment_list.ElementAt(i - 1).Value.end_pos;
                                    max_length = tmp_length;
                                }
                            }
                        }
                    }
                    else
                    {
                        //找到最大的空间进行分配，跳过已经有任务的
                        for (int i = 1; i < _segment_list.Count; i++)
                        {
                            var tmp_length = _segment_list.ElementAt(i).Key - _segment_list.ElementAt(i - 1).Value.end_pos;
                            var last_index_has_task = _segment_list.ElementAt(i - 1).Value.id != Guid.Empty;
                            if (!last_index_has_task)
                            {
                                if (tmp_length > max_length)
                                {
                                    last_index = i - 1;
                                    beg_position = _segment_list.ElementAt(i - 1).Value.end_pos;
                                    max_length = tmp_length;
                                }
                            }
                        }
                    }

                    //length is too small, ignored
                    if (max_length == 0 || (max_length < _MIN_DISPATCH_LENGTH && last_index == -1 && _segment_list.Count > 1))
                    {
                        beg_position = 0;
#pragma warning disable
                        if (_enable_tracing)
                            Tracer.GlobalTracer.TraceInfo("TaskDispatcher.AllocateNewTask called: out ulong beg_position=" + beg_position + ", Guid id=" + Guid.Empty.ToString() + " \r\n[segment=" + _segment_list.Count + ", task=" + _guid_begpos_mapping.Count + ", last_index=" + last_index + "]");
#pragma warning restore
                        return Guid.Empty;
                    }

                    //saving segment data
                    var id = Guid.NewGuid();

                    //assign mode
                    if (last_index == -1)
                    {
                        _segment_list.Add(beg_position, new _t_struct(id, beg_position));
                        _guid_begpos_mapping.Add(id, beg_position);
                    }
                    //update mode
                    else
                    {
                        var last_begin_pos = _segment_list.ElementAt(last_index).Key;
                        _segment_list[last_begin_pos] = new _t_struct(id, _segment_list[last_begin_pos].end_pos);
                        _guid_begpos_mapping.Add(id, last_begin_pos);
                    }
#pragma warning disable
                    if (_enable_tracing)
                        Tracer.GlobalTracer.TraceInfo("TaskDispatcher.AllocateNewTask called: out ulong beg_position=" + beg_position + ", Guid id=" + id.ToString() + " \r\n[segment=" + _segment_list.Count + ", task=" + _guid_begpos_mapping.Count + ", last_index=" + last_index + "]");
                    return id;
#pragma warning restore
                }
            }
            catch (Exception ex)
            {
                Tracer.GlobalTracer.TraceError(ex.ToString());
                beg_position = 0;
                return Guid.Empty;
            }

        }
        /// <summary>
        /// 删除任务
        /// </summary>
        /// <param name="id">任务标识id</param>
        public void ReleaseTask(Guid id)
        {
            try
            {
                if (id == Guid.Empty) return;
                lock (_data_lck)
                {
                    if (!_guid_begpos_mapping.ContainsKey(id))
                    {
                        //Tracer.GlobalTracer.TraceWarning("Missing id " + id.ToString() + " in mapping, maybe it's a bug?");
                        return;
                    }

                    var beg_pos = _guid_begpos_mapping[id];
                    _guid_begpos_mapping.Remove(id);

                    _segment_list[beg_pos] = new _t_struct(Guid.Empty, _segment_list[beg_pos].end_pos);
#pragma warning disable
                    if (_enable_tracing)
                        Tracer.GlobalTracer.TraceInfo("TaskDispatcher.ReleaseTask called: Guid id=" + id + " [segment=" + _segment_list.Count + ", task=" + _guid_begpos_mapping.Count + "]");
#pragma warning restore
                }
            }
            catch (Exception ex)
            {
                Tracer.GlobalTracer.TraceError(ex.ToString());
            }
        }
        /// <summary>
        /// 更新任务的位置，返回是否需要继续下载
        /// </summary>
        /// <param name="id">任务标识id</param>
        /// <param name="current_position">该任务目前已完成的位置</param>
        /// <returns></returns>
        public bool UpdateTaskSituation(Guid id, ulong current_position)
        {
            try
            {
                if (id == Guid.Empty) return false;
                lock (_data_lck)
                {
                    //合理性检测
                    if (!_guid_begpos_mapping.ContainsKey(id)) return false;
                    var beg_pos = _guid_begpos_mapping[id];
                    //当前分段数据
                    var segment_data = _segment_list[beg_pos];

                    if (segment_data.end_pos > current_position)
                        throw new ArgumentException("Decreasing position is forbidden");

                    //修改当前数据
                    _complete_length += (current_position - segment_data.end_pos);
                    _segment_list[beg_pos] = new _t_struct(id, current_position);

                    //下一分段数据
                    var next_segment_index = _segment_list.IndexOfKey(beg_pos) + 1;
                    do
                    {
                        if (next_segment_index == _segment_list.Count - 1)
                        {
                            //在获取下一分段之前，为了不影响最后一个标记文件结束位置segment，加入以下判断
                            if (current_position >= _length)
                            {
                                current_position = _length;
                                _segment_list[beg_pos] = new _t_struct(Guid.Empty, _length);
                                _complete_length -= current_position - _length;
                                _guid_begpos_mapping.Remove(id);
                                return false;
                            }
                            else
                                return true;
                        }
                        var next_segment_data = _segment_list.ElementAt(next_segment_index);

                        //合并检测
                        if (current_position >= next_segment_data.Key)
                        {
                            if (current_position >= next_segment_data.Value.end_pos)
                            {
                                //直接吞掉下一段的数据
                                _complete_length -= next_segment_data.Value.end_pos - next_segment_data.Key;
                                var next_guid = next_segment_data.Value.id;
                                _segment_list.RemoveAt(next_segment_index);
                                if (next_guid != Guid.Empty)
                                {
                                    _guid_begpos_mapping.Remove(next_guid);
                                }
#pragma warning disable
                                if (_enable_tracing)
                                    Tracer.GlobalTracer.TraceInfo("TaskDispatcher.TaskMerge: Merging Task #" + next_segment_index + " -> #" + (next_segment_index - 1) + " segments=" + _segment_list.Count + ", task=" + _guid_begpos_mapping.Count);
#pragma warning restore
                            }
                            else
                            {
                                //这一段宣告死亡，由下一段接管
                                _complete_length -= next_segment_data.Key - current_position;
                                current_position = next_segment_data.Key;
                                _segment_list[beg_pos] = next_segment_data.Value;
                                var next_guid = next_segment_data.Value.id;
                                if (next_guid != Guid.Empty)
                                {
                                    _guid_begpos_mapping[next_guid] = beg_pos;
                                }
                                _guid_begpos_mapping.Remove(id);
                                _segment_list.RemoveAt(next_segment_index);

#pragma warning disable
                                if (_enable_tracing)
                                    Tracer.GlobalTracer.TraceInfo("TaskDispatcher.TaskMerge: Merging Task #" + (next_segment_index - 1) + " -> #" + next_segment_index + " segments=" + _segment_list.Count + ", task=" + _guid_begpos_mapping.Count);
#pragma warning restore
                                _debug_check();

                                return false;
                            }
                        }
                        else
                            break;
                    } while (true);

                    //执行到这里应该都是未完成该段的，包括合并后的情况
                    //Tracer.GlobalTracer.TraceInfo("TaskDispatcher.TaskMerge: segments=" + _segment_list.Count + ", task=" + _guid_begpos_mapping.Count);
                    _debug_check();

                    return true;
                }
            }
            catch (Exception ex)
            {
                Tracer.GlobalTracer.TraceError(ex.ToString());
                return false;
            }
        }

        public TaskDispatcher(string path)
        {
            StreamReader sr = null;
            try
            {
                new StreamReader(path);
                _data_lck = new object();
                _length = ulong.Parse(sr.ReadLine());
                _guid_begpos_mapping = new SortedList<Guid, ulong>();
                _segment_list = new SortedList<ulong, _t_struct>();
                while (!sr.EndOfStream)
                {
                    var linestr = sr.ReadLine();
                    var args = linestr.Split(' ');
                    var beg_pos = ulong.Parse(args[0]);
                    var end_pos = ulong.Parse(args[1]);
                    _segment_list.Add(beg_pos, new _t_struct(Guid.Empty, end_pos));
                }
                _segment_list.Add(_length, new _t_struct(Guid.Empty, _length));
            }
            catch (Exception ex)
            {
                Tracer.GlobalTracer.TraceError(ex.ToString());
            }
            finally
            {
                if (sr != null)
                    sr.Close();
            }
        }
        public void SaveFile(string path)
        {
            StreamWriter sw = null;
            try
            {
                sw = new StreamWriter(path, false);
                sw.WriteLine(_length);
                lock (_data_lck)
                {
                    for (int i = 0; i < _segment_list.Count - 1; i++)
                    {
                        var data = _segment_list.ElementAt(i);
                        sw.WriteLine(data.Key + " " + data.Value.end_pos);
                    }
                }
            }
            catch (Exception ex)
            {
                Tracer.GlobalTracer.TraceError(ex.ToString());
            }
            finally
            {
                if (sw != null)
                    sw.Close();
            }
        }
        public KeyValuePair<ulong, ulong>[] GetSegments()
        {
            lock (_data_lck)
            {
                var ret = new KeyValuePair<ulong, ulong>[_segment_list.Count - 1];
                for (int i = 0; i < _segment_list.Count - 1; i++)
                {
                    var data = _segment_list.ElementAt(i);
                    ret[i] = new KeyValuePair<ulong, ulong>(data.Key, data.Value.end_pos);
                }
                return ret;
            }
        }

        //debug check, detecting invalid segments
        private bool _debug_check()
        {
            //disable debug to improve the speed
#pragma warning disable
            return true;

            if (_segment_list.Last().Key != _segment_list.Last().Value.end_pos || _segment_list.Last().Key != _length) return false;
            for (int i = 1; i < _segment_list.Count - 1; i++)
            {
                if (_segment_list.ElementAt(i).Key <= _segment_list.ElementAt(i - 1).Value.end_pos)
                {
                    Tracer.GlobalTracer.TraceWarning("Segment overflow between #" + (i - 1) + " and #" + i);
                    return false;
                }
                if (_segment_list.ElementAt(i - 1).Value.end_pos < _segment_list.ElementAt(i - 1).Key)
                {
                    Tracer.GlobalTracer.TraceWarning("Segment inverted in #" + (i - 1));
                    return false;
                }
                var id = _segment_list.ElementAt(i - 1).Value.id;
                if (id != Guid.Empty && (!_guid_begpos_mapping.ContainsKey(id) || _segment_list.ElementAt(i - 1).Key != _guid_begpos_mapping[id]))
                {
                    Tracer.GlobalTracer.TraceWarning("Missing or incorrect Task id in #" + (i - 1));
                    return false;
                }
            }
            return true;
#pragma warning restore
        }

        public ulong Length { get { return _length; } }
        public int SegmentCount { get { return _segment_list.Count; } }
        public ulong CompletedLength { get { return _complete_length; } }
    }

}
