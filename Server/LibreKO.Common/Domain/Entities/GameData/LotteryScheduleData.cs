using LibreKO.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LibreKO.Common.Domain.Entities.GameData;

public class LotteryScheduleData : IScheduleRule
{
    public int Id { get; set; }
    public int LotteryId { get; set; }
    public ScheduleRepeat Repeat { get; set; }
    public DateOnly? Date { get; set; }
    public DayOfWeek? Day { get; set; }
    public byte DayOfMonth { get; set; }
    public byte Hour { get; set; }
    public byte Minute { get; set; }

    internal class EntityConfiguration : IEntityTypeConfiguration<LotteryScheduleData>
    {
        public void Configure(EntityTypeBuilder<LotteryScheduleData> builder)
        {
            builder.HasKey(p => p.Id);
            builder.Property(p => p.Id).ValueGeneratedNever();
            builder.HasIndex(p => p.LotteryId);
            builder.HasOne<LotteryEventData>()
                .WithMany()
                .HasForeignKey(p => p.LotteryId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
