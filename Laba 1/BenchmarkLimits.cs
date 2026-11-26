using System;

namespace Laba_1
{
    // Ограничения входных данных для замеров: защищают от нехватки памяти, зависания UI и слишком долгих расчетов
    public static class BenchmarkLimits
    {
        public const int MinStartN = 1;
        public const int MinRuns = 1;
        public const int MaxRuns = 20;
        public const int MaxPoints2D = 5_000;
        public const int MinMatrixAxisPoints = 2;
        public const int MaxMatrixAxisPoints = 30;

        // Доля свободной памяти, которую разрешено занять под данные одного замера
        private const double MemoryBudgetShare = 0.6;

        private readonly record struct Limit(int MaxN, int BytesPerElement, string Reason);

        private static Limit GetLimit(int algorithmIndex) => algorithmIndex switch
        {
            0 => new(1_000_000, 4, "время O(1) не зависит от N, большие N только расходуют память"),
            1 or 2 or 10 => new(200_000_000, 4, "O(N), массив int (4 байта на элемент)"),
            4 => new(200_000_000, 8, "O(N), массив double (8 байт на элемент)"),
            3 => new(110_000, 8, "O(N²): ~20 с на один прогон при N = 110 000"),
            5 => new(70_000, 8, "O(N²): ~20 с на один прогон при N = 70 000"),
            6 => new(45_000_000, 8, "O(N log N): ~20 с на один прогон при N = 45 млн"),
            7 => new(65_000_000, 16, "O(N log N): ~20 с на один прогон при N = 65 млн"),
            8 => new(1_000, 24, "O(N³): ~13 с на одну ячейку при N = 1 000"),
            9 => new(50_000_000, 24, "HashSet: ~24 байта на элемент"),
            11 => new(12_000_000, 4, "~20 с на один прогон при N = 12 млн"),
            12 => new(10_000, 0, "шаг 1: N точек, суммарно O(N²) операций"),
            13 => new(10_000, 0, "глубина рекурсии и отдельный поток на каждый вызов"),
            14 => new(100_000, 0, "шаг 1: N точек на графике"),
            _ => new(1_000_000, 8, "общее ограничение")
        };

        private static bool IsStepBased(int algorithmIndex) => algorithmIndex >= 12 && algorithmIndex <= 14;
        private static bool IsMatrix(int algorithmIndex) => algorithmIndex == 8;

        public static int GetMaxN(int algorithmIndex) => GetLimit(algorithmIndex).MaxN;

        public static bool Validate(int algorithmIndex, string algorithmName, int startN, int endN, int step, int runs, out string error)
        {
            var limit = GetLimit(algorithmIndex);
            error = string.Empty;

            if (startN < MinStartN)
            {
                error = $"«Старт» должен быть не меньше {MinStartN}.";
                return false;
            }

            if (endN > limit.MaxN)
            {
                error = $"Для алгоритма «{algorithmName}» максимальный «Конец» — {limit.MaxN:N0} (указано {endN:N0}).\nПричина: {limit.Reason}.";
                return false;
            }

            long points = (long)(endN - startN) / step + 1;

            if (IsMatrix(algorithmIndex))
            {
                if (points < MinMatrixAxisPoints || points > MaxMatrixAxisPoints)
                {
                    error = $"Для 3D-графика матриц число точек по оси должно быть от {MinMatrixAxisPoints} до {MaxMatrixAxisPoints} (сейчас {points:N0}).\nПодберите «Шаг» так, чтобы (Конец − Старт) / Шаг + 1 попадало в этот диапазон.";
                    return false;
                }
            }
            else if (!IsStepBased(algorithmIndex))
            {
                if (runs < MinRuns || runs > MaxRuns)
                {
                    error = $"Количество прогонов должно быть от {MinRuns} до {MaxRuns} (указано {runs}).";
                    return false;
                }

                if (points > MaxPoints2D)
                {
                    error = $"Слишком много точек на графике: {points:N0} (максимум {MaxPoints2D:N0}).\nУвеличьте «Шаг» хотя бы до {Math.Max(1, (endN - startN) / (MaxPoints2D - 1) + 1):N0}.";
                    return false;
                }
            }

            long requiredBytes = IsMatrix(algorithmIndex)
                ? (long)endN * endN * limit.BytesPerElement
                : (long)endN * limit.BytesPerElement;

            long availableBytes = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes - GC.GetTotalMemory(false);
            long budgetBytes = (long)(availableBytes * MemoryBudgetShare);

            if (requiredBytes > budgetBytes)
            {
                error = $"Недостаточно памяти для замера: требуется ~{FormatBytes(requiredBytes)}, " +
                        $"доступно для замера ~{FormatBytes(budgetBytes)} ({MemoryBudgetShare:P0} от свободной памяти процесса).\nУменьшите «Конец».";
                return false;
            }

            return true;
        }

        private static string FormatBytes(long bytes)
        {
            const double Kb = 1024, Mb = Kb * 1024, Gb = Mb * 1024;
            return bytes >= Gb ? $"{bytes / Gb:F2} ГБ" : bytes >= Mb ? $"{bytes / Mb:F1} МБ" : $"{bytes / Kb:F0} КБ";
        }
    }
}
