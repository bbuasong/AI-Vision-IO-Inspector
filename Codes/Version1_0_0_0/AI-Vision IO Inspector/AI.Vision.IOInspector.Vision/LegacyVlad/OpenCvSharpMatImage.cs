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
