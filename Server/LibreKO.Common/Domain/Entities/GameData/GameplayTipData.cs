using LibreKO.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LibreKO.Common.Domain.Entities.GameData;

public class GameplayTipData
{
    public const int MaxTextLength = 300;

    public int Id { get; set; }
    public GameLanguage Language { get; set; } = GameLanguage.English;
    public string Text { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;

    internal class EntityConfiguration : IEntityTypeConfiguration<GameplayTipData>
    {
        public void Configure(EntityTypeBuilder<GameplayTipData> builder)
        {
            builder.HasKey(p => p.Id);
            builder.Property(p => p.Id).ValueGeneratedNever();
            builder.Property(p => p.Text).HasMaxLength(MaxTextLength);
        }
    }
}

public class GameplayTipSettingsData
{
    public const int SettingsId = 1;
    public const int DefaultIntervalMinutes = 5;
    public const int MaxIntervalMinutes = 720;

    public int Id { get; set; } = SettingsId;
    public bool Enabled { get; set; } = true;
    public int IntervalMinutes { get; set; } = DefaultIntervalMinutes;

    internal class EntityConfiguration : IEntityTypeConfiguration<GameplayTipSettingsData>
    {
        public void Configure(EntityTypeBuilder<GameplayTipSettingsData> builder)
        {
            builder.HasKey(p => p.Id);
            builder.Property(p => p.Id).ValueGeneratedNever();
            builder.HasData(new GameplayTipSettingsData());
        }
    }
}
