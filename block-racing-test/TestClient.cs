using System.Net.Sockets;
using block_racing_common.Network;
using block_racing_common.Network.Packets;

namespace block_racing_test;

public class TestClient
{
    private readonly string _host;
    private readonly int _port;

    private TcpClient? _client;
    private NetworkStream? _stream;

    private readonly ReceiveBuffer _receiveBuffer = new();

    private Task? _receiveTask;

    private readonly TaskCompletionSource<bool>
        _loginTcs =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

    private TaskCompletionSource<bool>
        _matchTcs =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

    public int ClientId { get; }

    public bool Matched =>
        _matchTcs.Task.IsCompletedSuccessfully;

    public TestClient(
        int clientId,
        string host,
        int port)
    {
        ClientId = clientId;
        _host = host;
        _port = port;
    }

    // =========================================================
    // Connect / Login
    // =========================================================

    public async Task ConnectAndLoginAsync()
    {
        _client = new TcpClient();

        await _client.ConnectAsync(
            _host,
            _port);

        _stream = _client.GetStream();

        Console.WriteLine(
            $"[Client {ClientId}] Connected");

        _receiveTask = ReceiveLoopAsync();

        await SendLoginAsync();

        bool loginSuccess =
            await _loginTcs.Task.WaitAsync(
                TimeSpan.FromSeconds(5));

        if (!loginSuccess)
        {
            throw new Exception(
                $"Client {ClientId} Login failed.");
        }

        Console.WriteLine(
            $"[Client {ClientId}] Login OK");
    }

    private async Task SendLoginAsync()
    {
        C_LoginPacket packet = new()
        {
            Nickname = $"TestPlayer_{ClientId}"
        };

        await SendAsync(packet);

        Console.WriteLine(
            $"[Client {ClientId}] Login sent");
    }

    // =========================================================
    // Match
    // =========================================================

    public async Task RequestMatchAsync()
    {
        C_MatchRequestPacket packet = new()
        {
            IsMatch = true
        };

        await SendAsync(packet);

        Console.WriteLine(
            $"[Client {ClientId}] MatchRequest sent");
    }

    public async Task<bool> WaitForMatchAsync(
        TimeSpan timeout)
    {
        try
        {
            return await _matchTcs.Task.WaitAsync(timeout);
        }
        catch (TimeoutException)
        {
            Console.WriteLine(
                $"[Client {ClientId}] Match TIMEOUT");

            return false;
        }
    }

    public void ResetMatchState()
    {
        _matchTcs =
            new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
    }

    // =========================================================
    // Connection 유지
    // =========================================================

    public async Task KeepConnectedAsync(
        TimeSpan duration)
    {
        Console.WriteLine(
            $"[Client {ClientId}] " +
            $"Keeping connection for " +
            $"{duration.TotalSeconds:F0}s");

        await Task.Delay(duration);

        Console.WriteLine(
            $"[Client {ClientId}] " +
            $"Keep duration finished");
    }

    // =========================================================
    // Receive
    // =========================================================

    private async Task ReceiveLoopAsync()
    {
        if (_stream == null)
            return;

        byte[] buffer = new byte[1024];

        try
        {
            while (true)
            {
                int received =
                    await _stream.ReadAsync(buffer);

                if (received == 0)
                    break;

                _receiveBuffer.Append(
                    buffer,
                    received);

                while (
                    _receiveBuffer.TryReadPacket(
                        out byte[] packet))
                {
                    ProcessPacket(packet);
                }
            }
        }
        catch (ObjectDisposedException)
        {
            // 정상적인 Disconnect 과정에서 발생 가능
        }
        catch (SocketException)
        {
            // 정상적인 Disconnect 과정에서 발생 가능
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                $"[Client {ClientId}] " +
                $"Receive Error: {ex.Message}");
        }
    }

    private void ProcessPacket(byte[] data)
    {
        PacketReader reader =
            new(data);

        ushort length =
            reader.ReadUInt16();

        ushort packetId =
            reader.ReadUInt16();

        PacketId id =
            (PacketId)packetId;

        Console.WriteLine(
            $"[Client {ClientId}] " +
            $"Receive Packet={id}");

        switch (id)
        {
            case PacketId.S_Login:
                HandleLogin(reader);
                break;

            case PacketId.S_RoomReady:
                HandleMatchFound(reader);
                break;

            case PacketId.S_GameCanceled:
                Console.WriteLine(
                    $"[Client {ClientId}] " +
                    $"Match canceled");
                break;

            default:
                break;
        }
    }

    private void HandleLogin(
        PacketReader reader)
    {
        S_LoginPacket packet = new();

        packet.Read(reader);

        Console.WriteLine(
            $"[Client {ClientId}] " +
            $"Login Response " +
            $"PlayerId={packet.PlayerId}");

        _loginTcs.TrySetResult(true);
    }

    private void HandleMatchFound(
        PacketReader reader)
    {
        S_RoomReadyPacket packet = new();

        packet.Read(reader);

        Console.WriteLine(
            $"[Client {ClientId}] " +
            $"MATCH FOUND Room={packet.RoomId}");

        _matchTcs.TrySetResult(true);
    }

    // =========================================================
    // Send
    // =========================================================

    private async Task SendAsync(
        IPacket packet)
    {
        if (_stream == null)
        {
            throw new InvalidOperationException(
                "Not connected.");
        }

        PacketWriter writer =
            new((ushort)packet.PacketId);

        packet.Write(writer);

        byte[] data =
            writer.ToArray();

        await _stream.WriteAsync(data);
    }

    // =========================================================
    // Disconnect
    // =========================================================

    public async Task DisconnectAsync()
    {
        try
        {
            if (_stream != null)
            {
                await _stream.DisposeAsync();
            }

            _client?.Close();

            if (_receiveTask != null)
            {
                try
                {
                    await _receiveTask;
                }
                catch
                {
                    // 종료 과정에서 발생하는 예외 무시
                }
            }
        }
        catch
        {
        }

        _stream = null;
        _client = null;
        _receiveTask = null;
    }
}