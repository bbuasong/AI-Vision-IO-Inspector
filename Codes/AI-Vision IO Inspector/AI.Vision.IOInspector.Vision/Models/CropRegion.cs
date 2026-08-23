using System;
using System.Globalization;

namespace AI.Vision.IOInspector.Vision.Models
{
    /// <summary>
    /// 크롭이 원본 이미지에서 잘라 낸 자리입니다.
    ///
    /// <para>
    /// 이 자리를 알면 잘라 낸 그림 위의 좌표를 원본 좌표로 되돌릴 수 있습니다.
    ///   원본좌표 = 크롭좌표 + 잘라 낸 시작 위치
    /// 측정부 좌표는 원본 기준으로 저장하고 AI에 넘기므로, 화면에 크롭을 보여 주면서도
    /// 좌표를 어긋나지 않게 하려면 이 값이 필요합니다.
    /// </para>
    /// </summary>
    public sealed class CropRegion
    {
        public CropRegion(int x, int y, int width, int height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        public int X { get; private set; }

        public int Y { get; private set; }

        public int Width { get; private set; }

        public int Height { get; private set; }

        public bool IsValid
        {
            get { return Width > 0 && Height > 0; }
        }

        /// <summary>
        /// 잘라 낸 그림 위의 좌표를 원본 이미지 좌표로 되돌립니다.
        /// </summary>
        public void ToSourcePoint(double croppedX, double croppedY, out double sourceX, out double sourceY)
        {
            sourceX = croppedX + X;
            sourceY = croppedY + Y;
        }

        /// <summary>
        /// 원본 이미지 좌표를 잘라 낸 그림 위의 좌표로 옮깁니다.
        /// 잘라 낸 범위 밖이면 음수나 크기를 넘는 값이 나옵니다.
        /// </summary>
        public void ToCroppedPoint(double sourceX, double sourceY, out double croppedX, out double croppedY)
        {
            croppedX = sourceX - X;
            croppedY = sourceY - Y;
        }

        /// <summary>
        /// SDK가 채워 준 JSON에서 자리를 읽습니다.
        ///
        /// <para>
        /// 형식은 이렇습니다.
        ///   { "cropped": true, "x": 123, "y": 45, "width": 300, "height": 200 }
        /// 잘라 내지 못했으면 { "cropped": false }만 옵니다.
        /// </para>
        ///
        /// <para>
        /// 값이 몇 개뿐이라 JSON 라이브러리를 쓰지 않고 직접 읽습니다.
        /// 이 경로는 화면을 그릴 때마다 지나가므로 가벼운 편이 낫습니다.
        /// </para>
        /// </summary>
        public static bool TryParse(string json, out CropRegion region)
        {
            region = null;
            if (string.IsNullOrWhiteSpace(json))
            {
                return false;
            }

            if (!ReadBoolean(json, "cropped"))
            {
                return false;
            }

            int x;
            int y;
            int width;
            int height;

            if (!TryReadInt32(json, "x", out x) ||
                !TryReadInt32(json, "y", out y) ||
                !TryReadInt32(json, "width", out width) ||
                !TryReadInt32(json, "height", out height))
            {
                return false;
            }

            if (width <= 0 || height <= 0)
            {
                return false;
            }

            region = new CropRegion(x, y, width, height);
            return true;
        }

        private static bool ReadBoolean(string json, string name)
        {
            int valueStart = FindValueStart(json, name);
            if (valueStart < 0)
            {
                return false;
            }

            return string.Compare(json, valueStart, "true", 0, 4, StringComparison.OrdinalIgnoreCase) == 0;
        }

        private static bool TryReadInt32(string json, string name, out int value)
        {
            value = 0;
            int valueStart = FindValueStart(json, name);
            if (valueStart < 0)
            {
                return false;
            }

            int end = valueStart;
            if (end < json.Length && (json[end] == '-' || json[end] == '+'))
            {
                end++;
            }

            while (end < json.Length && char.IsDigit(json[end]))
            {
                end++;
            }

            if (end == valueStart)
            {
                return false;
            }

            return int.TryParse(
                json.Substring(valueStart, end - valueStart),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out value);
        }

        /// <summary>
        /// "이름" 뒤의 콜론을 지나 값이 시작하는 자리를 찾습니다.
        /// </summary>
        private static int FindValueStart(string json, string name)
        {
            string key = "\"" + name + "\"";
            int keyIndex = json.IndexOf(key, StringComparison.OrdinalIgnoreCase);
            if (keyIndex < 0)
            {
                return -1;
            }

            int index = keyIndex + key.Length;
            while (index < json.Length && (json[index] == ' ' || json[index] == '\t'))
            {
                index++;
            }

            if (index >= json.Length || json[index] != ':')
            {
                return -1;
            }

            index++;
            while (index < json.Length && (json[index] == ' ' || json[index] == '\t'))
            {
                index++;
            }

            return index < json.Length ? index : -1;
        }

        public override string ToString()
        {
            return "x=" + X.ToString(CultureInfo.InvariantCulture) +
                   ", y=" + Y.ToString(CultureInfo.InvariantCulture) +
                   ", " + Width.ToString(CultureInfo.InvariantCulture) +
                   "x" + Height.ToString(CultureInfo.InvariantCulture);
        }
    }
}
