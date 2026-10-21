using Abot2.Util;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading;

namespace Abot2.Tests.Unit.Util
{
    //Expects background work to finish within fixed sleeps, which other tests running at the same time could delay
    [TestClass]
    [DoNotParallelize]
    public class TaskThreadManagerTest : ThreadManagerTest
    {
        protected override IThreadManager GetInstance(int maxThreads)
        {
            return new TaskThreadManager(maxThreads);
        }

        //Cancellation only stops work that has not started yet, work already running is not interrupted. These tests used to cancel
        //right after queuing work and assert some of it never ran, which raced the thread pool and failed whenever the test thread
        //was delayed for a millisecond before cancelling. They now only assert what cancellation actually guarantees.

        [TestMethod]
        public void AbortAll_CancelsTokenSource()
        {
            var cancellationTokenSource = new CancellationTokenSource();
            var uut = new TaskThreadManager(10, cancellationTokenSource);

            uut.AbortAll();

            Assert.IsTrue(cancellationTokenSource.IsCancellationRequested);
        }

        [TestMethod]
        public void DoWork_TokenCanceled_WorkNeverStarts()
        {
            var cancellationTokenSource = new CancellationTokenSource();
            var uut = new TaskThreadManager(10, cancellationTokenSource);
            cancellationTokenSource.Cancel();

            var count = 0;
            for (var i = 0; i < 5; i++)
                uut.DoWork(() => Interlocked.Increment(ref count));

            System.Threading.Thread.Sleep(200);
            Assert.AreEqual(0, count);
        }

        [TestMethod]
        public void Dispose_CancelsButDoesNotDisposeTokenSourcePassedIn()
        {
            var cancellationTokenSource = new CancellationTokenSource();
            var uut = new TaskThreadManager(10, cancellationTokenSource);

            uut.Dispose();

            Assert.IsTrue(cancellationTokenSource.IsCancellationRequested);
            _ = cancellationTokenSource.Token;//throws ObjectDisposedException if it was disposed
            cancellationTokenSource.Dispose();
        }

        [TestMethod]
        public void AbortAll_AfterTokenSourcePassedInWasDisposed_DoesNotThrow()
        {
            var cancellationTokenSource = new CancellationTokenSource();
            var uut = new TaskThreadManager(10, cancellationTokenSource);
            cancellationTokenSource.Dispose();

            uut.AbortAll();
            uut.Dispose();
        }
    }
}
