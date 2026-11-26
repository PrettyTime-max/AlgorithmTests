using System;

namespace Laba_1
{
    public static class AlgorithmExecutor
    {
        // Запускает алгоритм по его индексу и возвращает количество вычисленных операций (шагов)
        public static long Execute(int algorithmIndex, int[] intData, double[] doubleData, int n)
            => Execute(algorithmIndex, intData, doubleData, n, out _);

        // Результат вычисления можно прочитать после замера, чтобы JIT не считал его ненужным.
        public static long Execute(int algorithmIndex, int[] intData, double[] doubleData, int n, out long result)
        {
            result = 0;
            switch (algorithmIndex)
            {
                case 0:
                    Algorithms.Constant(intData, n);
                    return 1;

                case 1:
                    result = Algorithms.Sum(intData, n);
                    return n;

                case 2:
                    result = Algorithms.Product(intData, n);
                    return n;

                case 3:
                    Algorithms.PolyNaive(doubleData, 1.5, n);
                    return (long)n * n;

                case 4:
                    Algorithms.PolyHorner(doubleData, 1.5, n);
                    return n;

                case 5:
                    Algorithms.BubbleSort(doubleData, n);
                    return (long)n * n;

                case 6:
                    Algorithms.QuickSort(doubleData, n);
                    return (long)(n * Math.Log2(Math.Max(n, 2)));

                case 7:
                    Algorithms.TimSort(doubleData, n);
                    return (long)(n * Math.Log2(Math.Max(n, 2)));

                case 9:
                    Algorithms.LongestIncreasingSubsequence(intData, n);
                    return (long)n * n;

                case 10:
                    Algorithms.CycleSort(doubleData, n);
                    return (long)n * n;

                case 11:
                    Algorithms.ShellSort(intData, n);
                    return (long)(n * Math.Log2(Math.Max(n, 2)));

                case 12:
                    Algorithms.ResetSteps();
                    Algorithms.PowIterative(1.0001, n);
                    return Algorithms.StepCount;

                case 13:
                    Algorithms.ResetSteps();
                    Algorithms.PowRecursiveSafe(1.0001, n);
                    return Algorithms.StepCount;

                case 14:
                    Algorithms.ResetSteps();
                    Algorithms.PowBinary(1.0001, n);
                    return Algorithms.StepCount;

                default:
                    return -1;
            }
        }
        public static (Func<double, double> Func, string Label) GetComplexityInfo(string algorithmName)
        {
            if (algorithmName.Contains("Постоянная"))
                return (n => 1.0, "O(1)");

            if (algorithmName.Contains("Bubble") || algorithmName.Contains("Прямое вычисление") ||
                algorithmName.Contains("Cycle") || algorithmName.Contains("Циклическ") ||
                algorithmName.Contains("LIS") || algorithmName.Contains("подпоследовательн"))
                return (n => n * n, "O(N²)");

            if (algorithmName.Contains("Quick") || algorithmName.Contains("TimSort"))
                return (n => n * Math.Log2(Math.Max(n, 2)), "O(N log N)");

            if (algorithmName.Contains("Shell"))
                return (n => Math.Pow(Math.Max(n, 1), 1.25), "O(N^1.25)");

            if (algorithmName.Contains("быстрый") || algorithmName.Contains("бинарный") || algorithmName.Contains("PowBinary"))
                return (n => Math.Log2(Math.Max(n, 2)), "O(log N)");

            return (n => n, "O(N)");
        }
    }
}
