﻿using System;
using CoCoL;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;
using CoCoL.Network;

namespace StressedAlt
{
	/// <summary>
	/// The stressed reader performs a fair read from the set of channels
	/// </summary>
	class Reader
	{
		/// <summary>
		/// The number of reads to do before counting
		/// </summary>
		private const int WARMUP_ROUNDS = 10;

		/// <summary>
		/// The number of reads to perform from each channel in each round
		/// </summary>
		private const int MEASURE_ROUNDS = 100;

		/// <summary>
		/// The number of repeated rounds
		/// </summary>
		private const int TOTAL_ROUNDS = 10;

		/// <summary>
		/// The set of channels to read from
		/// </summary>
		private readonly MultiChannelSetRead<long> m_set;

		/// <summary>
		/// The number of channels
		/// </summary>
		private readonly int m_channelCount;

		/// <summary>
		/// The number of writes pr. channel
		/// </summary>
		private readonly int m_writes_pr_channel;

		/// <summary>
		/// A tracking dictionary to verify correctness
		/// </summary>
		private readonly Dictionary<long, long> m_tracking;

		/// <summary>
		/// The number of tracked reads
		/// </summary>
		private long m_tracked_reads = 0;

		/// <summary>
		/// A value indicating if errors were detected
		/// </summary>
		private bool m_has_errors = false;

		public Reader(IEnumerable<IReadChannel<long>> channels, int writes_pr_channel)
		{
			m_set = new MultiChannelSetRead<long>(channels, MultiChannelPriority.Fair);
			m_tracking = new Dictionary<long, long>();
			m_channelCount = m_set.Channels.Count();
			m_writes_pr_channel = writes_pr_channel;
		}

		public void Run()
		{
			RunAsync().WaitForTaskOrThrow();
		}

		public async Task RunAsync()
		{
			try
			{
				Console.WriteLine("Running {0} warmup rounds ...", WARMUP_ROUNDS);

				var readcount = m_writes_pr_channel * m_channelCount;

				// Measure the warmup
				var startWarmup = DateTime.Now;

				for (var i = 0; i < WARMUP_ROUNDS; i++)
					for (var j = 0; j < readcount; j++)
						UpdateTracking((await m_set.ReadFromAnyAsync()).Value);

				if (m_has_errors)
				{
					Console.WriteLine("Errors detected during warmup, quitting...");
					throw new System.IO.InvalidDataException("Errors detected");
				}

				var expected = ((DateTime.Now - startWarmup).Ticks / WARMUP_ROUNDS) * MEASURE_ROUNDS * TOTAL_ROUNDS;

				Console.WriteLine("Measuring {0} rounds, expected completion around: {1}", MEASURE_ROUNDS, DateTime.Now.AddTicks(expected));

				for(var r = 0; r < TOTAL_ROUNDS; r++)
				{
					var startMeasure = DateTime.Now;

					// Just keep reading
					for (var i = 0; i < MEASURE_ROUNDS * readcount; i++)
						await m_set.ReadFromAnyAsync();
					
					var elapsed = DateTime.Now - startMeasure;

					Console.WriteLine("Performed {0}x{1} priority alternation reads in {2}", MEASURE_ROUNDS, readcount, elapsed);
					Console.WriteLine("Communication time is {0} microseconds", (elapsed.TotalMilliseconds * 1000) / (MEASURE_ROUNDS * readcount));
				}
			}
			catch(Exception ex)
			{
				if (!ex.IsRetiredException())
				{
					Console.WriteLine("Unexpected exception: {0}", ex);
					throw;
				}
			}
			finally
			{
				m_set.Retire();
			}
		}

		private void UpdateTracking(long value)
		{
			// Keep track of correctness
			long c;
			if (!m_tracking.TryGetValue(value, out c))
				m_tracking[value] = 1;
			else
				m_tracking[value] = c + 1;

			m_tracked_reads++;

			if ((m_tracked_reads % (m_channelCount * m_writes_pr_channel)) == 0)
			{
				var counts = m_tracking.OrderBy(x => x.Value);
				if (Math.Abs(counts.Last().Value - counts.First().Value) > 1)
				{
					m_has_errors = true;
					Console.WriteLine("Error in fair alternation, diff: {0}", counts.Last().Value - counts.First().Value);
				}
			}
		}
	}

	public class Config
	{
		/// <summary>
		/// The number of channels to use
		/// </summary>
		[CommandlineOption("The number of channels", longname: "channels")]
		public static int Channels = 200;

		/// <summary>
		/// The number of writers for each channel
		/// </summary>
		[CommandlineOption("The number of writers for each channel", longname: "writers")]
		public static int Writers = 100;

		/// <summary>
		/// A value indicating if the channels should be network based
		/// </summary>
		[CommandlineOption("Indicates if the channels are network hosted", longname: "network")]
		public static bool NetworkedChannels = false;

