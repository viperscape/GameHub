using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;

namespace NetGame
{
    static class Compression
    {
        public static async Task<byte[]> Compress(byte[] raw)
        {
            using (MemoryStream memory = new MemoryStream())
            {
                using (GZipStream gzip = new GZipStream(memory, CompressionMode.Compress, true))
                {
                    await gzip.WriteAsync(raw, 0, raw.Length);
                }

                return memory.ToArray();
            }
        }

        public static async Task<byte[]> Decompress(byte[] gzip)
        {
            using (GZipStream stream = new GZipStream(new MemoryStream(gzip), CompressionMode.Decompress))
            {
                const int size = 4096; // arbitrary max size
                byte[] buffer = new byte[size];
                using (MemoryStream memory = new MemoryStream())
                {
                    int len = await stream.ReadAsync(buffer, 0, size);
                    await memory.WriteAsync(buffer, 0, len);

                    return memory.ToArray();
                }
            }
        }
    }
}
