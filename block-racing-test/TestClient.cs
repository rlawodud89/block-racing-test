using System.Net.Sockets;
using block_racing_common.Network;
using block_racing_common.Network.Packets;
using block_racing_common.Game.Enums;

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

    private readonly TaskCompletionSource<bool>
        _matchTcs =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

    private readonly TaskCompletionSource<bool>
        _gameStartTcs =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

    private readonly TaskCompletionSource<bool>
    _gameEndTcs =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public bool GameEnded =>
        _gameEndTcs.Task.IsCompletedSuccessfully;

    // 같은 Client에서 여러 Send가 동시에 발생하는 것을 방지
    private readonly SemaphoreSlim _sendLock = new(1, 1);

    public int ClientId { get; }

    public bool Matched =>
        _matchTcs.Task.IsCompletedSuccessfully;

    public bool GameStarted =>
        _gameStartTcs.Task.IsCompletedSuccessfully;

    public bool LoginSucceeded =>
        _loginTcs.Task.IsCompletedSuccessfully;

    // 성능 테스트 시 패킷 로그를 끌 수 있음
    public bool EnablePacketLog { get; set; } = true;

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
        C_LoginPacket packet = new();

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

    // =========================================================
    // Game Start
    // =========================================================

    public async Task<bool> WaitForGameStartAsync(
        TimeSpan timeout)
    {
        try
        {
            return await _gameStartTcs.Task.WaitAsync(timeout);
        }
        catch (TimeoutException)
        {
            Console.WriteLine(
                $"[Client {ClientId}] " +
                $"Game Start TIMEOUT");

            return false;
        }
    }

    // =========================================================
    // Game End
    // =========================================================

    public async Task<bool> WaitForGameEndAsync(
    TimeSpan timeout)
    {
        try
        {
            return await _gameEndTcs.Task.WaitAsync(timeout);
        }
        catch (TimeoutException)
        {
            Console.WriteLine(
                $"[Client {ClientId}] " +
                $"Game End TIMEOUT");

            return false;
        }
    }

    // =========================================================
    // Input
    // =========================================================

    public async Task SendInputAsync(
        InputType inputType)
    {
        C_InputPacket packet = new()
        {
            InputType = inputType
        };

        await SendAsync(packet);
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
                    await ProcessPacketAsync(packet);
                }
            }
        }
        catch (ObjectDisposedException)
        {
            // 정상적인 Disconnect 과정에서 발생 가능
        }
        catch (IOException)
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
                $"Receive Error: {ex}");
        }
    }

    private async Task ProcessPacketAsync(byte[] data)
    {
        PacketReader reader =
            new(data);

        // Length
        _ = reader.ReadUInt16();

        ushort packetId =
            reader.ReadUInt16();

        PacketId id =
            (PacketId)packetId;

        if (EnablePacketLog)
        {
            Console.WriteLine(
                $"[Client {ClientId}] " +
                $"Receive Packet={id}");
        }

        switch (id)
        {
            case PacketId.S_Login:
                HandleLogin(reader);
                break;

            case PacketId.S_RoomReady:
                await HandleRoomReadyAsync(reader);
                break;

            case PacketId.S_StartGame:
                HandleStartGame(reader);
                break;

            case PacketId.S_GameCanceled:
                HandleGameCanceled(reader);
                break;

            case PacketId.S_Heartbeat:
                await HandleHeartbeatAsync();
                break;

            case PacketId.S_GameEnd:
                HandleGameEnd(reader);
                break;

            default:
                break;
        }
    }

    // =========================================================
    // Packet Handlers
    // =========================================================


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

    private async Task HandleRoomReadyAsync(
        PacketReader reader)
    {
        S_RoomReadyPacket packet = new();

        packet.Read(reader);

        Console.WriteLine(
            $"[Client {ClientId}] " +
            $"ROOM READY Room={packet.RoomId}");

        _matchTcs.TrySetResult(true);

        await SendReadyAsync();
    }

    private void HandleStartGame(
        PacketReader reader)
    {
        S_StartGamePacket packet = new();

        packet.Read(reader);

        Console.WriteLine(
            $"[Client {ClientId}] " +
            $"GAME START " +
            $"Countdown={packet.StartTick}s");

        _gameStartTcs.TrySetResult(true);
    }

    private void HandleGameCanceled(
        PacketReader reader)
    {
        Console.WriteLine(
            $"[Client {ClientId}] " +
            $"Match canceled");
    }

    private async Task HandleHeartbeatAsync()
    {
        await SendAsync(new C_HeartbeatPacket());
    }

    private void HandleGameEnd(
    PacketReader reader)
    {
        S_GameEndPacket packet = new();

        packet.Read(reader);

        Console.WriteLine(
            $"[Client {ClientId}] " +
            $"GAME END Result={packet.Result}");

        _gameEndTcs.TrySetResult(true);
    }

    // =========================================================
    // Ready
    // =========================================================

    private async Task SendReadyAsync()
    {
        C_ReadyPacket packet = new();

        await SendAsync(packet);

        Console.WriteLine(
            $"[Client {ClientId}] Ready sent");
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

        await _sendLock.WaitAsync();

        try
        {
            if (_stream == null)
            {
                throw new InvalidOperationException(
                    "Not connected.");
            }

            await _stream.WriteAsync(data);
        }
        finally
        {
            _sendLock.Release();
        }
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
            // 종료 과정에서 발생하는 예외 무시
        }
        finally
        {
            _stream = null;
            _client = null;
            _receiveTask = null;
        }
    }
}