namespace OCRSample.Models
{
    public sealed class ScannerDevice
    {
        public ScannerDevice(string id, string name)
        {
            Id = id;
            Name = name;
        }

        public string Id { get; private set; }
        public string Name { get; private set; }
    }
}
