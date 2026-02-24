using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;
using System.Diagnostics;
using SqlToObjectifyVsEfCoreBenchmark;

public static class Program
{
    public static void Main(string[] args)
    {
        if (Debugger.IsAttached && Environment.GetEnvironmentVariable("BDN_ALLOW_DEBUGGER") is not "1")
        {
            Console.Error.WriteLine("BenchmarkDotNet is running with an attached debugger.");
            Console.Error.WriteLine("Run the benchmark without debugging (Visual Studio: Ctrl+F5) or set BDN_ALLOW_DEBUGGER=1 to override.");
            Environment.Exit(1);
        }

        // This will create+seed the DB on first run (10,000 rows),
        // then benchmark EF Core LINQ vs SqlToObjectify raw SQL mapping.
        var artifactsPath = Path.Combine(
            Path.GetTempPath(),
            "bdn",
            "SqlToObjectifyVsEfCoreBenchmark",
            Guid.NewGuid().ToString("N"));

        var config = DefaultConfig.Instance.WithArtifactsPath(artifactsPath);

        Console.WriteLine($"BenchmarkDotNet artifacts: {artifactsPath}");
        BenchmarkRunner.Run<PersonQueryBenchmarks>(config);
    }
}
