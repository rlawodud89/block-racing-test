
namespace block_racing_test;

public static class TestConfig
{
    public const string Host = "127.0.0.1";
    public const int Port = 7777;

    // Matchmaking Test
    public const int PersistentTestCount = 50;

    // Game Load Test
    public const int BatchClientCount = 50;
    public const int BatchTestCount = 1;

    // Matchmaking timeout
    public const int MatchTimeoutSeconds = 5;

    // 게임 유지 시간
    public const int GameKeepDurationSeconds = 60;
}

