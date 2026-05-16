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
        public DbSet<RosterConfig> RosterConfigs { get; set; } = null!;

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

            // Memberの主キーを「名簿名 ＋ エクセルID」に設定
            modelBuilder.Entity<Member>()
                .HasKey(m => new { m.RosterName, m.ExcelId });

            // 検索速度向上とデータ整合性のためのインデックス
            modelBuilder.Entity<CheckInLog>()
                .HasIndex(l => new { l.EventConfigId, l.RosterName, l.ExcelId })
                .IsUnique();

            modelBuilder.Entity<CheckInLog>()
                .HasIndex(l => new { l.RosterName, l.ExcelId });

            modelBuilder.Entity<CheckInLog>()
                .HasIndex(l => l.EventConfigId);

            // 名簿名とエクセルIDによるリレーション設定 (メンバーが見つからない場合もログを保持できるよう任意に設定)
            modelBuilder.Entity<CheckInLog>()
                .HasOne(l => l.Member)
                .WithMany()
                .HasForeignKey(l => new { l.RosterName, l.ExcelId })
                .HasPrincipalKey(m => new { m.RosterName, m.ExcelId })
                .IsRequired(false);

            // Memberの自由項目(CustomFields)をJSON文字列としてSQLiteに保存する設定
            modelBuilder.Entity<Member>()
                .Property(e => e.CustomFields)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<Dictionary<string, string>>(v, (JsonSerializerOptions?)null) ?? new Dictionary<string, string>()
                );

            // RosterConfigのマッピングリストをJSON文字列として保存する設定
            modelBuilder.Entity<RosterConfig>()
                .Property(e => e.Mappings)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<List<ColumnMapping>>(v, (JsonSerializerOptions?)null) ?? new List<ColumnMapping>()
                );

            modelBuilder.Entity<RosterConfig>()
                .Property(e => e.DisplayColumns)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>()
                );
        }
    }
}
