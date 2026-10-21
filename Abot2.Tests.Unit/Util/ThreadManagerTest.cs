using System.Threading.Tasks;
using Abot2.Util;
using System;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Abot2.Tests.Unit.Util
{
    public abstract class ThreadManagerTest
    {
        IThreadManager _unitUnderTest;
        const int MAXTHREADS = 10;
        public TestContext TestContext { get; set; }

        protected abstract IThreadManager GetInstance(int maxThreads);

        [TestInitialize]
        public void SetUp()
        {
            _unitUnderTest = GetInstance(MAXTHREADS);
        }

        [TestCleanup]
        public void TearDown()
        {
            if (_unitUnderTest != null)
                _unitUnderTest.Dispose();
        }

        [TestMethod]
        public void Constructor_CreatesDefaultInstance()
        {
            Assert.IsNotNull(_unitUnderTest);
            Assert.AreEqual(MAXTHREADS, _unitUnderTest.MaxThreads);
        }

        [TestMethod]
        public void Constructor_OverMax_ThrowsException()
        {
            Assert.ThrowsExactly<ArgumentException>(() => GetInstance(101));
        }

        [TestMethod]
        public void Constructor_BelowMinimum()
        {
            Assert.ThrowsExactly<ArgumentException>(() => GetInstance(0));
        }

        [TestMethod]
        public async Task HasRunningThreads()
        {
            //No threads should be running
            Assert.IsFalse(_unitUnderTest.HasRunningThreads());

            //Add word to be run on a thread
            _unitUnderTest.DoWork(() => System.Threading.Thread.Sleep(300));
            await Task.Delay(20, TestContext.CancellationToken);

            //Should have 1 running thread
            Assert.IsTrue(_unitUnderTest.HasRunningThreads());

            //Wait for the 1 running thread to finish
            await Task.Delay(400, TestContext.CancellationToken);

            //Should have 0 threads running since the thread should have completed by now
            Assert.IsFalse(_unitUnderTest.HasRunningThreads());
        }

        [TestMethod]
        public async Task DoWork_WorkItemsEqualToThreads_WorkIsCompletedAsync()
        {
            var count = 0;

            for (var i = 0; i < MAXTHREADS; i++)
            {
                _unitUnderTest.DoWork(() =>
                {
                    System.Threading.Thread.Sleep(5);
                    Interlocked.Increment(ref count);
                });
            }

            Assert.IsTrue(count < MAXTHREADS);
            await Task.Delay(80, TestContext.CancellationToken);//was 20 but had to bump it up or the TaskThreadManager would fail, its way slower
            Assert.AreEqual(MAXTHREADS, count);
        }

        [TestMethod]
        public async Task DoWork_MoreWorkThenThreads_WorkIsCompletedAsync()
        {
            var count = 0;
            for (var i = 0; i < 2 * MAXTHREADS; i++)
            {
                _unitUnderTest.DoWork(() =>
                {
                    System.Threading.Thread.Sleep(5);
                    Interlocked.Increment(ref count);
                });
            }

            //Assert.IsTrue(count < MAXTHREADS);//Manual has completed more then the thread count by the time it gets here
            await Task.Delay(80, TestContext.CancellationToken);//was 20 but had to bump it up or the TaskThreadManager would fail, its way slower
            Assert.AreEqual(2 * MAXTHREADS, count);
        }

        [TestMethod]
        public void DoWork_SingleThreaded_WorkIsCompletedSynchronously()
        {
            _unitUnderTest = GetInstance(1);

            var count = 0;
            for (var i = 0; i < MAXTHREADS; i++)
            {
                _unitUnderTest.DoWork(() =>
                {
                    System.Threading.Thread.Sleep(5);
                    Interlocked.Increment(ref count);
                });
            }

            Assert.AreEqual(MAXTHREADS, count);
        }

        [TestMethod]
        public void DoWork_ActionIsNull_ThrowsException()
        {
            Assert.ThrowsExactly<ArgumentNullException>(() => _unitUnderTest.DoWork(null));
        }

        [TestMethod]
        public void DoWork_CalledAfterAbortAll_ThrowsException()
        {
            _unitUnderTest.AbortAll();

            Assert.ThrowsExactly<InvalidOperationException>(() => _unitUnderTest.DoWork(() => System.Threading.Thread.Sleep(10)));
        }

        [TestMethod]
        public void Dispose()
        {
            Assert.IsTrue(_unitUnderTest is IDisposable);
        }

        [TestMethod]
        public void Abort_SetsHasRunningThreadsToZero()
        {
            for (var i = 0; i < 2 * MAXTHREADS; i++)
            {
                _unitUnderTest.DoWork(() => System.Threading.Thread.Sleep(5));
            }

            _unitUnderTest.AbortAll();
            Assert.IsFalse(_unitUnderTest.HasRunningThreads());
        }

        [TestMethod]
        public void DoWork_CalledAfterDispose_ThrowsObjectDisposedException()
        {
            _unitUnderTest.Dispose();

            Assert.ThrowsExactly<ObjectDisposedException>(() => _unitUnderTest.DoWork(() => System.Threading.Thread.Sleep(10)));
        }

        [TestMethod]
        public void Dispose_CalledTwice_DoesNotThrow()
        {
            _unitUnderTest.Dispose();
            _unitUnderTest.Dispose();
        }

        [TestMethod]
        public async Task Dispose_WhileThreadsAreRunning_DoesNotThrow()
        {
            for (var i = 0; i < MAXTHREADS; i++)
                _unitUnderTest.DoWork(() => System.Threading.Thread.Sleep(50));

            _unitUnderTest.Dispose();

            //Let the running threads finish against the disposed instance
            await Task.Delay(200, TestContext.CancellationToken);
        }
    }
}
