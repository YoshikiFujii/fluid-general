using System.Collections.Generic;
using System.Text.Json;
using fluid_general.Models;
using Microsoft.EntityFrameworkCore;
using System.IO;

namespace fluid_general.Data
{
    public class AppDbContext : DbContext
    {
        public DbSet<Member> Members { get; set; } = null!;
        public DbSet<EventConfig> Events { get; set; } = null!;
        public DbSet<CheckInLog> CheckInLogs { get; set; } = null!;

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                // アプリのデータフォルダにデータベースを作成
                var dbPath = Path.Combine(App.AppDataPath, "fluid_general.db");
                optionsBuilder.UseSqlite($"Data Source={dbPath}");
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Memberの自由項目(CustomFields)をJSON文字列としてSQLiteに保存する設定
            modelBuilder.Entity<Member>()
                .Property(e => e.CustomFields)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<Dictionary<string, string>>(v, (JsonSerializerOptions?)null) ?? new Dictionary<string, string>()
                );
        }
    }
}
