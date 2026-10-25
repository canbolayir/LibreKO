using LibreKO.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LibreKO.Common.Domain.Entities.GameData;

public class EventStartData : IScheduleRule
{
    public int Id { get; set; }
    public byte Kind { get; set; }
    public ScheduleRepeat Repeat { get; set; }
    public DateOnly? Date { get; set; }
    public DayOfWeek? Day { get; set; }
    public byte DayOfMonth { get; set; }
    public byte Hour { get; set; }
    public byte Minute { get; set; }

    internal class EntityConfiguration : IEntityTypeConfiguration<EventStartData>
    {
        public void Configure(EntityTypeBuilder<EventStartData> builder)
        {
            builder.ToTable("EventStarts");
            builder.HasKey(p => p.Id);
            builder.Property(p => p.Id).ValueGeneratedNever();
            builder.HasIndex(p => p.Kind);
        }
    }
}
