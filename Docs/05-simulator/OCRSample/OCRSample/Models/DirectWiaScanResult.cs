namespace OCRSample.Models
{
    /// <summary>
    /// Result of a WIA scan attempt. Normal device states, including an empty
    /// ADF, are returned as data so the application can keep running.
    /// </summary>
    public sealed class DirectWiaScanResult
    {
        public bool IsSuccess { get; private set; }

        public string ImagePath { get; private set; }

        public string ErrorMessage { get; private set; }

        public DirectWiaScanFailure Failure { get; private set; }

        public bool IsPaperEmpty
        {
            get { return Failure == DirectWiaScanFailure.PaperEmpty; }
        }

        public static DirectWiaScanResult Success(string imagePath)
        {
            return new DirectWiaScanResult
            {
                IsSuccess = true,
                ImagePath = imagePath,
                Failure = DirectWiaScanFailure.None
            };
        }

        public static DirectWiaScanResult Failed(DirectWiaScanFailure failure, string errorMessage)
        {
            return new DirectWiaScanResult
            {
                IsSuccess = false,
                Failure = failure,
                ErrorMessage = errorMessage ?? string.Empty
            };
        }
    }

    public enum DirectWiaScanFailure
    {
        None,
        PaperEmpty,
        Busy,
        Offline,
        Cancelled,
        DeviceNotFound,
        Unexpected
    }
}
