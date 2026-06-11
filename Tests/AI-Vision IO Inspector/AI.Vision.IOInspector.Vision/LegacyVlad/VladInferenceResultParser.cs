using System;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using AI.Vision.IOInspector.Domain.Enums;
using AI.Vision.IOInspector.Vision.Models;

namespace AI.Vision.IOInspector.Vision.LegacyVlad
{
    /// <summary>
    /// VLAD_SDK가 반환한 detectData 포인터를 현재 프로젝트에서 쓰기 쉬운 결과 모델로 변환합니다.
    /// 기존 VLAD_Ops 흐름처럼 Draw 함수를 먼저 호출해 detectText와 classCount를 채우고,
    /// MSG_V1 형식으로 보이는 경우에는 bbox 정보도 직접 해석합니다.
    /// </summary>
    public class VladInferenceResultParser
    {
        private const int DltLength = 4;
        private const int DefaultClassBufferLength = 256;
        private const int MaxDetectionCount = 1024;
        private const int MaxStringFieldLength = 260;

        public VladInferenceResult Parse(
            IntPtr vladId,
            IntPtr detectData,
            IntPtr rawMatPointer,
            ImageViewType viewType,
            string imagePath)
        {
            VladInferenceResult result = new VladInferenceResult();
            if (vladId == IntPtr.Zero)
            {
                result.Message = "VLAD_ID가 비어 있어 결과를 해석할 수 없습니다.";
                return result;
            }

            if (detectData == IntPtr.Zero)
            {
                result.Message = "VLAD detectData 포인터가 비어 있습니다.";
                return result;
            }

            int classBufferLength = GetClassBufferLength(vladId);
            int[] classCounts = new int[classBufferLength];
            StringBuilder detectTextBuilder = new StringBuilder(8192);

            GCHandle classCountHandle = GCHandle.Alloc(classCounts, GCHandleType.Pinned);
            try
            {
                FillDrawResult(vladId, detectData, rawMatPointer, classCountHandle.AddrOfPinnedObject(), detectTextBuilder);
            }
            finally
            {
                classCountHandle.Free();
            }

            result.ClassCounts = classCounts;
            result.DetectText = detectTextBuilder.ToString();
            result.ValidDetectionCount = VLAD_Ops_Ai.VLAD_InferenceData_Get_Valid_Count(vladId, detectData);
            result.IsSuccess = true;

            TryParseV1Detections(vladId, detectData, viewType, imagePath, result);
            if (result.Detections.Count == 0)
            {
                AddClassCountDetections(vladId, viewType, imagePath, result);
            }

            result.Message = "VLAD detectData 해석 완료";
            return result;
        }

        private int GetClassBufferLength(IntPtr vladId)
        {
            int classCount = VLAD_Ops_Ai.VLAD_Get_Class_Count(vladId);
            if (classCount < DefaultClassBufferLength)
            {
                return DefaultClassBufferLength;
            }

            return classCount + 16;
        }

        private void FillDrawResult(
            IntPtr vladId,
            IntPtr detectData,
            IntPtr rawMatPointer,
            IntPtr classCountPointer,
            StringBuilder detectTextBuilder)
        {
            int messageVersion = VLAD_Ops_Ai.VLAD_Get_Msg_Ver(vladId);
            if (messageVersion == (int)SDK_MSG.MSG_V2)
            {
                VLAD_Ops_Ai.VLAD_InferenceData_V2_Draw(
                    vladId,
                    detectData,
                    rawMatPointer,
                    classCountPointer,
                    detectTextBuilder);
                return;
            }

            VLAD_Ops_Ai.VLAD_InferenceData_V1_Draw(
                vladId,
                detectData,
                rawMatPointer,
                classCountPointer,
                detectTextBuilder,
                string.Empty,
                IntPtr.Zero,
                0);
        }

        private void TryParseV1Detections(
            IntPtr vladId,
            IntPtr detectData,
            ImageViewType viewType,
            string imagePath,
            VladInferenceResult result)
        {
            try
            {
                int offset = 0;
                string messageType = ReadAscii(detectData, ref offset, DltLength);
                if (string.IsNullOrWhiteSpace(messageType))
                {
                    return;
                }

                int folderLength = ReadFixedInt(detectData, ref offset);
                if (!IsValidLength(folderLength))
                {
                    return;
                }

                if (folderLength > 0)
                {
                    ReadAscii(detectData, ref offset, folderLength);
                }

                int validCount = ReadFixedInt(detectData, ref offset);
                if (validCount < 0 || validCount > MaxDetectionCount)
                {
                    return;
                }

                int loopCount = validCount;
                if (loopCount == 0 && result.ValidDetectionCount > 0 && result.ValidDetectionCount <= MaxDetectionCount)
                {
                    loopCount = result.ValidDetectionCount;
                }

                for (int index = 0; index < loopCount; index++)
                {
                    VladDetection detection = ReadV1Detection(vladId, detectData, ref offset, viewType, imagePath);
                    if (detection != null)
                    {
                        result.Detections.Add(detection);
                    }
                }
            }
            catch
            {
                // 모델/SDK 버전에 따라 detectData 내부 형식이 달라질 수 있으므로,
                // 직접 파싱이 실패하면 Draw 함수에서 얻은 classCount 기반 결과만 사용합니다.
            }
        }

