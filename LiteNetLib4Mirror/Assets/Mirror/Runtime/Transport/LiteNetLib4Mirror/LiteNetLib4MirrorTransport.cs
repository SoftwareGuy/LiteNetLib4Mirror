using System;
using System.Text;
using LiteNetLib;
using LiteNetLib4Mirror.Open.Nat;
using UnityEngine;

namespace Mirror.LiteNetLib4Mirror
{
	[RequireComponent(typeof(NetworkManager))]
	public class LiteNetLib4MirrorTransport : Transport, ISegmentTransport
	{
		public static LiteNetLib4MirrorTransport Singleton;

#if UNITY_EDITOR
		[Header("Connection settings")]
#endif
		public string clientAddress = "127.0.0.1";
#if UNITY_EDITOR
		[Rename("Server IPv4 Bind Address")]
#endif
		public string serverIPv4BindAddress = "0.0.0.0";
#if !DISABLE_IPV6
#if UNITY_EDITOR
		[Rename("Server IPv6 Bind Address")]
#endif
		public string serverIPv6BindAddress = "::";
#endif
		public ushort port = 7777;
#if UNITY_EDITOR
		[Rename("Use UPnP")]
#endif
		public bool useUpnP = true;
		public ushort maxConnections = 20;

#if UNITY_EDITOR
		[ArrayRename("Channel")]
#endif
		public DeliveryMethod[] channels =
		{
			DeliveryMethod.ReliableOrdered,
			DeliveryMethod.Unreliable,
			DeliveryMethod.Sequenced,
			DeliveryMethod.ReliableSequenced,
			DeliveryMethod.ReliableUnordered
		};

#if UNITY_EDITOR
		[Header("Connection additional auth code (optional)")]
#endif
		public string authCode;

		/// <summary>Library logic update (and send) period in milliseconds</summary>
#if UNITY_EDITOR
		[Header("LiteNetLib settings")]
		[Tooltip("Library logic update (and send) period in milliseconds")]
#endif
		public int updateTime = 15;
		/// <summary>Interval for latency detection and checking connection</summary>
#if UNITY_EDITOR
		[Tooltip("Interval for latency detection and checking connection")]
#endif
		public int pingInterval = 1000;
		/// <summary>If client or server doesn't receive any packet from remote peer during this time then connection will be closed (including library internal keepalive packets)</summary>
#if UNITY_EDITOR
		[Tooltip("If client or server doesn't receive any packet from remote peer during this time then connection will be closed (including library internal keepalive packets)")]
#endif
		public int disconnectTimeout = 5000;
		/// <summary>Delay betwen connection attempts</summary>
#if UNITY_EDITOR
		[Tooltip("Delay betwen connection attempts")]
#endif
		public int reconnectDelay = 500;
		/// <summary>Maximum connection attempts before client stops and call disconnect event.</summary>
#if UNITY_EDITOR
		[Tooltip("Maximum connection attempts before client stops and call disconnect event.")]
#endif
		public int maxConnectAttempts = 10;

		/// <summary>Simulate packet loss by dropping random amout of packets. (Works only in DEBUG mode)</summary>
#if UNITY_EDITOR
		[Header("Debug connection tests")][Tooltip("Simulate packet loss by dropping random amout of packets. (Works only in DEBUG mode)")]
#endif
		public bool simulatePacketLoss;
		/// <summary>Chance of packet loss when simulation enabled. Value in percents.</summary>
#if UNITY_EDITOR
		[Range(0, 100)][Tooltip("Chance of packet loss when simulation enabled. Value in percents.")]
#endif
		public int simulationPacketLossChance = 10;
		/// <summary>Simulate latency by holding packets for random time. (Works only in DEBUG mode)</summary>
#if UNITY_EDITOR
		[Tooltip("Simulate latency by holding packets for random time. (Works only in DEBUG mode)")]
#endif
		public bool simulateLatency;
		/// <summary>Minimum simulated latency</summary>
#if UNITY_EDITOR
		[Tooltip("Minimum simulated latency")]
#endif
		public int simulationMinLatency = 30;
		/// <summary>Maximum simulated latency</summary>
#if UNITY_EDITOR
		[Tooltip("Maximum simulated latency")]
#endif
		public int simulationMaxLatency = 100;

#if UNITY_EDITOR
		[Header("Error events")]
#endif
		public UnityEventError onClientSocketError;
		public UnityEventIntError onServerSocketError;

