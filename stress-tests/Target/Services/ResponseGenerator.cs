namespace Target.Services;

public class ResponseGenerator
{
    private static readonly Random Random = new();

    public byte[] GenerateRandom(int size)
    {
        var buffer = new byte[size];
        Random.NextBytes(buffer);
        return buffer;
    }

    public byte[] GenerateCompressible(int size)
    {
        // Generate text-like content that compresses well
        var text = string.Concat(Enumerable.Repeat("Lorem ipsum dolor sit amet, consectetur adipiscing elit. ", size / 50));
        return System.Text.Encoding.UTF8.GetBytes(text);
    }

    public string GenerateJson(int approximateSize)
    {
        var itemCount = approximateSize / 100; // Rough estimate
        var items = Enumerable.Range(1, itemCount)
            .Select(i => $$"""{"id": {{i}}, "name": "Item {{i}}", "value": {{Random.Next(1000)}}}""");
        
        return $"[{string.Join(",", items)}]";
    }

    public byte[] GeneratePattern(string pattern, int size)
    {
        var repeated = string.Concat(Enumerable.Repeat(pattern, (size / pattern.Length) + 1));
        var truncated = repeated.Substring(0, size);
        return System.Text.Encoding.UTF8.GetBytes(truncated);
    }
}
