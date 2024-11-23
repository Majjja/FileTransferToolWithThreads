using FileTransferToolWithThreads.Interfaces;
using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace FileTransferToolWithThreads.Implementations
{
    public class TransferHelper : ITransferHelper
    {
        public async Task TransferFileAsync(string sourcePath, string destinationPath)
        {
            const int chunkSize = 1 * 1024 * 1024;
            var chunkQueue = new BlockingCollection<(byte[] Chunk, int BytesRead, long Position)>();

            Task producer = Task.Run(() =>
            {
                using FileStream sourceStream = new(sourcePath, FileMode.Open, FileAccess.Read);
                byte[] buffer = new byte[chunkSize];
                int bytesRead;
                long position = 0;

                while ((bytesRead = sourceStream.Read(buffer, 0, chunkSize)) > 0)
                {
                    byte[] chunk = new byte[bytesRead];
                    Array.Copy(buffer, chunk, bytesRead);
                    chunkQueue.Add((chunk, bytesRead, position));
                    position += bytesRead;
                }
                chunkQueue.CompleteAdding();
            });

            Task consumer = Task.Run(async () =>
            {
                await using FileStream destStream = new(destinationPath, FileMode.Create, FileAccess.ReadWrite);
                int blockNumber = 0;

                foreach (var (chunk, bytesRead, position) in chunkQueue.GetConsumingEnumerable())
                {
                    blockNumber++;
                    string sourceHash = ComputeMD5Hash(chunk);

                    destStream.Position = position;
                    await destStream.WriteAsync(chunk, 0, bytesRead);
                    await destStream.FlushAsync();

                    destStream.Position = position;
                    byte[] destChunk = new byte[bytesRead];
                    await destStream.ReadAsync(destChunk, 0, bytesRead);

                    string destHash = ComputeMD5Hash(destChunk);
                    if (sourceHash != destHash)
                    {
                        Console.WriteLine($"Hash mismatch at position {position}. Retrying...");
                        destStream.Position = position;
                        await destStream.WriteAsync(chunk, 0, bytesRead);
                    }
                    Console.WriteLine($"{blockNumber}. Position = {position}, Hash = {sourceHash}");
                }
            });
            await Task.WhenAll(producer, consumer);

            string sourceFileHash = ComputeSHA256Hash(sourcePath);
            string destFileHash = ComputeSHA256Hash(destinationPath);

            Console.WriteLine();
            Console.WriteLine($"Source File SHA256: {sourceFileHash}");
            Console.WriteLine($"Destination File SHA256: {destFileHash}");
        }

        private string ComputeMD5Hash(byte[] data)
        {
            byte[] hash = MD5.HashData(data);
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }

        private string ComputeSHA256Hash(string filePath)
        {
            byte[] hash = SHA256.HashData(File.OpenRead(filePath));
            return Convert.ToHexString(hash).ToLowerInvariant();
        }
    }
}
