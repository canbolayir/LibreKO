using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LibreKO.Common.Domain.Entities.GameData;

public class EventDurationData
{
    public byte Kind { get; set; }
    public int Minutes { get; set; }

    internal class EntityConfiguration : IEntityTypeConfiguration<EventDurationData>
    {
        public void Configure(EntityTypeBuilder<EventDurationData> builder)
        {
            builder.ToTable("EventDurations");
            builder.HasKey(p => p.Kind);
            builder.Property(p => p.Kind).ValueGeneratedNever();
        }
    }
}
