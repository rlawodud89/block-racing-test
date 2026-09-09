namespace block_racing_test;

public class Program
{
    public static async Task Main()
    {
        Console.WriteLine("=================================");
        Console.WriteLine(" Block Racing Test Client");
        Console.WriteLine("=================================");
        Console.WriteLine();

        Console.WriteLine("1. Connection Load Test");
        Console.WriteLine("2. Matchmaking Test");
        Console.WriteLine("3. Game Load Test");
        Console.WriteLine("4. Game Play Test");
        Console.WriteLine();

        Console.Write("Select Test: ");

        string? input = Console.ReadLine();

        switch (input)
        {
            case "1":
                await ConnectionLoadTest.RunAsync();
                break;

            case "2":
                await MatchmakingTest.RunAsync();
                break;

            case "3":
                await GameLoadTest.RunAsync();
                break;

            case "4":
                await GamePlayTest.RunAsync();
                break;

            default:
                Console.WriteLine("Invalid test.");
                break;
        }

        Console.WriteLine();
        Console.WriteLine("=================================");
        Console.WriteLine(" Test Finished");
        Console.WriteLine("=================================");
    }
}