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
            var lambda_state_event = new EventHandler<TaskStateChangedEventArgs>((sender, e) =>
            {
                Tracer.GlobalTracer.TraceInfo($"{e.PreviousState} -> {e.CurrentState}");
            });
            task.StateChanged += lambda_state_event;
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

            var lambda_state_event = new EventHandler<TaskStateChangedEventArgs>((sender, e) =>
            {
                Tracer.GlobalTracer.TraceInfo($"{e.PreviousState} -> {e.CurrentState}");
            });
            var do_test = new ParameterizedThreadStart((_x) =>
            {
                Task x = (Task)_x;
                x.StateChanged += lambda_state_event;

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

            // testing start -> pause (request) -> failed -> retry -> finished operation sequence
            obj = new TaskImpl2 { ThrowOnPause = true };
            task = new Task(obj);
            do_test = new ParameterizedThreadStart((_x) =>
            {
                Task x = (Task)_x;
                x.StateChanged += lambda_state_event;
                x.Start();

                x.Wait(3000);
                Assert.AreEqual(TaskState.Started, x.State);
                x.Pause();
                x.Wait();
                Assert.AreEqual(TaskState.Failed, x.State);
                x.Retry();
                x.Wait();
                Assert.AreEqual(TaskState.Ready, x.State);
                x.Start();
                x.Wait();
                Assert.AreEqual(TaskState.Finished, x.State);
            });
            do_test(task);

            // testing start -> pause -> start -> cancel -> failed -> retry -> cancel operation sequence
            obj = new TaskImpl2 { ThrowOnCancel = true };
            task = new Task(obj);
            do_test = new ParameterizedThreadStart((_x) =>
            {
                Task x = (Task)_x;
                x.StateChanged += lambda_state_event;
                x.Start();
                x.Wait(3000);
                Assert.AreEqual(TaskState.Started, x.State);
                x.Pause();
                x.Wait();
                x.Cancel();
                x.Wait();
                Assert.AreEqual(TaskState.Failed, x.State);
                x.Retry();
                x.Wait();
                obj.ThrowOnCancel = false;
                x.Start();
                x.Wait(3000);
                x.Cancel();
                x.Wait();
                Assert.AreEqual(TaskState.Cancelled, x.State);
            });
            do_test(task);
        }

        [TestMethod]
        public void TaskStateTest4()
        {
            // testing start -> pause -> failed -> retry (requested) -> failed operation sequence

            var obj = new TaskImpl2 { ThrowOnPause = true, ThrowOnRetry = true };
            var task = new Task(obj);
            var lambda_state_event = new EventHandler<TaskStateChangedEventArgs>((sender, e) =>
            {
                Tracer.GlobalTracer.TraceInfo($"{e.PreviousState} -> {e.CurrentState}");
            });
            var do_test = new ParameterizedThreadStart((_x) =>
            {
                Task x = (Task)_x;
                x.StateChanged += lambda_state_event;
                x.Start();
                x.Wait(3000);
                Assert.AreEqual(TaskState.Started, x.State);
                x.Pause();
                x.Wait();
                Assert.AreEqual(TaskState.Failed, x.State);
                x.Retry();
                x.Wait();
                Assert.AreEqual(TaskState.Failed, x.State);
            });
            do_test(task);
        }

        [TestMethod]
        public void TaskStateTest5()
        {
            // testing TaskStateTest3 and 4 replacing throw Exception to EmitResponse
            // testing start -> failed -> retry -> failed operation sequence
            var obj = new TaskImpl1();
            var task = new Task(obj);

            var lambda_state_event = new EventHandler<TaskStateChangedEventArgs>((sender, e) =>
            {
                Tracer.GlobalTracer.TraceInfo($"{e.PreviousState} -> {e.CurrentState}");
            });
            var do_test = new ParameterizedThreadStart((_x) =>
            {
                Task x = (Task)_x;
                x.StateChanged += lambda_state_event;

                obj.EmitFailureInsteadResponse = true;
                x.Start();
                x.Wait(-1);
                Assert.AreEqual(TaskState.Failed, x.State);
                obj.EmitFailureInsteadResponse = false;

                x.Retry();
                x.Wait(-1);
                Assert.AreEqual(TaskState.Ready, x.State);

                obj.EmitFailureInsteadResponse = true;
                x.Start();
                x.Wait(-1);
                Assert.AreEqual(TaskState.Failed, x.State);
            });
            do_test(task);

            obj = new TaskImpl1();
            task = new Task(obj);
            do_test = new ParameterizedThreadStart((_x) =>
            {
                Task x = (Task)_x;
                x.StateChanged += lambda_state_event;

                x.Start();
                x.Wait(3000);
                Assert.AreEqual(TaskState.Started, x.State);

                obj.EmitFailureInsteadResponse = true;
                x.Wait(-1);
                Assert.AreEqual(TaskState.Failed, x.State);
            });
            do_test(task);

            // testing start -> pause (request) -> failed -> retry -> finished operation sequence
            obj = new TaskImpl1();
            task = new Task(obj);
            do_test = new ParameterizedThreadStart((_x) =>
            {
                Task x = (Task)_x;
                x.StateChanged += lambda_state_event;
                x.Start();

                x.Wait(3000);
                Assert.AreEqual(TaskState.Started, x.State);
                obj.EmitFailureInsteadResponse = true;
                x.Pause();
                x.Wait();
                Assert.AreEqual(TaskState.Failed, x.State);
                obj.EmitFailureInsteadResponse = false;
                x.Retry();
                x.Wait();
                Assert.AreEqual(TaskState.Ready, x.State);
                x.Start();
                x.Wait();
                Assert.AreEqual(TaskState.Finished, x.State);
            });
            do_test(task);

            // testing start -> pause -> start -> cancel -> failed -> retry -> cancel operation sequence
            obj = new TaskImpl1();
            task = new Task(obj);
            do_test = new ParameterizedThreadStart((_x) =>
            {
                Task x = (Task)_x;
                x.StateChanged += lambda_state_event;
                x.Start();
                x.Wait(3000);
                Assert.AreEqual(TaskState.Started, x.State);
                x.Pause();
                x.Wait();
                obj.EmitFailureInsteadResponse = true;
                x.Cancel();
                x.Wait();
                obj.EmitFailureInsteadResponse = false;
                Assert.AreEqual(TaskState.Failed, x.State);
                x.Retry();
                x.Wait();

                x.Start();
                x.Wait(3000);
                x.Cancel();
                x.Wait();
                Assert.AreEqual(TaskState.Cancelled, x.State);
            });
            do_test(task);
        }

        [TestMethod]
        [ExpectedException(typeof(InvalidTaskStateException))]
        public void TaskStateTest6()
        {
            var obj = new TaskImpl2();
            var task = new Task(obj);
            task.Start();
            task.Cancel();
            Assert.Fail();
        }
    }
}
