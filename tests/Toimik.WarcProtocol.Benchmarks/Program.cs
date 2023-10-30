namespace Toimik.WarcProtocol.Benchmarks;

using BenchmarkDotNet.Running;

internal class Program
{
    static void Main(string[] args)
    {
        BenchmarkRunner.Run(typeof(Program).Assembly, config: null, args);
    }
}