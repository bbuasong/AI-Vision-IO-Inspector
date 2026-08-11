using System;

namespace ScannerSample.Services.Ocr.Common
{
    public class OcrEngineRunner
    {
        public OcrEngineRunner(string slotKey, string displayName, IOcrTextReader reader)
        {
            if (string.IsNullOrWhiteSpace(slotKey))
            {
                throw new ArgumentException("slotKey is required.", "slotKey");
            }

            if (string.IsNullOrWhiteSpace(displayName))
            {
                throw new ArgumentException("displayName is required.", "displayName");
            }

            if (reader == null)
            {
                throw new ArgumentNullException("reader");
            }

            SlotKey = slotKey;
            DisplayName = displayName;
            Reader = reader;
        }

        public string SlotKey { get; private set; }

        public string DisplayName { get; private set; }

        public IOcrTextReader Reader { get; private set; }
    }
}