        private VladDetection ReadV1Detection(
            IntPtr vladId,
            IntPtr detectData,
            ref int offset,
            ImageViewType viewType,
            string fallbackImagePath)
        {
            ReadFixedInt(detectData, ref offset);
            ReadFixedInt(detectData, ref offset);

            int imageFileLength = ReadFixedInt(detectData, ref offset);
            if (!IsValidLength(imageFileLength))
            {
                return null;
            }

            string imagePath = string.Empty;
            if (imageFileLength > 0)
            {
                imagePath = ReadAscii(detectData, ref offset, imageFileLength);
            }

            int classId = ReadFixedInt(detectData, ref offset);
            decimal score = ReadFixedDecimal(detectData, ref offset);
            int x = ReadFixedInt(detectData, ref offset);
            int y = ReadFixedInt(detectData, ref offset);
            int width = ReadFixedInt(detectData, ref offset);
            int height = ReadFixedInt(detectData, ref offset);

            if (string.IsNullOrWhiteSpace(imagePath))
            {
                imagePath = fallbackImagePath;
            }

            VladDetection detection = new VladDetection();
            detection.ViewType = viewType;
            detection.ClassId = classId;
            detection.ClassName = GetClassName(vladId, classId);
            detection.Score = NormalizeScore(score);
            detection.X = x;
            detection.Y = y;
            detection.Width = width;
            detection.Height = height;
            detection.SourceImagePath = imagePath;
            return detection;
        }

        private void AddClassCountDetections(
            IntPtr vladId,
            ImageViewType viewType,
            string imagePath,
            VladInferenceResult result)
        {
            if (result.ClassCounts == null)
            {
                return;
            }

            for (int index = 0; index < result.ClassCounts.Length; index++)
            {
                int count = result.ClassCounts[index];
                if (count <= 0)
                {
                    continue;
                }

                for (int occurrence = 0; occurrence < count && occurrence < MaxDetectionCount; occurrence++)
                {
                    VladDetection detection = new VladDetection();
                    detection.ViewType = viewType;
                    detection.ClassId = index;
                    detection.ClassName = GetClassName(vladId, index);
                    detection.Score = 0m;
                    detection.SourceImagePath = imagePath;
                    result.Detections.Add(detection);
                }
            }
        }

        private string GetClassName(IntPtr vladId, int classId)
        {
            IntPtr namePointer = VLAD_Ops_Ai.VLAD_Get_Class_Str(vladId, classId);
            if (namePointer == IntPtr.Zero)
            {
                return "Class" + classId.ToString(CultureInfo.InvariantCulture);
            }

            string name = Marshal.PtrToStringAnsi(namePointer);
            if (string.IsNullOrWhiteSpace(name))
            {
                return "Class" + classId.ToString(CultureInfo.InvariantCulture);
            }

            return name;
        }

        private bool IsValidLength(int length)
        {
            return length >= 0 && length <= MaxStringFieldLength;
        }

        private string ReadAscii(IntPtr source, ref int offset, int length)
        {
            if (length <= 0)
            {
                return string.Empty;
            }

            byte[] buffer = new byte[length];
            Marshal.Copy(IntPtr.Add(source, offset), buffer, 0, length);
            offset += length;
            return Encoding.ASCII.GetString(buffer).TrimEnd('\0');
        }

        private int ReadFixedInt(IntPtr source, ref int offset)
        {
            string text = ReadAscii(source, ref offset, DltLength).Trim();
            int value;
            if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
            {
                return 0;
            }

            return value;
        }

        private decimal ReadFixedDecimal(IntPtr source, ref int offset)
        {
            string text = ReadAscii(source, ref offset, DltLength).Trim();
            decimal value;
            if (!decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
            {
                return 0m;
            }

            return value;
        }

        private decimal NormalizeScore(decimal score)
        {
            if (score > 1m && score <= 100m)
            {
                return score / 100m;
            }

            return score;
        }
    }
}
