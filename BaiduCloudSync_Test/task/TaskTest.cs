using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using BaiduCloudSync.task;
using System.Threading;
using GlobalUtil;

namespace BaiduCloudSync_Test.task
{
    [TestClass]
    public class TaskTest
    {
        [TestMethod]
        public void TaskStateTest1()
        {
            // testing start -> pause -> start -> finished operation sequence
            var obj = new TaskImpl1();
            var task = new Task(obj);
            task.StateChanged += (sender, e) =>
            {
                Tracer.GlobalTracer.TraceInfo($"{e.PreviousState} -> {e.CurrentState}");
            };
            Assert.AreEqual(TaskState.Ready, task.State);
            task.Start();
            Assert.IsTrue(task.State == TaskState.Started || task.State == TaskState.StartRequested);
            task.Wait(1000);
            Assert.AreEqual(TaskState.Started, task.State);
            task.Pause();
            task.Wait(-1);
            Assert.AreEqual(TaskState.Paused, task.State);
            Assert.IsTrue(obj.PauseFlag);
            Assert.AreEqual(1, obj.TriggerPause);

            task.Start();
            task.Wait(-1);
            Assert.AreEqual(TaskState.Finished, task.State);

            Assert.AreEqual(0, obj.TriggerCancel);
            Assert.AreEqual(1, obj.TriggerPause);
            Assert.AreEqual(2, obj.TriggerStarted);
        }

        [TestMethod]
        public void TaskStateTest2()
        {
            // testing start -> pause -> start -> cancel operation sequence
            var obj = new TaskImpl1();
            var task = new Task(obj);
            task.StateChanged += (sender, e) =>
            {
                Tracer.GlobalTracer.TraceInfo($"{e.PreviousState} -> {e.CurrentState}");
            };
            Assert.AreEqual(TaskState.Ready, task.State);
            task.Start();
            Assert.IsTrue(task.State == TaskState.Started || task.State == TaskState.StartRequested);
            task.Wait(1000);
            Assert.AreEqual(TaskState.Started, task.State);
            task.Pause();
            task.Wait(-1);
            Assert.AreEqual(TaskState.Paused, task.State);
            Assert.IsTrue(obj.PauseFlag);
            Assert.AreEqual(1, obj.TriggerPause);

            task.Start();
            Tracer.GlobalTracer.TraceInfo("yes");
            task.Wait(3000);
            task.Cancel();
            task.Wait(-1);
            Assert.AreEqual(TaskState.Cancelled, task.State);
            Assert.IsTrue(obj.CancelFlag);
            Assert.AreEqual(1, obj.TriggerCancel);

            Assert.AreEqual(1, obj.TriggerCancel);
            Assert.AreEqual(1, obj.TriggerPause);
            Assert.AreEqual(2, obj.TriggerStarted);
        }

        [TestMethod]
        public void TaskStateTest3()
        {
            // testing start -> failed -> retry -> failed operation sequence
            var obj = new TaskImpl2 { ThrowOnStart = true };
            var task = new Task(obj);

            var do_test = new ParameterizedThreadStart((_x) =>
            {
                Task x = (Task)_x;
                x.StateChanged += (sender, e) =>
                {
                    Tracer.GlobalTracer.TraceInfo($"{e.PreviousState} -> {e.CurrentState}");
                };

                x.Start();
                x.Wait(-1);
                Assert.AreEqual(TaskState.Failed, x.State);

                x.Retry();
                x.Wait(-1);
                Assert.AreEqual(TaskState.Ready, x.State);

                x.Start();
                x.Wait(-1);
                Assert.AreEqual(TaskState.Failed, x.State);
            });
            do_test(task);

            obj = new TaskImpl2 { ThrowOnRun = true };
            task = new Task(obj);
            do_test(task);
        }

        //todo: add more test
    }
}
