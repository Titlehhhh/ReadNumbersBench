using System;
using System.Buffers;
using System.IO;
using System.IO.Pipelines;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using BenchmarkDotNet;
using BenchmarkDotNet.Attributes;

namespace ReadNumbersBench
{
    [MemoryDiagnoser]
    public class Benchmarks
    {
        [Params(10_000_000)] public int N { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            Random rand = new();
            using var sw = new StreamWriter("numbers.txt");
            for (int i = 0; i < N; i++)
            {
                sw.WriteLine(rand.Next(0, 5000));
            }
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            File.Delete("numbers.txt");
        }


        [Benchmark]
        public async Task ScenarioStream()
        {
            int[] bucket = new int[1024];
            await using var fs = File.OpenRead("numbers.txt");
            using var sr = new StreamReader(fs);
            int total = 0;
            while (!sr.EndOfStream)
            {
                string str = await sr.ReadLineAsync();
                if (string.IsNullOrEmpty(str)) break;
                bucket[total % 1024] = int.Parse(str);
                total++;
            }


            if (total != N)
            {
                throw new Exception($"Total: {total}");
            }
        }

        [Benchmark]
        public async Task ScenarioPipelines()
        {
            await using var fs = File.OpenRead("numbers.txt");

            Pipe pipe = new(new PipeOptions(useSynchronizationContext: false));
            PipeWriter writer = pipe.Writer;
            int[] bucket = new int[1024];

            PipeReader reader = pipe.Reader;
            Task fillPipeTask = FillPipeAsync(fs, writer);

            Task t = Task.Run(async () =>
            {
                int total = 0;
                while (true)
                {
                    ReadResult result = await reader.ReadAsync();


                    ReadOnlySequence<byte> buffer = result.Buffer;

                    while (TryReadNumber(ref buffer, out int number))
                    {
                        bucket[total % 1024] = number;
                        total++;
                    }

                    reader.AdvanceTo(buffer.Start, buffer.End);

                    if (result.IsCompleted)
                        break;
                }

                await reader.CompleteAsync();

                if (total != N)
                {
                    throw new Exception($"Total: {total}");
                }
            });

            await Task.WhenAll(fillPipeTask, t);
        }

        async Task FillPipeAsync(Stream stream, PipeWriter writer)
        {
            const int minimumBufferSize = 4096 * 4;

            while (true)
            {
                Memory<byte> memory = writer.GetMemory(minimumBufferSize);
                try
                {
                    int bytesRead = await stream.ReadAsync(memory);
                    if (bytesRead == 0)
                    {
                        break;
                    }

                    writer.Advance(bytesRead);
                }
                catch (Exception)
                {
                    break;
                }

                FlushResult result = await writer.FlushAsync();

                if (result.IsCompleted)
                {
                    break;
                }
            }

            await writer.CompleteAsync();
        }

        private static ReadOnlySpan<byte> NewLine => "\r\n"u8;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool TryReadNumber(ref ReadOnlySequence<byte> buffer, out int number)
        {
            var reader = new SequenceReader<byte>(buffer);
            if (reader.TryReadTo(out ReadOnlySequence<byte> bytes, NewLine))
            {
                buffer = buffer.Slice(reader.Position);
                if (bytes.IsSingleSegment)
                {
                    number = int.Parse(utf8Text: bytes.FirstSpan);
                }
                else
                {
                    number = int.Parse(Encoding.UTF8.GetString(bytes));
                }

                return true;
            }

            number = 0;
            return false;
        }
    }
}