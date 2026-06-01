using System;

namespace AI.Vision.IOInspector.Application.Services
{
    /// <summary>
    /// 측정값을 DB에 등록된 기준값 단위로 변환합니다.
    /// Vision 내부 계산은 mm 기준을 우선하지만, AI 엔진이 cm 또는 m으로 반환해도 검사 흐름이 깨지지 않도록 보정합니다.
    /// </summary>
    public static class MeasurementUnitConverter
    {
        public static bool TryConvert(decimal value, string sourceUnit, string targetUnit, out decimal convertedValue)
        {
            convertedValue = value;

            string normalizedSourceUnit = NormalizeUnit(sourceUnit);
            string normalizedTargetUnit = NormalizeUnit(targetUnit);
            if (string.Equals(normalizedSourceUnit, normalizedTargetUnit, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            decimal sourceMillimeterRatio;
            decimal targetMillimeterRatio;
            if (!TryGetMillimeterRatio(normalizedSourceUnit, out sourceMillimeterRatio))
            {
                return false;
            }

            if (!TryGetMillimeterRatio(normalizedTargetUnit, out targetMillimeterRatio))
            {
                return false;
            }

            decimal valueInMillimeter = value * sourceMillimeterRatio;
            convertedValue = valueInMillimeter / targetMillimeterRatio;
            return true;
        }

        private static string NormalizeUnit(string unit)
        {
            if (string.IsNullOrWhiteSpace(unit))
            {
                return "mm";
            }

            return unit.Trim().ToLowerInvariant();
        }

        private static bool TryGetMillimeterRatio(string unit, out decimal millimeterRatio)
        {
            millimeterRatio = 1m;

            if (unit == "mm" || unit == "millimeter" || unit == "millimeters")
            {
                millimeterRatio = 1m;
                return true;
            }

            if (unit == "cm" || unit == "centimeter" || unit == "centimeters")
            {
                millimeterRatio = 10m;
                return true;
            }

            if (unit == "m" || unit == "meter" || unit == "meters")
            {
                millimeterRatio = 1000m;
                return true;
            }

            return false;
        }
    }
}
