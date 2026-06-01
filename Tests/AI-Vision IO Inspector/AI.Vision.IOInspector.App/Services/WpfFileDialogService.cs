using Microsoft.Win32;

namespace AI.Vision.IOInspector.App.Services
{
    /// <summary>
    /// 기준 이미지 추가 시 사용할 WPF 파일 선택 다이얼로그 구현입니다.
    /// </summary>
    public class WpfFileDialogService : IFileDialogService
    {
        public string SelectImageFile()
        {
            OpenFileDialog dialog = new OpenFileDialog();
            dialog.Title = "기준 이미지 선택";
            dialog.Filter = "Image Files|*.png;*.jpg;*.jpeg;*.bmp";
            bool? result = dialog.ShowDialog();
            if (result.HasValue && result.Value)
            {
                return dialog.FileName;
            }

            return string.Empty;
        }

        public string SelectCsvOpenFile()
        {
            OpenFileDialog dialog = new OpenFileDialog();
            dialog.Title = "부품 CSV 불러오기";
            dialog.Filter = "CSV Files|*.csv|All Files|*.*";
            bool? result = dialog.ShowDialog();
            if (result.HasValue && result.Value)
            {
                return dialog.FileName;
            }

            return string.Empty;
        }

        public string SelectCsvSaveFile()
        {
            SaveFileDialog dialog = new SaveFileDialog();
            dialog.Title = "CSV 저장";
            dialog.Filter = "CSV Files|*.csv|All Files|*.*";
            dialog.FileName = "export.csv";
            bool? result = dialog.ShowDialog();
            if (result.HasValue && result.Value)
            {
                return dialog.FileName;
            }

            return string.Empty;
        }
    }
}
