using block_racing_common.Network;
using block_racing_common.Network.Packets;

namespace block_racing_test;

public class Program
{
    private const string Host = "127.0.0.1";
    private const int Port = 7777;

    // 기존 연결을 유지한 채 새로운 상대를 계속 붙이는 횟수
    private const int PersistentTestCount = 50;

    // 동시에 새로 접속시키는 클라이언트 수
    // 20명 = 최대 10개 Room
    private const int BatchClientCount = 20;

    // 동시 접속 테스트 반복 횟수
    // 성능 측정에서는 한 번 생성한 Room을 유지하면서 관찰하기 위해 1회만 실행
    private const int BatchTestCount = 1;

    // 매칭된 게임을 유지하는 시간
    private static readonly TimeSpan GameKeepDuration =
        TimeSpan.FromSeconds(60);

    public static async Task Main()
    {
        Console.WriteLine("=================================");
        Console.WriteLine(" Block Racing Match Test Client");
        Console.WriteLine("=================================");
        Console.WriteLine();

        // -------------------------------------------------
        // Persistent Match Test
        // -------------------------------------------------

        await PersistentPlayerTest();

        Console.WriteLine();
        Console.WriteLine("=================================");
        Console.WriteLine(" Persistent Test Finished");
        Console.WriteLine("=================================");

        await Task.Delay(1000);

        // -------------------------------------------------
        // Batch Match / Game Load Test
        // -------------------------------------------------

        await BatchMatchTest();

        Console.WriteLine();
        Console.WriteLine("=================================");
        Console.WriteLine(" All Tests Finished");
        Console.WriteLine("=================================");
    }

    // =========================================================
    // 기존 플레이어를 계속 유지하면서 새로운 플레이어와 매칭
    // =========================================================

    private static async Task PersistentPlayerTest()
    {
        Console.WriteLine();
        Console.WriteLine("=================================");
        Console.WriteLine(" PERSISTENT PLAYER TEST");
        Console.WriteLine("=================================");
        Console.WriteLine(
            $"Persistent Tests : {PersistentTestCount}");
        Console.WriteLine();

        TestClient? persistentClient = null;

        int success = 0;
        int fail = 0;

        try
        {
            // -------------------------------------------------
            // 1. 기존 플레이어 접속
            // -------------------------------------------------

            persistentClient =
                new TestClient(
                    1,
                    Host,
                    Port);

            Console.WriteLine(
                "[PERSISTENT] Connecting Player 1...");

            await persistentClient.ConnectAndLoginAsync();

            Console.WriteLine(
                "[PERSISTENT] Player 1 connected.");

            // -------------------------------------------------
            // 2. 계속해서 새로운 상대를 접속시킴
            // -------------------------------------------------

            for (int i = 0;
                 i < PersistentTestCount;
                 i++)
            {
                int opponentId = i + 2;

                Console.WriteLine();
                Console.WriteLine(
                    $"========== PERSISTENT TEST " +
                    $"{i + 1}/{PersistentTestCount} ==========");

                // 기존 Player가 매칭 가능한 상태인지 확인
                persistentClient.ResetMatchState();

                // -------------------------------------------------
                // 새로운 상대 생성
                // -------------------------------------------------

                TestClient opponent =
                    new TestClient(
                        opponentId,
                        Host,
                        Port);

                try
                {
                    Console.WriteLine(
                        $"[TEST] Connecting Player {opponentId}...");

                    await opponent.ConnectAndLoginAsync();

                    Console.WriteLine(
                        $"[TEST] Player {opponentId} connected.");

                    // -------------------------------------------------
                    // 두 플레이어가 동시에 MatchRequest
                    // -------------------------------------------------

                    Console.WriteLine(
                        "[TEST] Sending MatchRequest...");

                    Task persistentMatch =
                        persistentClient.RequestMatchAsync();

                    Task opponentMatch =
                        opponent.RequestMatchAsync();

                    await Task.WhenAll(
                        persistentMatch,
                        opponentMatch);

                    // -------------------------------------------------
                    // 매칭 결과 대기
                    // -------------------------------------------------

                    Task<bool> persistentResult =
                        persistentClient.WaitForMatchAsync(
                            TimeSpan.FromSeconds(5));

                    Task<bool> opponentResult =
                        opponent.WaitForMatchAsync(
                            TimeSpan.FromSeconds(5));

                    bool[] results =
                        await Task.WhenAll(
                            persistentResult,
                            opponentResult);

                    bool matched =
                        results[0] && results[1];

                    if (matched)
                    {
                        success++;

                        Console.WriteLine(
                            $"[RESULT] MATCH SUCCESS " +
                            $"Player 1 <-> Player {opponentId}");
                    }
                    else
                    {
                        fail++;

                        Console.WriteLine(
                            $"[RESULT] MATCH FAILED " +
                            $"Player 1={results[0]}, " +
                            $"Player {opponentId}={results[1]}");
                    }
                }
                catch (Exception ex)
                {
                    fail++;

                    Console.WriteLine(
                        $"[TEST ERROR] Player {opponentId}");

                    Console.WriteLine(ex);
                }
                finally
                {
                    // 상대방만 종료
                    await opponent.DisconnectAsync();
                }

                // 다음 상대가 들어오기 전에 잠깐 대기
                await Task.Delay(300);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                "[PERSISTENT TEST ERROR]");

            Console.WriteLine(ex);
        }
        finally
        {
            // 마지막에 기존 플레이어 종료
            if (persistentClient != null)
            {
                await persistentClient.DisconnectAsync();
            }
        }

        Console.WriteLine();
        Console.WriteLine("=================================");
        Console.WriteLine(" PERSISTENT TEST RESULT");
        Console.WriteLine("=================================");
        Console.WriteLine($"Success : {success}");
        Console.WriteLine($"Fail    : {fail}");
        Console.WriteLine($"Total   : {success + fail}");
    }

