using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LibreKO.Common.Domain.Entities.GameData;

public class RentalListingData
{
    public const int NameLength = 21;

    public int Id { get; set; }
    public int OwnerCharacterId { get; set; }
    public string OwnerName { get; set; } = string.Empty;
    public int ItemId { get; set; }
    public short Durability { get; set; }
    public byte Flag { get; set; }
    public int UniqueId { get; set; }
    public int DailyPrice { get; set; }
    public DateTime ListedAt { get; set; }
    public DateTime ListingEndsAt { get; set; }
    public int RenterCharacterId { get; set; }
    public DateTime? RentedUntil { get; set; }
    public long Earnings { get; set; }
    public bool Collected { get; set; }
    public int Version { get; set; }

    public bool RentedAt(DateTime now) => RentedUntil is { } until && until > now;

    public bool OpenAt(DateTime now) => !Collected && ListingEndsAt > now;

    public int WholeDaysLeft(DateTime now) => Collected ? 0 : Math.Max(0, (int)Math.Floor((ListingEndsAt - now).TotalDays));

    internal class EntityConfiguration : IEntityTypeConfiguration<RentalListingData>
    {
        public void Configure(EntityTypeBuilder<RentalListingData> builder)
        {
            builder.HasKey(p => p.Id);
            builder.Property(p => p.Id).ValueGeneratedNever();
            builder.Property(p => p.OwnerName).HasMaxLength(NameLength);
            builder.Property(p => p.Version).IsConcurrencyToken();
            builder.HasIndex(p => p.OwnerCharacterId);
            builder.HasIndex(p => p.ListingEndsAt);
        }
    }
}
