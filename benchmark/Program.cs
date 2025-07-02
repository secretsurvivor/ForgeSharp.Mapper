using BenchmarkDotNet.Running;

namespace ForgeSharp.Mapper.Benchmark
{
    internal class Program
    {
        static void Main(string[] args)
        {
            BenchmarkRunner.Run<MappingComparison>();
        }
    }
}
