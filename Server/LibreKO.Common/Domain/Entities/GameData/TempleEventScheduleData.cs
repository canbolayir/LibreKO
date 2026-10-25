using LibreKO.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LibreKO.Common.Domain.Entities.GameData;

public class TempleEventScheduleData : IScheduleRule
{
    public int Id { get; set; }
    public TempleEvent Event { get; set; }
    public ScheduleRepeat Repeat { get; set; }
    public DateOnly? Date { get; set; }
    public DayOfWeek? Day { get; set; }
    public byte DayOfMonth { get; set; }
    public byte Hour { get; set; }
    public byte Minute { get; set; }
    public byte MinLevel { get; set; } = 20;
    public byte MaxLevel { get; set; } = 83;
    public byte CountdownMinutes { get; set; } = 10;

    internal class EntityConfiguration : IEntityTypeConfiguration<TempleEventScheduleData>
    {
        public void Configure(EntityTypeBuilder<TempleEventScheduleData> builder)
        {
            builder.ToTable("TempleEventSchedules");
            builder.HasKey(p => p.Id);
            builder.Property(p => p.Id).ValueGeneratedNever();
            builder.Property(p => p.MinLevel).HasDefaultValue((byte)20);
            builder.Property(p => p.MaxLevel).HasDefaultValue((byte)83);
            builder.Property(p => p.CountdownMinutes).HasDefaultValue((byte)10);
            builder.HasIndex(p => p.Event);
        }
    }
}
