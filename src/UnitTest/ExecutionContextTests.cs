﻿using System;
using System.Linq;
using CoCoL;
using System.Threading.Tasks;

#if NETCOREAPP2_0
using TOP_LEVEL = Microsoft.VisualStudio.TestTools.UnitTesting.TestClassAttribute;
using TEST_METHOD = Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute;
#else
using TOP_LEVEL = NUnit.Framework.TestFixtureAttribute;
using TEST_METHOD = NUnit.Framework.TestAttribute;
#endif

namespace UnitTest
{
    [TOP_LEVEL]
	public class ExecutionContextTests
	{
        [TEST_METHOD]
		public void TestSingleThreadPool()
		{
			TestCappedPool(1, 10, 50);
		}

        [TEST_METHOD]
		public void TestDualThreadPool()
		{
			TestCappedPool(2, 20, 100);
		}

        [TEST_METHOD]
		public void TestQuadThreadPool()
		{
			TestCappedPool(4, 40, 200);
		}

        [TEST_METHOD]
		public void TestOctoThreadPool()
		{
			TestCappedPool(8, 20, 400);
		}

        [TEST_METHOD]
		public void TestUnlimitedThreadPool()
		{
			TestCappedPool(-1, 200, 800);
		}

		private void TestCappedPool(int poolsize, int readers, int writes)
		{
			var concurrent = 0;
			var max_concurrent = 0;
			var rnd = new Random();
			var earlyRetire = new TaskCompletionSource<bool>();

			using (new IsolatedChannelScope())
			using(new ExecutionScope(poolsize <= 0 ? ThreadPool.DEFAULT_THREADPOOL : new CappedThreadedThreadPool(poolsize)))
			{
				var readertasks = Task.WhenAll(Enumerable.Range(0, readers).Select(count =>
					AutomationExtensions.RunTask(new {
						Input = ChannelMarker.ForRead<int>("channel")
					},
						async x =>
						{
							//Console.WriteLine("Started {0}", count);

							while (true)
							{
								await x.Input.ReadAsync();
								var cur = System.Threading.Interlocked.Increment(ref concurrent);
								//Console.WriteLine("Active {0}", count);

								// Dirty access to "concurrent" and "max_concurrent" variables
								max_concurrent = Math.Max(cur, Math.Max(max_concurrent, concurrent));

								if (cur > poolsize && poolsize > 0)
								{
									Console.WriteLine("Found {0} concurrent threads", cur);
									earlyRetire.TrySetException(new UnittestException(string.Format("Found {0} concurrent threads", cur)));
									throw new UnittestException(string.Format("Found {0} concurrent threads", cur));
								}

								// By blocking the actual thread, we provoke the threadpool to start multiple threads
								System.Threading.Thread.Sleep(rnd.Next(10, 500));

								// Dirty access to "concurrent" and "max_concurrent" variables
								max_concurrent = Math.Max(cur, Math.Max(max_concurrent, concurrent));
								System.Threading.Interlocked.Decrement(ref concurrent);
								//Console.WriteLine("Inactive {0}", count);

							}

						})
				));

				var writetask = AutomationExtensions.RunTask(new {
					Output = ChannelMarker.ForWrite<int>("channel")
				},
				async x => {
					foreach(var i in Enumerable.Range(0, writes))
					{
						//Console.WriteLine("Writing {0}", i);
						await x.Output.WriteAsync(i);
					}
				});

				var timeout = Task.Delay((writes * 500) + 5000);
				if (Task.WhenAny(Task.WhenAll(readertasks, writetask), timeout, earlyRetire.Task).WaitForTaskOrThrow() == timeout)
					throw new TimeoutException("I've waited for so long ....");

				Console.WriteLine("Threads at shutdown: {0}", concurrent);

                ExecutionScope.Current.EnsureFinishedAsync(TimeSpan.FromSeconds(5)).WaitForTaskOrThrow();
				Console.WriteLine("Max concurrent threads: {0}, should be {1}", max_concurrent, poolsize <= 0 ? "unlimited" : poolsize.ToString());

                if (poolsize > 0 && max_concurrent > poolsize)
                    throw new UnittestException($"The pool allowed {max_concurrent} concurrent threads, but should be limited to {poolsize}");

			}

		}

	}
}