    // =========================================================
    // 동시 접속 + 동시 매칭 + 게임 유지 테스트
    // =========================================================

    private static async Task BatchMatchTest()
    {
        Console.WriteLine();
        Console.WriteLine("=================================");
        Console.WriteLine(" BATCH MATCH / GAME LOAD TEST");
        Console.WriteLine("=================================");
        Console.WriteLine(
            $"Clients : {BatchClientCount}");
        Console.WriteLine(
            $"Expected Rooms : {BatchClientCount / 2}");
        Console.WriteLine(
            $"Tests   : {BatchTestCount}");
        Console.WriteLine(
            $"Keep Duration : {GameKeepDuration.TotalSeconds:F0}s");
        Console.WriteLine();

        int totalSuccess = 0;
        int totalFail = 0;

        for (int test = 1;
             test <= BatchTestCount;
             test++)
        {
            Console.WriteLine();
            Console.WriteLine(
                $"========== BATCH TEST " +
                $"{test}/{BatchTestCount} ==========");

            var clients =
                new List<TestClient>();

            try
            {
                // -------------------------------------------------
                // 1. 클라이언트 생성
                // -------------------------------------------------

                for (int i = 0;
                     i < BatchClientCount;
                     i++)
                {
                    clients.Add(
                        new TestClient(
                            i + 1,
                            Host,
                            Port));
                }

                // -------------------------------------------------
                // 2. 동시에 Connect + Login
                // -------------------------------------------------

                Console.WriteLine(
                    "[TEST] Connecting clients...");

                var connectTasks =
                    clients
                        .Select(
                            client =>
                                client.ConnectAndLoginAsync())
                        .ToArray();

                await Task.WhenAll(
                    connectTasks);

                Console.WriteLine(
                    "[TEST] All clients connected.");

                // -------------------------------------------------
                // 3. 동시에 MatchRequest
                // -------------------------------------------------

                Console.WriteLine(
                    "[TEST] Sending MatchRequest...");

                var matchTasks =
                    clients
                        .Select(
                            client =>
                                client.RequestMatchAsync())
                        .ToArray();

                await Task.WhenAll(
                    matchTasks);

                // -------------------------------------------------
                // 4. 매칭 결과 대기
                // -------------------------------------------------

                Console.WriteLine(
                    "[TEST] Waiting for matchmaking...");

                var resultTasks =
                    clients
                        .Select(
                            client =>
                                client.WaitForMatchAsync(
                                    TimeSpan.FromSeconds(5)))
                        .ToArray();

                bool[] results =
                    await Task.WhenAll(
                        resultTasks);

                int success =
                    results.Count(
                        x => x);

                int fail =
                    results.Length - success;

                totalSuccess += success;
                totalFail += fail;

                Console.WriteLine();
                Console.WriteLine(
                    $"[RESULT] Success={success}, Fail={fail}");

                foreach (var client in clients)
                {
                    Console.WriteLine(
                        $"Client {client.ClientId}: " +
                        $"{(client.Matched
                            ? "MATCHED"
                            : "TIMEOUT")}");
                }

                // -------------------------------------------------
                // 5. 매칭된 Room 유지
                // -------------------------------------------------

                if (success > 0)
                {
                    Console.WriteLine();
                    Console.WriteLine(
                        $"[TEST] Keeping connections " +
                        $"for {GameKeepDuration.TotalSeconds:F0} seconds...");

                    await Task.Delay(
                        GameKeepDuration);

                    Console.WriteLine(
                        "[TEST] Game keep duration finished.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    "[TEST ERROR]");

                Console.WriteLine(ex);
            }
            finally
            {
                // -------------------------------------------------
                // 6. 모든 클라이언트 종료
                // -------------------------------------------------

                Console.WriteLine(
                    "[TEST] Disconnecting clients...");

                foreach (var client in clients)
                {
                    await client.DisconnectAsync();
                }

                Console.WriteLine(
                    "[TEST] All clients disconnected.");
            }

            await Task.Delay(500);
        }

        Console.WriteLine();
        Console.WriteLine("=================================");
        Console.WriteLine(" BATCH TEST RESULT");
        Console.WriteLine("=================================");
        Console.WriteLine($"Success : {totalSuccess}");
        Console.WriteLine($"Fail    : {totalFail}");
        Console.WriteLine(
            $"Total   : {totalSuccess + totalFail}");
    }
}