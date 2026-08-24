using System;
using System.IO;
using OpenCvSharp;

namespace AI.Vision.IOInspector.Vision.LegacyVlad
{
    /// <summary>
    /// 이미지 파일을 VLAD_SDK가 요구하는 OpenCV Mat 포인터로 변환하기 위한 보조 클래스입니다.
    /// Mat 수명은 VLAD 추론이 끝날 때까지 유지되어야 하므로 using 블록 안에서 사용합니다.
    /// </summary>
    public sealed class OpenCvSharpMatImage : IDisposable
    {
        private Mat _mat;

        private OpenCvSharpMatImage(Mat mat)
        {
            _mat = mat;
        }

        public IntPtr CvPtr
        {
            get
            {
                if (_mat == null)
                {
                    return IntPtr.Zero;
                }

                return _mat.CvPtr;
            }
        }

        public int Width
        {
            get { return _mat == null ? 0 : _mat.Cols; }
        }

        public int Height
        {
            get { return _mat == null ? 0 : _mat.Rows; }
        }

        public string TypeText
        {
            get { return _mat == null ? string.Empty : _mat.Type().ToString(); }
        }

        /// <summary>
        /// 빈 그림을 만듭니다. 파일 없이 Mat 만 있으면 되는 곳에서 씁니다.
        ///
        /// <para>
        /// 프로그램을 켠 뒤 AI 를 한 번 깨워 두는 데 씁니다. 그때는 검사할 사진이 아직 없고,
        /// 무엇이 찍혔는지도 중요하지 않습니다. 크기와 형식만 실제 검사와 같으면 됩니다.
        /// </para>
        /// </summary>
        public static OpenCvSharpMatImage CreateBlank(int width, int height)
        {
            if (width <= 0 || height <= 0)
            {
                throw new ArgumentOutOfRangeException("width", "빈 그림의 크기는 0보다 커야 합니다.");
            }

            return new OpenCvSharpMatImage(new Mat(height, width, MatType.CV_8UC3, Scalar.All(0)));
        }

        public static OpenCvSharpMatImage LoadFromFile(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("이미지 파일 경로가 비어 있습니다.", "filePath");
            }

            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("VLAD 추론에 사용할 이미지 파일을 찾을 수 없습니다.", filePath);
            }

            Mat mat = Cv2.ImRead(filePath, ImreadModes.Color);
            if (mat == null || mat.Empty())
            {
                if (mat != null)
                {
                    mat.Dispose();
                }

                throw new InvalidOperationException("OpenCV가 이미지 파일을 Mat로 읽지 못했습니다. " + filePath);
            }

            return new OpenCvSharpMatImage(NormalizeForVlad(mat));
        }

        private static Mat NormalizeForVlad(Mat source)
        {
            Mat normalized = source;

            // VLAD API에는 원본 프레임 크기를 전달하는 별도 인자가 없습니다.
            // 따라서 수신 및 저장된 원본 해상도를 그대로 유지하고, BGR 3채널 형식만 보장합니다.
            // 과거의 1920x1080 강제 Resize는 Top/Thickness 고해상도 프레임을 축소했습니다.
            if (normalized.Type() != MatType.CV_8UC3)
            {
                Mat converted = new Mat();
                normalized.ConvertTo(converted, MatType.CV_8UC3);
                normalized.Dispose();
                normalized = converted;
            }

            return normalized;
        }

        public void Dispose()
        {
            if (_mat != null)
            {
                _mat.Dispose();
                _mat = null;
            }
        }
    }
}
