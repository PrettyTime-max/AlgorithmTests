using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Laba_1
{
    // Модель сессии замера для отображения в выпадающем списке
    public class ExperimentSession
    {
        public int Id { get; set; }
        public string AlgorithmName { get; set; }
        public int StartN { get; set; }
        public int EndN { get; set; }
        public int Step { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ExperimentDate { get; set; }
        public int MinN { get; set; }
        public int MaxN { get; set; }
        public int TotalRecords { get; set; }

        // Оформление элемента в выпадающем списке (Дата | Алгоритм | Диапазон N)
        public string DisplayText => $"{ExperimentDate.ToLocalTime():dd.MM.yyyy HH:mm:ss} | {AlgorithmName} (N={MinN}..{MaxN})";

        public override string ToString() => DisplayText;
    }

    // Схема таблицы результатов замеров (пункт 2 из ТЗ)
    public class BenchmarkResult
    {
        public int Id { get; set; }
        public string AlgorithmName { get; set; } = string.Empty; // Название алгоритма
        public int N { get; set; }                               // Размер входных данных (N)
        public int RunNumber { get; set; }                       // Номер запуска (1..5)
        public double ExecutionTimeMs { get; set; }              // Затраченное время (мс)
        public long? StepCount { get; set; }                    // Количество шагов
        public DateTime ExperimentDate { get; set; } = DateTime.UtcNow; // Дата эксперимента
    }

    public class AppDbContext : DbContext
    {
        public DbSet<BenchmarkResult> BenchmarkResults { get; set; } = null!;

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                // Укажите свои данные для подключения к PostgreSQL
                string connectionString = "Host=localhost;Port=5432;Database=AlgorithmBenchmarksDb;Username=postgres;Password=your_password";
                optionsBuilder.UseNpgsql(connectionString);
            }
        }

        public static async Task InitDatabaseAsync()
        {
            using var db = new AppDbContext();
            await db.Database.EnsureCreatedAsync();
        }

        public static async Task SaveResultsAsync(List<BenchmarkResult> results, string algorithmName, int startN, int endN, int step)
        {
            var session = new ExperimentSession
            {
                AlgorithmName = algorithmName,
                StartN = startN,
                EndN = endN,
                Step = step,
                CreatedAt = DateTime.Now
            };
            if (results == null || results.Count == 0) return;
            using (var context = new AppDbContext())
            {
                await context.BenchmarkResults.AddRangeAsync(results);
                await context.SaveChangesAsync();
            }
        }

        public static async Task ClearAllResultsAsync()
        {
            using (var context = new AppDbContext())
            {
                context.BenchmarkResults.RemoveRange(context.BenchmarkResults);
                await context.SaveChangesAsync();
            }
        }

        // 1. Получить список всех сохраненных сессий замеров (для выпадающего списка)
        public static async Task<List<ExperimentSession>> GetExperimentSessionsAsync()
        {
            using var db = new AppDbContext();

            var rawData = await db.BenchmarkResults
                .Select(r => new { r.ExperimentDate, r.AlgorithmName, r.N })
                .ToListAsync();

            return rawData
                .GroupBy(r => new { r.ExperimentDate, r.AlgorithmName })
                .Select(g => new ExperimentSession
                {
                    ExperimentDate = g.Key.ExperimentDate,
                    AlgorithmName = g.Key.AlgorithmName,
                    MinN = g.Min(x => x.N),
                    MaxN = g.Max(x => x.N),
                    TotalRecords = g.Count()
                })
                .OrderByDescending(s => s.ExperimentDate)
                .ToList();
        }

        // 2. Получить замеры для конкретной выбранной сессии
        public static async Task<List<BenchmarkResult>> GetResultsForSessionAsync(DateTime date, string algorithmName)
        {
            using var db = new AppDbContext();
            return await db.BenchmarkResults
                .Where(r => r.ExperimentDate == date && r.AlgorithmName == algorithmName)
                .OrderBy(r => r.N)
                .ThenBy(r => r.RunNumber)
                .ToListAsync();
        }

        // 3. МЕХАНИЗМ КЭШИРОВАНИЯ (пункт 3 из ТЗ):
        // Проверяет наличие уже рассчитанных результатов для комбинации "Алгоритм + N"
        public static async Task<List<BenchmarkResult>> GetCachedResultsAsync(string algorithmName, int n)
        {
            using var db = new AppDbContext();
            return await db.BenchmarkResults
                .Where(r => r.AlgorithmName == algorithmName && r.N == n)
                .OrderBy(r => r.RunNumber)
                .ToListAsync();
        }

        // 4. Получить абсолютно все сохраненные замеры из БД
        public static async Task<List<BenchmarkResult>> GetAllResultsAsync()
        {
            using var db = new AppDbContext();
            return await db.BenchmarkResults
                .OrderByDescending(r => r.Id)
                .ToListAsync();
        }

        // 5. Очистить все замеры в БД
        public static async Task ClearDatabaseAsync()
        {
            using var db = new AppDbContext();
            db.BenchmarkResults.RemoveRange(db.BenchmarkResults);
            await db.SaveChangesAsync();
        }

        // 6. Очистка кэша/памяти приложения
        public static void ForceClearMemoryCache()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }
    }
}