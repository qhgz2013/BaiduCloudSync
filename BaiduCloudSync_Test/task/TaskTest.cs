using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using BaiduCloudSync.task;
using System.Threading;

namespace BaiduCloudSync_Test.task
{
    [TestClass]
    public class TaskTest
    {
        [TestMethod]
        public void TaskStateTest1()
        {
            var obj = new TaskImpl(1000);
            Assert.AreEqual(TaskState.Ready, obj.State);
            obj.Start();
            obj.Start();
            obj.WaitStart();
            Assert.AreEqual(1, obj.StartTriggeredTimes);
            Assert.AreEqual(TaskState.Started, obj.State);

            Thread.Sleep(1500);
            Assert.AreEqual(TaskState.Finished, obj.State);

            obj.Pause();
            Assert.AreEqual(0, obj.PauseTriggeredTimes);

            obj.Cancel();
            Assert.AreEqual(0, obj.CancelTriggeredTimes);

            obj.Start();
            Assert.AreEqual(1, obj.StartTriggeredTimes);
            Assert.AreEqual(TaskState.Finished, obj.State);
        }

        [TestMethod]
        public void TaskStateTest2()
        {
            var obj = new TaskImpl(20000);
            obj.Start();
            obj.Pause();
            obj.WaitPause();
            Assert.AreEqual(TaskState.Paused, obj.State);
            obj.Start();
            obj.Cancel();
            obj.WaitCancel();
            Assert.AreEqual(TaskState.Cancelled, obj.State);

            Assert.AreEqual(2, obj.StartTriggeredTimes);
            Assert.AreEqual(1, obj.PauseTriggeredTimes);
            Assert.AreEqual(1, obj.CancelTriggeredTimes);
        }

        [TestMethod]
        public void TaskStateTest3()
        {
            var obj = new TaskImpl(1000, throw_on_start: true);
            obj.Start();
            obj.WaitStart();
            Thread.Sleep(1500);
            Assert.AreEqual(TaskState.Failed, obj.State);
            obj.Start();
            obj.Pause();
            obj.Cancel();
            Assert.AreEqual(TaskState.Failed, obj.State);
        }
    }
}