		internal static bool Polling;
		#region Overridable methods       
		protected internal virtual string GenerateCode()
		{
			return Convert.ToBase64String(Encoding.UTF8.GetBytes(LiteNetLib4MirrorUtils.Concatenate(Application.productName, Application.companyName, Application.unityVersion, LiteNetLib4MirrorCore.TransportVersion, Singleton.authCode)));
		}

		protected internal virtual void ProcessConnectionRequest(ConnectionRequest request)
		{
			if (LiteNetLib4MirrorCore.Host.PeersCount >= maxConnections)
			{
				request.Reject();
			}
			else if (request.AcceptIfKey(LiteNetLib4MirrorServer.Code) == null)
			{
				Debug.LogWarning("Client tried to join with an invalid auth code! Current code:" + LiteNetLib4MirrorServer.Code);
			}
		}
		#endregion

		internal void InitializeTransport()
		{
			if (Singleton == null)
			{
				Singleton = this;
				LiteNetLib4MirrorCore.State = LiteNetLib4MirrorCore.States.Idle;
			}
		}

		#region Unity Functions
		private void Awake()
		{
			InitializeTransport();
		}

		private void LateUpdate()
		{
			if (Polling)
			{
				LiteNetLib4MirrorCore.Host.PollEvents();
			}
		}

		private void OnDestroy()
		{
			LiteNetLib4MirrorCore.StopTransport();
			if (LiteNetLib4MirrorUtils.LastForwardedPort != 0)
			{
				NatDiscoverer.ReleaseAll();
				LiteNetLib4MirrorUtils.LastForwardedPort = 0;
			}
		}
		#endregion

		#region Transport Overrides
		public override bool ClientConnected()
		{
			return LiteNetLib4MirrorClient.IsConnected();
		}

		public override void ClientConnect(string address)
		{
			clientAddress = address;
			LiteNetLib4MirrorClient.ConnectClient(GenerateCode());
		}

		public override bool ClientSend(int channelId, byte[] data)
		{
			return LiteNetLib4MirrorClient.Send(channels[channelId], data, 0, data.Length, (byte)channelId);
		}

		public override void ClientDisconnect()
		{
			if (!LiteNetLib4MirrorServer.IsActive())
			{
				LiteNetLib4MirrorCore.StopTransport();
			}
		}

		public override bool ServerActive()
		{
			return LiteNetLib4MirrorServer.IsActive();
		}

		public override void ServerStart()
		{
			LiteNetLib4MirrorServer.StartServer(GenerateCode());
		}

		public override bool ServerSend(int connectionId, int channelId, byte[] data)
		{
			return LiteNetLib4MirrorServer.Send(connectionId, channels[channelId], data, 0, data.Length, (byte)channelId);
		}

		public override bool ServerDisconnect(int connectionId)
		{
			return connectionId == 0 || LiteNetLib4MirrorServer.Disconnect(connectionId);
		}

		public override void ServerStop()
		{
			LiteNetLib4MirrorCore.StopTransport();
		}

		public override string ServerGetClientAddress(int connectionId)
		{
			return LiteNetLib4MirrorServer.GetClientAddress(connectionId);
		}

		public override void Shutdown()
		{
			LiteNetLib4MirrorCore.StopTransport();
		}

		public override int GetMaxPacketSize(int channelId = Channels.DefaultReliable)
		{
			return LiteNetLib4MirrorCore.GetMaxPacketSize(channels[channelId]);
		}

		public override string ToString()
		{
			return LiteNetLib4MirrorCore.GetState();
		}
		#endregion

		#region ISegmentTransport
		public bool ClientSend(int channelId, ArraySegment<byte> data)
		{
			return LiteNetLib4MirrorClient.Send(channels[channelId], data.Array, data.Offset, data.Count, (byte)channelId);
		}

		public bool ServerSend(int connectionId, int channelId, ArraySegment<byte> data)
		{
			return LiteNetLib4MirrorServer.Send(connectionId, channels[channelId], data.Array, data.Offset, data.Count, (byte)channelId);
		}
		#endregion
	}
}
