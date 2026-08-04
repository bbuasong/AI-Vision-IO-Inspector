using System;
using System.IO;

namespace AI.Vision.IOInspector.Infrastructure.Services.Camera
{
    /// <summary>
    /// 외부 이미지 라이브러리 없이 WPF가 읽을 수 있는 24bit BMP 파일을 생성합니다.
    /// 실제 카메라 연결 전에도 검사 UI와 기준 이미지 저장 흐름을 파일 기반으로 검증하기 위한 유틸리티입니다.
    /// </summary>
    public static class SimpleBitmapWriter
    {
        public static void WriteGradient(string filePath, int width, int height, int colorSeed)
        {
            if (width <= 0)
            {
                width = 640;
            }

            if (height <= 0)
            {
                height = 480;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(filePath));
            int rowStride = ((width * 3 + 3) / 4) * 4;
            int pixelDataSize = rowStride * height;
            int fileSize = 54 + pixelDataSize;

            using (BinaryWriter writer = new BinaryWriter(File.Open(filePath, FileMode.Create, FileAccess.Write, FileShare.Read)))
            {
                writer.Write((byte)'B');
                writer.Write((byte)'M');
                writer.Write(fileSize);
                writer.Write((short)0);
                writer.Write((short)0);
                writer.Write(54);
                writer.Write(40);
                writer.Write(width);
                writer.Write(height);
                writer.Write((short)1);
                writer.Write((short)24);
                writer.Write(0);
                writer.Write(pixelDataSize);
                writer.Write(2835);
                writer.Write(2835);
                writer.Write(0);
                writer.Write(0);

                byte[] row = new byte[rowStride];
                for (int y = height - 1; y >= 0; y--)
                {
                    FillRow(row, width, height, y, colorSeed);
                    writer.Write(row);
                }
            }
        }

        private static void FillRow(byte[] row, int width, int height, int y, int colorSeed)
        {
            Array.Clear(row, 0, row.Length);
            for (int x = 0; x < width; x++)
            {
                int offset = x * 3;
                byte blue = (byte)((x + colorSeed * 31) % 256);
                byte green = (byte)((y + colorSeed * 53) % 256);
                byte red = (byte)(((x + y) / 2 + colorSeed * 79) % 256);
                row[offset] = blue;
                row[offset + 1] = green;
                row[offset + 2] = red;
            }
        }
    }
}
