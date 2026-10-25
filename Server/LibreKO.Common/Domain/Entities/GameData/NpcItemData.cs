using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LibreKO.Common.Domain.Entities.GameData;

public class NpcItemData
{
    public const int DropSlots = 7;

    public short Index { get; set; }
    public bool IsMonster { get; set; }
    public int Item1 { get; set; }
    public short Percent1 { get; set; }
    public int Item2 { get; set; }
    public short Percent2 { get; set; }
    public int Item3 { get; set; }
    public short Percent3 { get; set; }
    public int Item4 { get; set; }
    public short Percent4 { get; set; }
    public int Item5 { get; set; }
    public short Percent5 { get; set; }
    public int Item6 { get; set; }
    public short Percent6 { get; set; }
    public int Item7 { get; set; }
    public short Percent7 { get; set; }

    public (int ItemId, short Percent)[] GetDrops()
    {
        return
        [
            (Item1, Percent1),
            (Item2, Percent2),
            (Item3, Percent3),
            (Item4, Percent4),
            (Item5, Percent5),
            (Item6, Percent6),
            (Item7, Percent7)
        ];
    }

    public void SetDrop(int slot, int itemId, short percent)
    {
        switch (slot)
        {
            case 0: Item1 = itemId; Percent1 = percent; break;
            case 1: Item2 = itemId; Percent2 = percent; break;
            case 2: Item3 = itemId; Percent3 = percent; break;
            case 3: Item4 = itemId; Percent4 = percent; break;
            case 4: Item5 = itemId; Percent5 = percent; break;
            case 5: Item6 = itemId; Percent6 = percent; break;
            case 6: Item7 = itemId; Percent7 = percent; break;
            default: throw new ArgumentOutOfRangeException(nameof(slot));
        }
    }

    public bool HasAnyDrop =>
        Item1 > 0 || Item2 > 0 || Item3 > 0 || Item4 > 0 || Item5 > 0 || Item6 > 0 || Item7 > 0;

    internal class EntityConfiguration : IEntityTypeConfiguration<NpcItemData>
    {
        public void Configure(EntityTypeBuilder<NpcItemData> builder)
        {
            builder.HasKey(p => new { p.Index, p.IsMonster });
            builder.Property(p => p.Index).ValueGeneratedNever();
        }
    }
}