		/// <summary>
		/// The size of the latency hiding buffer used on network channels
		/// </summary>
		[CommandlineOption("The buffer size for network channels", longname: "buffersize")]
		public static int NetworkChannelLatencyBufferSize = 0;

		/// <summary>
		/// The hostname for the channel server
		/// </summary>
		[CommandlineOption("The hostname for the channel server", longname: "host")]
		public static string ChannelServerHostname = "localhost";

		/// <summary>
		/// The port for the channel server
		/// </summary>
		[CommandlineOption("The port for the channel server", longname: "port")]
		public static int ChannelServerPort = 8888;

		/// <summary>
		/// A value indicating if the channel server is on the local host
		/// </summary>
		[CommandlineOption("Indicates if the process hosts a server itself", longname: "selfhost")]
		public static bool ChannelServerSelfHost = true;

		/// <summary>
		/// Parses the commandline args
		/// </summary>
		/// <param name="args">The commandline arguments.</param>
		public static bool Parse(string[] args)
		{
			return SettingsHelper.Parse<Config>(args.ToList(), null);
		}

		/// <summary>
		/// Returns the config object as a human readable string
		/// </summary>
		/// <returns>The string.</returns>
		public static string AsString()
		{
			return string.Join(", ", typeof(Config).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static).Select(x => string.Format("{0}={1}", x.Name, x.GetValue(null))));
		}
	}

	public class MainClass
	{
		/// <summary>
		/// Wraps the channel with a latency hiding instance, if required by config
		/// </summary>
		/// <returns>The buffered write channel.</returns>
		/// <param name="input">The input channel.</param>
		private static IWriteChannel<T> AsBufferedWrite<T>(IWriteChannel<T> input)
		{
			if (Config.NetworkChannelLatencyBufferSize != 0 && input is NetworkChannel<T> && Config.NetworkedChannels)
				return new LatencyHidingWriter<T>(input, Config.NetworkChannelLatencyBufferSize);
			return input;
		}

		/// <summary>
		/// Wraps the channel with a latency hidign instance, if required by config
		/// </summary>
		/// <returns>The buffered read channel.</returns>
		/// <param name="input">The input channel.</param>
		private static IReadChannel<T> AsBufferedRead<T>(IReadChannel<T> input)
		{
			if (Config.NetworkChannelLatencyBufferSize != 0 && input is NetworkChannel<T> && Config.NetworkedChannels)
				return new LatencyHidingReader<T>(input, Config.NetworkChannelLatencyBufferSize);
			return input;
		}

		/// <summary>
		/// Debug counter to check for hanging writers
		/// </summary>
		internal static int WriterCount = 0;

		/// <summary>
		/// Runs the writer process
		/// </summary>
		/// <param name="id">The id to write into the channel.</param>
		/// <param name="channel">The channel to write into.</param>
		private static async Task RunWriterAsync(long id, IWriteChannel<long> channel)
		{
			Interlocked.Increment(ref WriterCount);
			try
			{
				while (true)
					await channel.WriteAsync(id);
			}
			catch (Exception ex)
			{
				if (!ex.IsRetiredException())
				{
					Console.WriteLine("Unexpected exception: {0}", ex);
					throw;
				}
			}
			finally
			{
				Interlocked.Decrement(ref WriterCount);
				channel.Retire();
			}
		}


		public static void Main(string[] args)
		{
			if (!Config.Parse(args))
				return;

			Console.WriteLine("Config is: {0}", Config.AsString());

			var servertoken = new CancellationTokenSource();
			var server = (Config.NetworkedChannels && Config.ChannelServerSelfHost) ? NetworkChannelServer.HostServer(servertoken.Token, Config.ChannelServerHostname, Config.ChannelServerPort) : null;

			if (Config.NetworkedChannels && !Config.ChannelServerSelfHost)
				NetworkConfig.Configure(Config.ChannelServerHostname, Config.ChannelServerPort, true);
			
			using (Config.NetworkedChannels ? new NetworkChannelScope(redirectunnamed: true) : null)
			{
				Console.WriteLine("Running with {0} channels and {1} writers, a total of {2} communications pr. round", Config.Channels, Config.Writers, Config.Channels * Config.Writers);

				var allchannels = (from n in Enumerable.Range(0, Config.Channels)
				                  select ChannelManager.CreateChannel<long>()).ToArray();

				var tasks = new List<Task>();
				for (var i = 0; i < allchannels.Length; i++)
					for (var j = 0; j < Config.Writers; j++)
						tasks.Add(RunWriterAsync(i, AsBufferedWrite(allchannels[i])));

				tasks.Add(new Reader(allchannels.Select(x => AsBufferedRead(x)).ToArray(), Config.Writers).RunAsync());

				Task.WhenAll(tasks).WaitForTaskOrThrow();

				Console.WriteLine("All tasks completed");
			}

			servertoken.Cancel();
			if (server != null)
				server.WaitForTaskOrThrow();

			if (WriterCount != 0)
				Console.WriteLine("Terminating, with {0} writers active", WriterCount);
		}
	}
}
