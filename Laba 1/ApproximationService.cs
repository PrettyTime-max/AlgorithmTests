using System;

namespace Laba_1
{
    public static class ApproximationService
    {
        public class Result
        {
            public double C { get; set; }
            public double[] ApproximateValues { get; set; } = Array.Empty<double>();
            public double Mse { get; set; }
        }
        public static Result Calculate(int[] nValues, double[] empiricalValues, Func<double, double> theoreticalFunc)
        {
            int k = nValues.Length;
            if (k == 0 || k != empiricalValues.Length)
            {
                throw new ArgumentException("Массивы данных пустые или имеют разную длину.");
            }

            double[] fValues = new double[k];
            double numerator = 0;   // Числитель для метода наименьших квадратов
            double denominator = 0; // Знаменатель для метода наименьших квадратов

            // Шаг 1: Вычисляем f(n) для каждой точки
            for (int i = 0; i < k; i++)
            {
                fValues[i] = theoreticalFunc(nValues[i]);
                numerator += empiricalValues[i] * fValues[i];
                denominator += fValues[i] * fValues[i];
            }

            // Находим оптимальный коэффициент C
            double c = denominator != 0 ? numerator / denominator : 1.0;

            // Шаг 2: Вычисляем теоретические значения T_approx(n) и MSE
            double[] approxValues = new double[k];
            double sumSquaredError = 0;

            for (int i = 0; i < k; i++)
            {
                approxValues[i] = c * fValues[i];
                double error = empiricalValues[i] - approxValues[i];
                sumSquaredError += error * error;
            }

            double mse = sumSquaredError / k;

            return new Result
            {
                C = c,
                ApproximateValues = approxValues,
                Mse = mse
            };
        }
    }
}