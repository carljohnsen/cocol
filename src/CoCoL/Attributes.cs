﻿using System;

namespace CoCoL
{
	/// <summary>
	/// Attribute that indicates that this class is a process, and indicates how many instances should be started
	/// </summary>
	[AttributeUsage(AttributeTargets.Class)]
	public class ProcessAttribute : Attribute
	{
		/// <summary>
		/// The number of processes
		/// </summary>
		public readonly long ProcessCount;

		/// <summary>
		/// Initializes a new instance of the <see cref="CoCoL.ProcessAttribute"/> class.
		/// </summary>
		/// <param name="count">The number of processes to launch.</param>
		public ProcessAttribute(long count = 1)
		{
			ProcessCount = count;
		}
	}

	/// <summary>
	/// Enumeration for choosing the name scope
	/// </summary>
	public enum ChannelNameScope
	{
		/// <summary>
		/// Use the current local name scope
		/// </summary>
		Local,
		/// <summary>
		/// Use the current parent name scope
		/// </summary>
		Parent,
		/// <summary>
		/// Use the global name scope
		/// </summary>
		Global
	}

	/// <summary>
	/// Attribute for naming a channel in automatic wireup
	/// </summary>
	[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
	public class ChannelNameAttribute : Attribute
	{
		/// <summary>
		/// The name of the channel
		/// </summary>
		public string Name { get; set; }

		/// <summary>
		/// The buffer size of the channel
		/// </summary>
		public readonly int BufferSize;

		/// <summary>
		/// The target scope.
		/// </summary>
		public readonly ChannelNameScope TargetScope;

		/// <summary>
		/// The maximum number of pending readers
		/// </summary>
		public readonly int MaxPendingReaders;

		/// <summary>
		/// The maximum number of pendinger writers
		/// </summary>
		public readonly int MaxPendingWriters;

		/// <summary>
		/// The strategy for selecting pending readers to discard on overflow
		/// </summary>
		public readonly QueueOverflowStrategy PendingReadersOverflowStrategy;

		/// <summary>
		/// The strategy for selecting pending readers to discard on overflow
		/// </summary>
		public readonly QueueOverflowStrategy PendingWritersOverflowStrategy;

		/// <summary>
		/// Initializes a new instance of the <see cref="CoCoL.ChannelNameAttribute"/> class.
		/// </summary>
		/// <param name="name">The name of the channel.</param>
		/// <param name="buffersize">The size of the buffer on the created channel</param>
		/// <param name="targetScope">The scope where the channel is created</param>
		/// <param name="maxPendingReaders">The maximum number of pending readers. A negative value indicates infinite</param>
		/// <param name="maxPendingWriters">The maximum number of pending writers. A negative value indicates infinite</param>
		/// <param name="pendingReadersOverflowStrategy">The strategy for dealing with overflow for read requests</param>
		/// <param name="pendingWritersOverflowStrategy">The strategy for dealing with overflow for write requests</param>
		public ChannelNameAttribute(string name, int buffersize = 0, ChannelNameScope targetScope = ChannelNameScope.Local, int maxPendingReaders = -1, int maxPendingWriters = -1, QueueOverflowStrategy pendingReadersOverflowStrategy = QueueOverflowStrategy.Reject, QueueOverflowStrategy pendingWritersOverflowStrategy = QueueOverflowStrategy.Reject)
		{
			Name = name;
			BufferSize = buffersize;
			TargetScope = targetScope;
			MaxPendingReaders = maxPendingReaders;
			MaxPendingWriters = maxPendingWriters;
			PendingReadersOverflowStrategy = pendingReadersOverflowStrategy;
			PendingWritersOverflowStrategy = pendingWritersOverflowStrategy;
		}
	}

	/// <summary>
	/// Attribute for naming a channel in automatic wireup
	/// </summary>
	[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
	public class BroadcastChannelNameAttribute : ChannelNameAttribute
	{
		/// <summary>
		/// The minimum number of readers required for a broadcast to be performed
		/// </summary>
		public readonly int InitialBarrierSize;
		/// <summary>
		/// The minimum number of readers required for the first broadcast to be performed
		/// </summary>
		public readonly int MinimumReaders;

		/// <summary>
		/// Initializes a new instance of the <see cref="CoCoL.BroadcastChannelNameAttribute"/> class.
		/// </summary>
		/// <param name="name">The name of the channel.</param>
		/// <param name="buffersize">The size of the buffer on the created channel</param>
		/// <param name="targetScope">The scope where the channel is created</param>
		/// <param name="maxPendingReaders">The maximum number of pending readers. A negative value indicates infinite</param>
		/// <param name="maxPendingWriters">The maximum number of pending writers. A negative value indicates infinite</param>
		/// <param name="pendingReadersOverflowStrategy">The strategy for dealing with overflow for read requests</param>
		/// <param name="pendingWritersOverflowStrategy">The strategy for dealing with overflow for write requests</param>
		/// <param name="initialBarrierSize">The number of readers required on the channel before sending the first broadcast</param>
		/// <param name="minimumReaders">The minimum number of readers required on the channel, before a broadcast can be performed</param>
		public BroadcastChannelNameAttribute(string name, int buffersize = 0, ChannelNameScope targetScope = ChannelNameScope.Local, int maxPendingReaders = -1, int maxPendingWriters = -1, QueueOverflowStrategy pendingReadersOverflowStrategy = QueueOverflowStrategy.Reject, QueueOverflowStrategy pendingWritersOverflowStrategy = QueueOverflowStrategy.Reject, int initialBarrierSize = -1, int minimumReaders = -1)
			: base(name, buffersize, targetScope, maxPendingReaders, maxPendingWriters, pendingReadersOverflowStrategy, pendingWritersOverflowStrategy)
		{
			InitialBarrierSize = initialBarrierSize;
			MinimumReaders = minimumReaders;
		}
	}


}

