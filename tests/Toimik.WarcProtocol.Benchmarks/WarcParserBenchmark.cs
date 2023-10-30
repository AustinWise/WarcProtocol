namespace Toimik.WarcProtocol.Benchmarks;

using BenchmarkDotNet.Attributes;

[MemoryDiagnoser]
public class WarcParserBenchmark
{
    readonly byte[] warcFile;

    public WarcParserBenchmark()
    {
        using var resourceStream = typeof(WarcParserBenchmark).Assembly.GetManifestResourceStream("Toimik.WarcProtocol.Benchmarks.response.warc");
        using var memoryStream = new MemoryStream();
        resourceStream!.CopyTo(memoryStream);
        warcFile = memoryStream.ToArray();
    }

    [Benchmark]
    public async Task<int> ReadFile()
    {
        var parser = new WarcParser();
        int recordCount = 0;
        await foreach (var record in parser.Parse(new MemoryStream(warcFile, writable: false), isCompressed: false))
        {
            recordCount += 1;
        }
        return recordCount;
    }
}
