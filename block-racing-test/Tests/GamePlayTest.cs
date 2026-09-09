using block_racing_common.Game.Enums;

namespace block_racing_test;

public static class GamePlayTest
{
    public static async Task RunAsync()
    {
        Console.WriteLine();
        Console.WriteLine("=================================");
        Console.WriteLine(" GAME INPUT LOAD TEST");
        Console.WriteLine("=================================");

        Console.WriteLine(
            $"Clients : {TestConfig.BatchClientCount}");

        Console.WriteLine(
            $"Expected Rooms : " +
            $"{TestConfig.BatchClientCount / 2}");

        Console.WriteLine(
            $"Test Count : " +
            $"{TestConfig.BatchTestCount}");

        Console.WriteLine(
            $"Game End Timeout : " +
            $"{TestConfig.GameEndTimeoutSeconds}s");

        Console.WriteLine(
            $"Input Interval : " +
            $"{TestConfig.InputIntervalMilliseconds}ms");

        Console.WriteLine();

        int totalConnectSuccess = 0;
        int totalConnectFail = 0;

        int totalMatchSuccess = 0;
        int totalMatchFail = 0;

        int totalGameStartSuccess = 0;
        int totalGameStartFail = 0;

        long totalInputSuccess = 0;
        long totalInputFail = 0;

        for (int test = 1;
             test <= TestConfig.BatchTestCount;
             test++)
        {
            Console.WriteLine(
                $"========== TEST {test}/" +
                $"{TestConfig.BatchTestCount} ==========");

            List<TestClient> clients = [];
            List<TestClient> connectedClients = [];

            try
            {
                // =================================================
                // 1. Client 생성
                // =================================================

                for (int i = 0;
                     i < TestConfig.BatchClientCount;
                     i++)
                {
                    TestClient client =
                        new(
                            i + 1,
                            TestConfig.Host,
                            TestConfig.Port)
                        {
                            EnablePacketLog = false
                        };

                    clients.Add(client);
                }

                // =================================================
                // 2. 동시 접속 + Login
                // =================================================

                Console.WriteLine(
                    "[TEST] Connecting clients...");

                Task<bool>[] connectTasks =
                    clients
                        .Select(
                            async client =>
                            {
                                try
                                {
                                    await client.ConnectAndLoginAsync();

                                    return true;
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine(
                                        $"[Client {client.ClientId}] " +
                                        $"Connect failed: {ex.Message}");

                                    return false;
                                }
                            })
                        .ToArray();

                bool[] connectResults =
                    await Task.WhenAll(connectTasks);

                for (int i = 0;
                     i < clients.Count;
                     i++)
                {
                    if (connectResults[i])
                        connectedClients.Add(clients[i]);
                }

                int connectSuccess =
                    connectedClients.Count;

                int connectFail =
                    clients.Count - connectSuccess;

                totalConnectSuccess += connectSuccess;
                totalConnectFail += connectFail;

                Console.WriteLine();
                Console.WriteLine(
                    $"[CONNECT RESULT] " +
                    $"Success={connectSuccess}, " +
                    $"Fail={connectFail}");

                if (connectedClients.Count < 2)
                {
                    Console.WriteLine(
                        "[TEST] Not enough connected clients.");

                    continue;
                }

                // =================================================
                // 3. 동시 Match Request
                // =================================================

                Console.WriteLine();
                Console.WriteLine(
                    "[TEST] Sending MatchRequests...");

                await SendMatchRequestsSimultaneouslyAsync(
                    connectedClients);

                // =================================================
                // 4. Match 결과 대기
                // =================================================

                Console.WriteLine(
                    "[TEST] Waiting for matchmaking...");

                Task<bool>[] matchTasks =
                    connectedClients
                        .Select(
                            client =>
                                client.WaitForMatchAsync(
                                    TimeSpan.FromSeconds(
                                        TestConfig.MatchTimeoutSeconds)))
                        .ToArray();

                bool[] matchResults =
                    await Task.WhenAll(matchTasks);

                int matchSuccess =
                    matchResults.Count(result => result);

                int matchFail =
                    matchResults.Length - matchSuccess;

                totalMatchSuccess += matchSuccess;
                totalMatchFail += matchFail;

                Console.WriteLine();
                Console.WriteLine(
                    $"[MATCH RESULT] " +
                    $"Success={matchSuccess}, " +
                    $"Fail={matchFail}");

                PrintFailedClients(
                    connectedClients,
                    matchResults,
                    "MATCH");

                // =================================================
                // 5. 게임 시작 확인
                // =================================================

                List<TestClient> matchedClients = [];

                for (int i = 0;
                     i < connectedClients.Count;
                     i++)
                {
                    if (matchResults[i])
                        matchedClients.Add(
                            connectedClients[i]);
                }

                Console.WriteLine();
                Console.WriteLine(
                    "[TEST] Waiting for game start...");

                Task<bool>[] gameStartTasks =
                    matchedClients
                        .Select(
                            client =>
                                client.WaitForGameStartAsync(
                                    TimeSpan.FromSeconds(
                                        TestConfig.MatchTimeoutSeconds)))
                        .ToArray();

                bool[] gameStartResults =
                    await Task.WhenAll(gameStartTasks);

                int gameStartSuccess =
                    gameStartResults.Count(result => result);

                int gameStartFail =
                    gameStartResults.Length -
                    gameStartSuccess;

                totalGameStartSuccess +=
                    gameStartSuccess;

                totalGameStartFail +=
                    gameStartFail;

                Console.WriteLine(
                    $"[GAME START RESULT] " +
                    $"Success={gameStartSuccess}, " +
                    $"Fail={gameStartFail}");

                PrintFailedClients(
                    matchedClients,
                    gameStartResults,
                    "GAME START");

                // =================================================
                // 6. 실제 게임 Input 테스트
                // =================================================

                List<TestClient> gameClients = [];

                for (int i = 0;
                     i < matchedClients.Count;
                     i++)
                {
                    if (gameStartResults[i])
                        gameClients.Add(
                            matchedClients[i]);
                }

                if (gameClients.Count == 0)
                {
                    Console.WriteLine(
                        "[TEST] No clients available " +
                        "for input test.");

                    continue;
                }

                Console.WriteLine();
                Console.WriteLine(
                    $"[TEST] Starting game input test " +
                    $"with {gameClients.Count} clients...");

                (
                    long inputSuccess,
                    long inputFail
                ) =
                    await RunRandomInputAsync(
                        gameClients);

                totalInputSuccess += inputSuccess;
                totalInputFail += inputFail;

                Console.WriteLine();
                Console.WriteLine(
                    $"[INPUT RESULT] " +
                    $"Success={inputSuccess}, " +
                    $"Fail={inputFail}");
            }
            catch (Exception ex)
            {
                Console.WriteLine();
                Console.WriteLine("[TEST ERROR]");
                Console.WriteLine(ex);
            }
            finally
            {
                // =================================================
                // 7. Disconnect
                // =================================================

                Console.WriteLine();
                Console.WriteLine(
                    "[TEST] Disconnecting clients...");

                Task[] disconnectTasks =
                    clients
                        .Select(
                            client =>
                                client.DisconnectAsync())
                        .ToArray();

                await Task.WhenAll(disconnectTasks);

                Console.WriteLine(
                    "[TEST] All clients disconnected.");
            }

            Console.WriteLine();
        }

        // =========================================================
        // Final Result
        // =========================================================

        Console.WriteLine();
        Console.WriteLine("=================================");
        Console.WriteLine(" GAME INPUT LOAD TEST RESULT");
        Console.WriteLine("=================================");

        Console.WriteLine(
            $"Connect Success : {totalConnectSuccess}");

        Console.WriteLine(
            $"Connect Fail    : {totalConnectFail}");

        Console.WriteLine(
            $"Connect Total   : " +
            $"{totalConnectSuccess + totalConnectFail}");

        Console.WriteLine();

        Console.WriteLine(
            $"Match Success : {totalMatchSuccess}");

        Console.WriteLine(
            $"Match Fail    : {totalMatchFail}");

        Console.WriteLine(
            $"Match Total   : " +
            $"{totalMatchSuccess + totalMatchFail}");

        Console.WriteLine();

        Console.WriteLine(
            $"Game Start Success : " +
            $"{totalGameStartSuccess}");

        Console.WriteLine(
            $"Game Start Fail    : " +
            $"{totalGameStartFail}");

        Console.WriteLine(
            $"Game Start Total   : " +
            $"{totalGameStartSuccess + totalGameStartFail}");

        Console.WriteLine();

        Console.WriteLine(
            $"Input Success : " +
            $"{totalInputSuccess}");

        Console.WriteLine(
            $"Input Fail    : " +
            $"{totalInputFail}");

        Console.WriteLine(
            $"Input Total   : " +
            $"{totalInputSuccess + totalInputFail}");

        Console.WriteLine("=================================");
    }

