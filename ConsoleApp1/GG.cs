using System.Buffers;
using System.IO.Pipelines;

class GG
{
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
}