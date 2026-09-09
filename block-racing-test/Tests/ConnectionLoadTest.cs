using System.Net.Sockets;

namespace block_racing_test;

public static class ConnectionLoadTest
{
    public static async Task RunAsync()
    {
        Console.WriteLine();
        Console.WriteLine("=================================");
        Console.WriteLine(" TCP CONNECTION LOAD TEST");
        Console.WriteLine("=================================");

        Console.WriteLine(
            $"Clients : {TestConfig.BatchClientCount}");

        Console.WriteLine(
            $"Batch Size : {TestConfig.ConnectionBatchSize}");

        Console.WriteLine(
            $"Batch Delay : {TestConfig.ConnectionBatchDelayMs}ms");

        Console.WriteLine();

        // =========================================================
        // 1. 동시 접속 테스트
        // =========================================================

        await RunSimultaneousConnectionTestAsync();

        Console.WriteLine();

        // =========================================================
        // 2. 분할 접속 테스트
        // =========================================================

        await RunBatchedConnectionTestAsync();

        Console.WriteLine();
        Console.WriteLine("=================================");
        Console.WriteLine(" TCP CONNECTION LOAD TEST FINISHED");
        Console.WriteLine("=================================");
    }

    // =========================================================
    // Simultaneous Connection Test
    // =========================================================

    private static async Task RunSimultaneousConnectionTestAsync()
    {
        Console.WriteLine(
            "========== SIMULTANEOUS TEST ==========");

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
            // 2. 동시 Connect + Login
            // =================================================

            Console.WriteLine(
                "[TEST] Connecting all clients simultaneously...");

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
                            catch (SocketException ex)
                            {
                                Console.WriteLine(
                                    $"[Client {client.ClientId}] " +
                                    $"SocketError={ex.SocketErrorCode}, " +
                                    $"Message={ex.Message}");

                                return false;
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

            bool[] results =
                await Task.WhenAll(connectTasks);

            for (int i = 0;
                 i < clients.Count;
                 i++)
            {
                if (results[i])
                    connectedClients.Add(clients[i]);
            }

            int success =
                connectedClients.Count;

            int fail =
                clients.Count - success;

            Console.WriteLine();
            Console.WriteLine(
                $"[SIMULTANEOUS RESULT] " +
                $"Success={success}, " +
                $"Fail={fail}, " +
                $"Total={clients.Count}");

            PrintFailedClients(
                clients,
                results);
        }
        finally
        {
            // =================================================
            // 3. Disconnect
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
    }

    // =========================================================
    // Batched Connection Test
    // =========================================================

    private static async Task RunBatchedConnectionTestAsync()
    {
        Console.WriteLine(
            "========== BATCHED TEST ==========");

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

            int totalSuccess = 0;
            int totalFail = 0;

            // =================================================
            // 2. Batch 단위 접속
            // =================================================

            for (
                int start = 0;
                start < clients.Count;
                start += TestConfig.ConnectionBatchSize)
            {
                int end =
                    Math.Min(
                        start + TestConfig.ConnectionBatchSize,
                        clients.Count);

                List<TestClient> batch =
                    clients[start..end];

                Console.WriteLine();
                Console.WriteLine(
                    $"[TEST] Connecting clients " +
                    $"{start + 1}~{end}...");

                Task<bool>[] connectTasks =
                    batch
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

                bool[] results =
                    await Task.WhenAll(connectTasks);

                for (int i = 0;
                     i < batch.Count;
                     i++)
                {
                    if (results[i])
                    {
                        connectedClients.Add(batch[i]);
                        totalSuccess++;
                    }
                    else
                    {
                        totalFail++;
                    }
                }

                Console.WriteLine(
                    $"[BATCH RESULT] " +
                    $"Success={results.Count(x => x)}, " +
                    $"Fail={results.Count(x => !x)}, " +
                    $"Total={results.Length}");

                // =================================================
                // 다음 Batch까지 대기
                // =================================================

                if (end < clients.Count)
                {
                    Console.WriteLine(
                        $"[TEST] Waiting " +
                        $"{TestConfig.ConnectionBatchDelayMs}ms " +
                        "before next batch...");

                    await Task.Delay(
                        TestConfig.ConnectionBatchDelayMs);
                }
            }

            // =================================================
            // 3. 최종 결과
            // =================================================

            Console.WriteLine();
            Console.WriteLine(
                $"[BATCHED RESULT] " +
                $"Success={totalSuccess}, " +
                $"Fail={totalFail}, " +
                $"Total={clients.Count}");
        }
        finally
        {
            // =================================================
            // 4. Disconnect
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
    }

    // =========================================================
    // Failed Client 출력
    // =========================================================

    private static void PrintFailedClients(
        List<TestClient> clients,
        bool[] results)
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
            $"[FAILED CLIENTS] " +
            $"Count={failedClients.Count}");

        Console.WriteLine(
            $"Clients={string.Join(
                ", ",
                failedClients)}");
    }
}