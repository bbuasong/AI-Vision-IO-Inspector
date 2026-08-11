using System;
using System.IO;

namespace AI.Vision.IOInspector.Vision.ImvCamera
{
    /// <summary>
    /// IMV SDK에서 복사한 BGR24 프레임을 BMP 파일로 저장합니다.
    /// System.Drawing 의존을 피해서 배포 PC에 GDI+ 문제가 생기지 않도록 직접 BMP 헤더를 작성합니다.
    /// </summary>
    internal static class ImvBitmapWriter
    {
        public static void WriteBgr24(string filePath, int width, int height, byte[] bgrBuffer)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("저장 경로가 비어 있습니다.", "filePath");
            }

            if (width <= 0 || height <= 0)
            {
                throw new ArgumentException("이미지 크기가 올바르지 않습니다.");
            }

            if (bgrBuffer == null)
            {
                throw new ArgumentNullException("bgrBuffer");
            }

            int sourceStride = checked(width * 3);
            int requiredLength = checked(sourceStride * height);
            if (bgrBuffer.Length < requiredLength)
            {
                throw new ArgumentException("BGR24 버퍼 크기가 이미지 크기보다 작습니다.", "bgrBuffer");
            }

            string directoryPath = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrWhiteSpace(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            int destinationStride = ((sourceStride + 3) / 4) * 4;
            int imageSize = checked(destinationStride * height);
            int fileSize = 14 + 40 + imageSize;

            using (FileStream stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.Read))
            using (BinaryWriter writer = new BinaryWriter(stream))
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
                writer.Write(imageSize);
                writer.Write(0);
                writer.Write(0);
                writer.Write(0);
                writer.Write(0);

                byte[] rowPadding = new byte[destinationStride - sourceStride];
                int y = height - 1;
                while (y >= 0)
                {
                    int sourceOffset = y * sourceStride;
                    writer.Write(bgrBuffer, sourceOffset, sourceStride);
                    if (rowPadding.Length > 0)
                    {
                        writer.Write(rowPadding);
                    }

                    y--;
                }
            }
        }
    }
}
