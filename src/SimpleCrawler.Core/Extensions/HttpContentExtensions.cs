using System.Buffers;

namespace SimpleCrawler.Core.Extensions;

public static class HttpContentExtensions
{
    /// <summary>
    /// Reads the decompressed response body but aborts once maxBytes is exceeded, returning null.
    /// Content-Length can't be trusted after decompression, so the limit is enforced while streaming.
    /// </summary>
    public static async Task<byte[]?> ReadCappedByteArrayAsync(this HttpContent content, long maxBytes, CancellationToken cancellationToken)
    {
        await using var stream = await content.ReadAsStreamAsync(cancellationToken);
        using var buffer = new MemoryStream();

        var rented = ArrayPool<byte>.Shared.Rent(81920);
        try
        {
            int read;
            while ((read = await stream.ReadAsync(rented, cancellationToken)) > 0)
            {
                if (buffer.Length + read > maxBytes)
                    return null;

                buffer.Write(rented, 0, read);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }

        return buffer.ToArray();
    }
}
