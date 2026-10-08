using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using LibreKO.Network;
using Xunit;

namespace LibreKO.Tests;

public class FramedConnLifecycleTests
{
    private sealed class PausedConnection : FramedConn
    {
        public readonly TaskCompletionSource OldDecoded = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public readonly TaskCompletionSource OldReleased = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public readonly ManualResetEventSlim Release = new();

        protected override Packet? BuildIncoming(byte[] body)
        {
            if (body[0] == 0x41)
            {
                OldDecoded.TrySetResult();
                if (!Release.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException();
                OldReleased.TrySetResult();
            }
            return base.BuildIncoming(body);
        }
    }

    private static async Task WaitUntil(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (!condition()) await Task.Delay(5, timeout.Token);
    }

    private static async Task Send(NetworkStream stream, byte opcode)
    {
        var frame = new byte[] { 0xaa, 0x55, 0, 0, opcode, 0x55, 0xaa };
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(2), 1);
        await stream.WriteAsync(frame);
    }

    [Fact]
    public async Task CloseDiscardsDecodedPacketsWaitingForTheMainThread()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
        var connection = new FramedConn();
        try
        {
            connection.Connect("127.0.0.1", ((IPEndPoint)listener.LocalEndpoint).Port);
            using var peer = await listener.AcceptTcpClientAsync();
            await WaitUntil(() => connection.Connected);
            await Send(peer.GetStream(), 0x42);
            await WaitUntil(() => connection.Incoming.Count == 1);
            connection.Close();
            Assert.False(connection.Connected);
            Assert.Empty(connection.Incoming);
        }
        finally { connection.Close(); }
    }

    [Fact]
    public async Task AnOldDecoderCannotEnqueueIntoAReplacementConnection()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
        var connection = new PausedConnection();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        try
        {
            connection.Connect("127.0.0.1", port);
            using var oldPeer = await listener.AcceptTcpClientAsync();
            await WaitUntil(() => connection.Connected);
            await Send(oldPeer.GetStream(), 0x41);
            await connection.OldDecoded.Task.WaitAsync(TimeSpan.FromSeconds(10));
            connection.Connect("127.0.0.1", port);
            using var newPeer = await listener.AcceptTcpClientAsync();
            await WaitUntil(() => connection.Connected);
            connection.Release.Set();
            await connection.OldReleased.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await Send(newPeer.GetStream(), 0x42);
            await WaitUntil(() => !connection.Incoming.IsEmpty);
            // Receiving the new packet proves the replacement receiver is running after the old decoder resumes.
            Assert.True(connection.Incoming.TryDequeue(out var packet));
            Assert.Equal(0x42, packet!.GetOpcode());
            Assert.Empty(connection.Incoming);
            Assert.True(connection.Connected);
        }
        finally { connection.Release.Set(); connection.Close(); }
    }
}
