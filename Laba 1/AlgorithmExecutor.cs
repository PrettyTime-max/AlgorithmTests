using System;

namespace Laba_1
{
    public static class AlgorithmExecutor
    {
        // Запускает алгоритм по его индексу и возвращает количество вычисленных операций (шагов)
        public static long Execute(int algorithmIndex, int[] intData, double[] doubleData, int n)
        {
            switch (algorithmIndex)
            {
                case 0:
                    Algorithms.Constant(intData, n);
                    return 1;

                case 1:
                    Algorithms.Sum(intData, n);
                    return n;

                case 2:
                    Algorithms.Product(intData, n);
                    return n;

                case 3:
                    Algorithms.PolyNaive(doubleData, 1.5, n);
                    return (long)n * n;

                case 4:
                    Algorithms.PolyHorner(doubleData, 1.5, n);
                    return n;

                case 5:
                    {
                        double[] copy = new double[n];
                        Array.Copy(doubleData, copy, n);
                        Algorithms.BubbleSort(copy, n);
                        return (long)n * n;
                    }

                case 6:
                    {
                        double[] copy = new double[n];
                        Array.Copy(doubleData, copy, n);
                        Algorithms.QuickSort(copy, n);
                        return (long)(n * Math.Log2(Math.Max(n, 1)));
                    }

                case 7:
                    {
                        double[] copy = new double[n];
                        Array.Copy(doubleData, copy, n);
                        Algorithms.TimSort(copy, n);
                        return (long)(n * Math.Log2(Math.Max(n, 1)));
                    }

                case 9:
                    Algorithms.HasDuplicates(intData, n);
                    return n;

                case 10:
                    {
                        int[] copy = new int[n];
                        Array.Copy(intData, copy, n);
                        Algorithms.ReverseArray(copy, n);
                        return n / 2;
                    }

                case 11:
                    {
                        int[] copy = new int[n];
                        Array.Copy(intData, copy, n);
                        Algorithms.ShellSort(copy, n);
                        return (long)(n * Math.Log2(Math.Max(n, 1)));
                    }

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

            if (algorithmName.Contains("Bubble") || algorithmName.Contains("HasDuplicates") || algorithmName.Contains("Прямое вычисление"))
                return (n => n * n, "O(N²)");

            if (algorithmName.Contains("Quick") || algorithmName.Contains("TimSort") || algorithmName.Contains("Shell"))
                return (n => n * Math.Log2(Math.Max(n, 1)), "O(N log N)");

            if (algorithmName.Contains("быстрый") || algorithmName.Contains("бинарный") || algorithmName.Contains("PowBinary"))
                return (n => Math.Log2(Math.Max(n, 1)), "O(log N)");

            return (n => n, "O(N)");
        }
    }
}