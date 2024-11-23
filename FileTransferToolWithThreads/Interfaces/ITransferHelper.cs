namespace FileTransferToolWithThreads.Interfaces
{
    public interface ITransferHelper
    {
        Task TransferFileAsync(string sourcePath, string destinationPath);
    }
}