    // =========================================================
    // Random Input Test
    // =========================================================

    private static async Task<(long success, long fail)>
        RunRandomInputAsync(
            List<TestClient> clients)
    {
        using CancellationTokenSource cts =
            new();

        long totalSuccess = 0;
        long totalFail = 0;

        // =========================================================
        // 1. Input 전송 시작
        // =========================================================

        Task[] inputTasks =
            clients
                .Select(
                    client =>
                        RunClientInputAsync(
                            client,
                            cts.Token,
                            () =>
                            {
                                Interlocked.Increment(
                                    ref totalSuccess);
                            },
                            () =>
                            {
                                Interlocked.Increment(
                                    ref totalFail);
                            }))
                .ToArray();

        // =========================================================
        // 2. GameEnd 대기
        // =========================================================

        Console.WriteLine(
            "[TEST] Waiting for GAME END...");

        Task<bool>[] gameEndTasks =
            clients
                .Select(
                    client =>
                        client.WaitForGameEndAsync(
                            TimeSpan.FromSeconds(
                                TestConfig.GameEndTimeoutSeconds)))
                .ToArray();

        bool[] gameEndResults =
            await Task.WhenAll(gameEndTasks);

        int gameEndSuccess =
            gameEndResults.Count(result => result);

        int gameEndFail =
            gameEndResults.Length -
            gameEndSuccess;

        Console.WriteLine();

        Console.WriteLine(
            $"[GAME END RESULT] " +
            $"Success={gameEndSuccess}, " +
            $"Fail={gameEndFail}");

        PrintFailedClients(
            clients,
            gameEndResults,
            "GAME END");

        // =========================================================
        // 3. GameEnd 수신 후 Input 중단
        // =========================================================

        cts.Cancel();

        try
        {
            await Task.WhenAll(inputTasks);
        }
        catch (OperationCanceledException)
        {
            // 정상적인 테스트 종료
        }

        return (
            totalSuccess,
            totalFail);
    }

