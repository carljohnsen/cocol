﻿using System;
using CoCoL;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTest
{
	[TestClass]
	public class TestLambdaProcess
	{
		private const string CHANNEL_NAME = "SomeChannel";

		[TestMethod]
		public void TestRetireWithoutLoss()
		{
			Task[] tasks;
			int count = 0;

			using (new ChannelScope())
			{
				tasks = new Task[] {
					AutomationExtensions.RunTask(
						new { channel = ChannelMarker.ForWrite<int>(CHANNEL_NAME) },

						async self =>
						{
							await Task.Delay(500);
							await self.channel.WriteAsync(1);
						}
					),

					AutomationExtensions.RunTask(
						new { channel = ChannelMarker.ForWrite<int>(CHANNEL_NAME) },

						async self =>
						{
							await Task.Delay(1000);
							await self.channel.WriteAsync(1);
						}
					),

					AutomationExtensions.RunTask(
						new { channel = ChannelMarker.ForRead<int>(CHANNEL_NAME) },

						async self =>
						{
							while (true)
							{
								await self.channel.ReadAsync();
								count++;
							}
						}
					)
				};
			}

			var all = Task.WhenAll(tasks).WaitForTask();

			if (count != 2)
				throw new UnittestException(string.Format("Unexpected count, expected {0} but got {1}", 2, count));
			if (all.IsFaulted || !all.IsCompleted)
				throw new UnittestException("Unexpected task state");

		}
	}
}

