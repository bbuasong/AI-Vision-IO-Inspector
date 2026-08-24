using System;
using System.Globalization;
using System.Windows.Data;

namespace AI.Vision.IOInspector.App.Converters
{
    /// <summary>
    /// 여러 줄로 된 글을 표의 한 칸에 담을 수 있게 한 줄로 펴 줍니다.
    ///
    /// <para>
    /// 검사 결과 메시지는 방향마다 한 줄씩 붙어 일곱 줄이 됩니다. 표의 한 행은 한 줄 높이라
    /// 둘째 줄부터는 잘려 읽을 수가 없었습니다. 저장된 글 자체는 그대로 두어야 합니다.
    /// 방향별 판정은 이력을 되짚을 때 필요한 자료이고, CSV 로 내보낼 때도 그대로 나가야 합니다.
    /// 그래서 보여 줄 때만 폅니다.
    /// </para>
    ///
    /// <para>
    /// 줄이 바뀌던 자리는 가운뎃점으로 표시해 원래 여러 줄이었음을 알 수 있게 합니다.
    /// 전체 글은 칸에 마우스를 올리면 볼 수 있도록 부르는 쪽에서 ToolTip 에 원문을 겁니다.
    /// </para>
    /// </summary>
    public class SingleLineTextConverter : IValueConverter
    {
        private const string LineSeparator = "  ·  ";

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string text = value as string;
            if (string.IsNullOrEmpty(text))
            {
                return text;
            }

            if (text.IndexOf('\n') < 0 && text.IndexOf('\r') < 0)
            {
                return text;
            }

            string[] lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            System.Text.StringBuilder builder = new System.Text.StringBuilder();
            foreach (string line in lines)
            {
                string trimmed = line.Trim();
                if (trimmed.Length == 0)
                {
                    continue;
                }

                if (builder.Length > 0)
                {
                    builder.Append(LineSeparator);
                }

                builder.Append(trimmed);
            }

            return builder.ToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }
}
