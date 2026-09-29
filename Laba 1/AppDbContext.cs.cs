using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace Laba_1
{
    // Модель сессии замера для отображения в выпадающем списке
    public class ExperimentSession
    {
        public int Id { get; set; }
        public string AlgorithmName { get; set; } = string.Empty;
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

    public class BenchmarkResult
    {
        public int Id { get; set; }
        public string AlgorithmName { get; set; } = string.Empty; // Название алгоритма
        public int N { get; set; }                               // Размер входных данных 
        public int RunNumber { get; set; }                       // Номер запуска 
        public double ExecutionTimeMs { get; set; }              // Затраченное время (мс)
        public long? StepCount { get; set; }                    // Количество шагов (зависит от алгоритма)
        public DateTime ExperimentDate { get; set; } = DateTime.UtcNow; // Дата эксперимента
    }

    public class AppDbContext : DbContext
    {
        public DbSet<ExperimentSession> ExperimentSessions { get; set; } = null!;
        public DbSet<BenchmarkResult> BenchmarkResults { get; set; } = null!;

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                // Указать свои данные для подключения к PostgreSQL (БД)
                string connectionString = "Host=localhost;Port=5432;Database=BenchmarkDb;Username=postgres;Password=your_password";
                optionsBuilder.UseNpgsql(connectionString);
            }
        }

        public static async Task InitDatabaseAsync()
        {
            using var db = new AppDbContext();
            await db.Database.EnsureCreatedAsync();
        }

        // Поиск ранее проведенной сессии по входным параметрам
        public static async Task<ExperimentSession?> FindExistingSessionAsync(string algorithmName, int startN, int endN, int step)
        {
            using var db = new AppDbContext();
            return await db.ExperimentSessions
                .Where(s => s.AlgorithmName == algorithmName
                         && s.StartN == startN
                         && s.EndN == endN
                         && s.Step == step)
                .OrderByDescending(s => s.ExperimentDate)
                .FirstOrDefaultAsync();
        }

        public static async Task SaveResultsAsync(List<BenchmarkResult> results, string algorithmName, int startN, int endN, int step)
        {
            if (results == null || results.Count == 0) return;

            using (var context = new AppDbContext())
            {
                var now = DateTime.UtcNow;

                // Проставляем единую дату эксперимента для всех результатов замеров
                foreach (var res in results)
                {
                    res.ExperimentDate = now;
                }

                // Создаем объект сессии с параметрами диапазона
                var session = new ExperimentSession
                {
                    AlgorithmName = algorithmName,
                    StartN = startN,
                    EndN = endN,
                    Step = step,
                    MinN = startN,
                    MaxN = endN,
                    CreatedAt = now,
                    ExperimentDate = now,
                    TotalRecords = results.Count
                };

                try
                {
                    context.ExperimentSessions.Add(session);
                    await context.SaveChangesAsync();

                    await context.BenchmarkResults.AddRangeAsync(results);
                    await context.SaveChangesAsync();
                }
                catch (DbUpdateException ex)
                {
                    string innerMsg = ex.InnerException?.Message ?? ex.Message;
                    MessageBox.Show($"Ошибка БД: {innerMsg}");
                    throw;
                }
            }
        }

        public static async Task<List<ExperimentSession>> GetExperimentSessionsAsync()
        {
            using var db = new AppDbContext();
            return await db.ExperimentSessions
                .OrderByDescending(s => s.ExperimentDate)
                .ToListAsync();
        }

        // Получить замеры для конкретной выбранной сессии
        public static async Task<List<BenchmarkResult>> GetResultsForSessionAsync(DateTime date, string algorithmName)
        {
            using var db = new AppDbContext();
            return await db.BenchmarkResults
                .Where(r => r.ExperimentDate == date && r.AlgorithmName == algorithmName)
                .OrderBy(r => r.N)
                .ThenBy(r => r.RunNumber)
                .ToListAsync();
        }

        // Кэширование
        public static async Task<List<BenchmarkResult>> GetCachedResultsAsync(string algorithmName, int n)
        {
            using var db = new AppDbContext();
            return await db.BenchmarkResults
                .Where(r => r.AlgorithmName == algorithmName && r.N == n)
                .OrderBy(r => r.RunNumber)
                .ToListAsync();
        }

        // Получить абсолютно все сохраненные замеры из БД
        public static async Task<List<BenchmarkResult>> GetAllResultsAsync()
        {
            using var db = new AppDbContext();
            return await db.BenchmarkResults
                .OrderByDescending(r => r.Id)
                .ToListAsync();
        }

        // Очистить все таблицы
        public static async Task ClearAllResultsAsync()
        {
            using var db = new AppDbContext();
            db.BenchmarkResults.RemoveRange(db.BenchmarkResults);
            db.ExperimentSessions.RemoveRange(db.ExperimentSessions);
            await db.SaveChangesAsync();
        }
    }
}