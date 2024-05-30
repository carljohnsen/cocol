﻿using System;
using System.Linq;
using CoCoL;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTest
{
	[TestClass]
	public class MixedOperationTest
	{
		[TestMethod]
		public void TestInvalidMultiAccessOperation()
		{
			TestAssert.Throws<InvalidOperationException>(() =>
			{
				try
				{
					var c1 = ChannelManager.CreateChannel<int>();
					MultiChannelAccess.ReadOrWriteAnyAsync(MultisetRequest.Read(c1), MultisetRequest.Write(1, c1)).WaitForTask().Wait();
				}
				catch (AggregateException aex)
				{
					if (aex.InnerExceptions.Count == 1)
						throw aex.InnerExceptions.First();
					throw;
				}
			});
		}

		[TestMethod]
		public void TestMultiAccessOperation()
		{
			var c1 = ChannelManager.CreateChannel<int>();
			var c2 = ChannelManager.CreateChannel<int>();

			// Copy c2 + 1 => c1
			Func<Task> p1 = async () =>
			{
				var val = await c2.ReadAsync();
				while (true)
				{
					var res = await MultiChannelAccess.ReadOrWriteAnyAsync(MultisetRequest.Read(c2), MultisetRequest.Write(val, c1));
					if (res.IsRead)
						val = res.Value + 1;
				}
			};

			// Copy c1 => c2
			Func<Task> p2 = async () =>
			{
				var val = 1;
				for (var i = 0; i < 10; i++)
				{
					await c2.WriteAsync(val);
					val = await c1.ReadAsync();
				}

				c1.Retire();
				c2.Retire();

				if (val != 10)
					throw new InvalidProgramException("Bad counter!");
			};

			// Wait for shutdown
			try
			{
				Task.WhenAll(p1(), p2()).WaitForTask().Wait();
			}
			catch (Exception ex)
			{
				// Filter out all ChannelRetired exceptions
				if (ex is AggregateException)
				{
					var rex = (from n in (ex as AggregateException).InnerExceptions
							   where !(n is RetiredException)
							   select n);

					if (rex.Count() == 1)
						throw rex.First();
					else if (rex.Count() != 0)
						throw new AggregateException(rex);
				}
				else
					throw;
			}
		}

		[TestMethod]
		public void TestMultiTypeOperation()
		{
			var c1 = ChannelManager.CreateChannel<int>();
			var c2 = ChannelManager.CreateChannel<string>();

			// Copy c2 + 1 => c1
			Func<Task> p1 = async () =>
			{
				var val = int.Parse(await c2.ReadAsync());
				while (true)
				{
					var res = await MultiChannelAccess.ReadOrWriteAnyAsync(MultisetRequest.Read(c2), MultisetRequest.Write(val, c1));
					if (res.IsRead)
						val = int.Parse((string)res.Value) + 1;
				}
			};

			// Copy c1 => c2
			Func<Task> p2 = async () =>
			{
				var val = 1;
				for (var i = 0; i < 10; i++)
				{
					await c2.WriteAsync(val.ToString());
					val = await c1.ReadAsync();
				}

				c1.Retire();
				c2.Retire();

				if (val != 10)
					throw new InvalidProgramException("Bad counter!");
			};

			// Wait for shutdown
			try
			{
				Task.WhenAll(p1(), p2()).Wait();
			}
			catch (Exception ex)
			{
				// Filter out all ChannelRetired exceptions
				if (ex is AggregateException)
				{
					var rex = (from n in (ex as AggregateException).InnerExceptions
							   where !(n is RetiredException)
							   select n);

					if (rex.Count() == 1)
						throw rex.First();
					else if (rex.Count() != 0)
						throw new AggregateException(rex);
				}
				else
					throw;
			}
		}

		[TestMethod]
		public void TestMultiTypeReadWrite()
		{
			var c1 = ChannelManager.CreateChannel<int>();
			var c2 = ChannelManager.CreateChannel<string>();
			var c3 = ChannelManager.CreateChannel<long>();

			c1.WriteNoWait(1);
			c2.WriteNoWait("2");
			c3.WriteNoWait(3);

			// Using explicit .AsUntyped() calls
			var r = MultiChannelAccess.ReadFromAnyAsync(c1.AsUntyped(), c2.AsUntyped(), c3.AsUntyped()).WaitForTask().Result;
			if (r == null)
				throw new UnittestException("Unexpected null result");
			if (r.Channel != c1)
				throw new UnittestException("Unexpected read channel");

			if (!(r.Value is int))
				throw new UnittestException("Priority changed?");
			if ((int)r.Value != 1)
				throw new UnittestException("Bad value?");

			// Using explicit .RequestRead() calls
			r = MultiChannelAccess.ReadFromAnyAsync(c1.RequestRead(), c2.RequestRead(), c3.RequestRead()).WaitForTask().Result;
			if (r == null)
				throw new UnittestException("Unexpected null result");
			if (r.Channel != c2)
				throw new UnittestException("Unexpected read channel");
			if (!(r.Value is string))
				throw new UnittestException("Priority changed?");
			if ((string)r.Value != "2")
				throw new UnittestException("Bad value?");

			// Using channels directly
			r = MultiChannelAccess.ReadFromAnyAsync(c1, c2, c3).WaitForTask().Result;
			if (r == null)
				throw new UnittestException("Unexpected null result");
			if (r.Channel != c3)
				throw new UnittestException("Unexpected read channel");

			if (!(r.Value is long))
				throw new UnittestException("Priority changed?");
			if ((long)r.Value != 3)
				throw new UnittestException("Bad value?");

			// Writing with untyped channel request
			var t = new[] { c1.AsUntyped().RequestWrite(4) }.WriteToAnyAsync();
			if (c1.Read() != 4)
				throw new UnittestException("Bad value?");

			t.WaitForTask().Wait();

			// Writing with a typed write request, using mixed types
			var t2 = MultiChannelAccess.WriteToAnyAsync(c1.RequestWrite(5), c2.RequestWrite("6"));
			if (c1.Read() != 5)
				throw new UnittestException("Bad value?");

			t2.WaitForTask().Wait();
		}
	}
}