    // =========================================================
    // Client Input Loop
    // =========================================================

    private static async Task RunClientInputAsync(
        TestClient client,
        CancellationToken cancellationToken,
        Action onSuccess,
        Action onFail)
    {
        Random random =
            new(
                client.ClientId ^
                Environment.TickCount);

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                // =================================================
                // GameEnd 수신 시 Input 전송 중단
                // =================================================

                if (client.GameEnded)
                    break;

                InputType input =
                    GetRandomInput(random);

                try
                {
                    // GameEnd가 전송 직전에 발생했는지 한 번 더 확인
                    if (client.GameEnded)
                        break;

                    await client.SendInputAsync(
                        input);

                    onSuccess();
                }
                catch (Exception ex)
                {
                    onFail();

                    Console.WriteLine(
                        $"[Client {client.ClientId}] " +
                        $"Input failed: {ex.Message}");
                }

                await Task.Delay(
                    TestConfig.InputIntervalMilliseconds,
                    cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // GameEnd 수신 또는 테스트 종료
        }
    }

    // =========================================================
    // Random Input
    // =========================================================

    private static InputType GetRandomInput(
    Random random)
    {
        return random.Next(100) switch
        {
            < 30 => InputType.MoveLeft,
            < 60 => InputType.MoveRight,
            < 75 => InputType.Rotate,
            < 90 => InputType.Shoot,
            _ => InputType.ChangeMode
        };
    }

    // =========================================================
    // Simultaneous Match Request
    // =========================================================

    private static async Task
        SendMatchRequestsSimultaneouslyAsync(
            List<TestClient> clients)
    {
        TaskCompletionSource startSignal =
            new(
                TaskCreationOptions.RunContinuationsAsynchronously);

        Task[] tasks =
            clients
                .Select(
                    async client =>
                    {
                        await startSignal.Task;

                        await client.RequestMatchAsync();
                    })
                .ToArray();

        await Task.Yield();

        Console.WriteLine(
            "[TEST] Releasing MatchRequest barrier...");

        startSignal.SetResult();

        await Task.WhenAll(tasks);
    }

    // =========================================================
    // Failed Client 출력
    // =========================================================

    private static void PrintFailedClients(
        List<TestClient> clients,
        bool[] results,
        string stage)
    {
        List<int> failedClients = [];

        for (int i = 0;
             i < results.Length;
             i++)
        {
            if (!results[i])
            {
                failedClients.Add(
                    clients[i].ClientId);
            }
        }

        if (failedClients.Count == 0)
            return;

        Console.WriteLine(
            $"[FAILED {stage}] " +
            $"Clients={string.Join(
                ", ",
                failedClients)}");
    }
}