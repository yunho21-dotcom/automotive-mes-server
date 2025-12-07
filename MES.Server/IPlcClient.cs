public interface IPlcClient : IDisposable
{
    bool EnsureConnected();
    int ReadDevice(string deviceName);
    void WriteDevice(string deviceName, int value);
}
