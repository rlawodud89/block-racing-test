namespace block_racing_test;

public static class GameLoadTest
{
    public static async Task RunAsync()
    {
        Console.WriteLine();
        Console.WriteLine("=================================");
        Console.WriteLine(" MATCHMAKING CONCURRENCY TEST");
        Console.WriteLine("=================================");

        Console.WriteLine(
            $"Clients : {TestConfig.BatchClientCount}");

        Console.WriteLine(
            $"Expected Rooms : " +
            $"{TestConfig.BatchClientCount / 2}");

        Console.WriteLine(
            $"Test Count : " +
            $"{TestConfig.BatchTestCount}");

        Console.WriteLine();

        int totalMatchSuccess = 0;
        int totalMatchFail = 0;

        int totalGameStartSuccess = 0;
        int totalGameStartFail = 0;

        for (int test = 1;
             test <= TestConfig.BatchTestCount;
             test++)
        {
            Console.WriteLine(
                $"========== TEST {test}/" +
                $"{TestConfig.BatchTestCount} ==========");

            List<TestClient> clients = [];

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

                Task[] connectTasks =
                    clients
                        .Select(
                            client =>
                                client.ConnectAndLoginAsync())
                        .ToArray();

                await Task.WhenAll(connectTasks);

                Console.WriteLine(
                    "[TEST] All clients connected.");

                // =================================================
                // 3. 동시 Match Request
                // =================================================

                Console.WriteLine(
                    "[TEST] Preparing simultaneous MatchRequest...");

                await SendMatchRequestsSimultaneouslyAsync(
                    clients);

                Console.WriteLine(
                    "[TEST] All MatchRequests sent.");

                // =================================================
                // 4. Match 결과 대기
                // =================================================

                Console.WriteLine(
                    "[TEST] Waiting for matchmaking...");

                Task<bool>[] matchTasks =
                    clients
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
                    clients,
                    matchResults,
                    "MATCH");

                // =================================================
                // 5. 게임 시작 확인
                // =================================================

                Console.WriteLine();
                Console.WriteLine(
                    "[TEST] Waiting for game start...");

                Task<bool>[] gameStartTasks =
                    clients
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

                totalGameStartSuccess += gameStartSuccess;
                totalGameStartFail += gameStartFail;

                Console.WriteLine(
                    $"[GAME START RESULT] " +
                    $"Success={gameStartSuccess}, " +
                    $"Fail={gameStartFail}");

                PrintFailedClients(
                    clients,
                    gameStartResults,
                    "GAME START");

                // =================================================
                // 6. 게임 유지
                // =================================================

                if (gameStartSuccess == clients.Count)
                {
                    Console.WriteLine();
                    Console.WriteLine(
                        $"[TEST] Keeping " +
                        $"{clients.Count} clients connected " +
                        $"for " +
                        $"{TestConfig.GameKeepDurationSeconds}s...");

                    await Task.Delay(
                        TimeSpan.FromSeconds(
                            TestConfig.GameKeepDurationSeconds));

                    Console.WriteLine(
                        "[TEST] Game duration finished.");
                }
                else
                {
                    Console.WriteLine(
                        "[TEST] Game load skipped " +
                        "because not all games started.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine();
                Console.WriteLine(
                    "[TEST ERROR]");

                Console.WriteLine(ex);
            }
            finally
            {
                // =================================================
                // 7. 동시 Disconnect
                // =================================================

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
        Console.WriteLine(" MATCHMAKING CONCURRENCY RESULT");
        Console.WriteLine("=================================");

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

        Console.WriteLine("=================================");
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
                        // 모든 Client가 여기서 대기
                        await startSignal.Task;

                        // 동시에 시작
                        await client.RequestMatchAsync();
                    })
                .ToArray();

        // 모든 Task가 대기 상태가 되도록 한 번 양보
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