using System.Buffers;
using System.IO.Pipelines;
using System.Runtime.CompilerServices;
using System.Text;

class GG
{
    public int N { get; set; } = 10_000_000;

    public void Setup()
    {
        Random rand = new();
        using var sw = new StreamWriter("numbers.txt");
        for (int i = 0; i < N; i++)
        {
            sw.WriteLine(rand.Next(0, 5000));
        }
    }

    public void Cleanup()
    {
        File.Delete("numbers.txt");
    }

    public async Task ScenarioPipelines()
    {
        await using var fs = File.OpenRead("numbers.txt");
        PipeReader reader = PipeReader.Create(fs);
        int[] bucket = new int[1024];
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
                number = int.Parse(bytes.FirstSpan);
            }
            else
            {
                number = int.Parse(Encoding.ASCII.GetString(bytes));
            }

            return true;
        }

        number = 0;
        return false;
    }
}