namespace block_racing_test;

public static class MatchmakingTest
{
    public static async Task RunAsync()
    {
        Console.WriteLine();
        Console.WriteLine("=================================");
        Console.WriteLine(" Matchmaking Test");
        Console.WriteLine("=================================");

        int clientCount = TestConfig.BatchClientCount;
        int testCount = TestConfig.BatchTestCount;

        int totalSuccess = 0;
        int totalFail = 0;

        for (int testIndex = 1; testIndex <= testCount; testIndex++)
        {
            Console.WriteLine();
            Console.WriteLine(
                $"[Test {testIndex}/{testCount}] " +
                $"Starting {clientCount} clients");

            List<TestClient> clients = new();

            try
            {
                // -------------------------------------------------
                // 1. Client 생성
                // -------------------------------------------------

                for (int i = 0; i < clientCount; i++)
                {
                    TestClient client = new(
                        i + 1,
                        TestConfig.Host,
                        TestConfig.Port);

                    clients.Add(client);
                }

                // -------------------------------------------------
                // 2. Connect + Login
                // -------------------------------------------------

                Console.WriteLine(
                    $"[Test {testIndex}] Connecting clients...");

                await Task.WhenAll(
                    clients.Select(
                        client => client.ConnectAndLoginAsync()));

                Console.WriteLine(
                    $"[Test {testIndex}] " +
                    $"All clients connected.");

                // -------------------------------------------------
                // 3. Match Request
                // -------------------------------------------------

                Console.WriteLine(
                    $"[Test {testIndex}] " +
                    $"Sending match requests...");

                await Task.WhenAll(
                    clients.Select(
                        client => client.RequestMatchAsync()));

                // -------------------------------------------------
                // 4. Match 결과 대기
                // -------------------------------------------------

                Console.WriteLine(
                    $"[Test {testIndex}] " +
                    $"Waiting for matchmaking...");

                bool[] results =
                    await Task.WhenAll(
                        clients.Select(
                            client =>
                                client.WaitForMatchAsync(
                                    TimeSpan.FromSeconds(
                                        TestConfig.MatchTimeoutSeconds))));

                int successCount =
                    results.Count(result => result);

                int failCount =
                    results.Length - successCount;

                Console.WriteLine(
                    $"[Test {testIndex}] " +
                    $"Match Result: " +
                    $"Success={successCount}, " +
                    $"Fail={failCount}");

                if (failCount == 0)
                {
                    totalSuccess++;
                }
                else
                {
                    totalFail++;
                }

                // -------------------------------------------------
                // 5. 매칭 테스트에서는 게임 시작을 기다리지 않음
                // -------------------------------------------------
                //
                // S_RoomReady를 받은 순간 매칭 성공으로 판단.
                // TestClient가 자동으로 C_Ready를 보내므로
                // 이후 게임 시작 과정은 GameLoadTest에서 확인한다.
            }
            catch (Exception ex)
            {
                totalFail++;

                Console.WriteLine(
                    $"[Test {testIndex}] " +
                    $"ERROR: {ex.Message}");
            }
            finally
            {
                // -------------------------------------------------
                // 6. Client Disconnect
                // -------------------------------------------------

                await Task.WhenAll(
                    clients.Select(
                        client => client.DisconnectAsync()));
            }

            // 테스트 간 서버 상태 정리 시간
            await Task.Delay(1000);
        }

        // ---------------------------------------------------------
        // Summary
        // ---------------------------------------------------------

        Console.WriteLine();
        Console.WriteLine("=================================");
        Console.WriteLine(" Matchmaking Test Result");
        Console.WriteLine("=================================");

        Console.WriteLine(
            $"Total Tests : {testCount}");

        Console.WriteLine(
            $"Success     : {totalSuccess}");

        Console.WriteLine(
            $"Fail        : {totalFail}");

        Console.WriteLine(
            $"Success Rate: " +
            $"{(double)totalSuccess / testCount * 100:F1}%");

        Console.WriteLine(
            "=================================");
    }
}