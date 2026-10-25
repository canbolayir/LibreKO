namespace LibreKO.Game.World;

public enum RankMarkTier : byte
{
    Bronze = 0,
    Silver = 1,
    Gold = 2,
}

public sealed class RankMarks
{
    public const byte LastBoardMark = 7;
    public const byte KingRibbon = 0;
    public const byte LastRibbon = 4;
    public const int TierCount = 3;

    private readonly object marksLock = new();
    private readonly SortedDictionary<byte, ushort[]> boardMarks = [];
    private readonly SortedDictionary<byte, ushort> ribbons = [];

    public IReadOnlyList<(byte Type, ushort Bronze, ushort Silver, ushort Gold)> Boards
    {
        get
        {
            lock (marksLock)
                return boardMarks.Select(mark => (mark.Key, mark.Value[0], mark.Value[1], mark.Value[2])).ToList();
        }
    }

    public IReadOnlyList<(byte Id, ushort Count)> Ribbons
    {
        get
        {
            lock (marksLock)
                return ribbons.Select(ribbon => (ribbon.Key, ribbon.Value)).ToList();
        }
    }

    public bool Award(byte type, RankMarkTier tier)
    {
        if (type > LastBoardMark || !Enum.IsDefined(tier))
            return false;
        lock (marksLock)
        {
            if (!boardMarks.TryGetValue(type, out var counts))
                boardMarks[type] = counts = new ushort[TierCount];
            counts[(int)tier] = (ushort)Math.Min(ushort.MaxValue, counts[(int)tier] + 1);
        }
        return true;
    }

    public bool AwardRibbon(byte id)
    {
        if (id > LastRibbon)
            return false;
        lock (marksLock)
            ribbons[id] = (ushort)Math.Min(ushort.MaxValue, ribbons.GetValueOrDefault(id) + 1);
        return true;
    }

    public byte[] Serialize()
    {
        lock (marksLock)
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);
            writer.Write((byte)boardMarks.Count);
            foreach (var (type, counts) in boardMarks)
            {
                writer.Write(type);
                foreach (var count in counts)
                    writer.Write(count);
            }
            writer.Write((byte)ribbons.Count);
            foreach (var (id, count) in ribbons)
            {
                writer.Write(id);
                writer.Write(count);
            }
            writer.Flush();
            return stream.ToArray();
        }
    }

    public static RankMarks Load(byte[]? data)
    {
        var marks = new RankMarks();
        if (data is not { Length: > 0 })
            return marks;
        try
        {
            using var reader = new BinaryReader(new MemoryStream(data));
            int boards = reader.ReadByte();
            for (int i = 0; i < boards; i++)
            {
                byte type = reader.ReadByte();
                var counts = new ushort[TierCount];
                for (int tier = 0; tier < TierCount; tier++)
                    counts[tier] = reader.ReadUInt16();
                if (type <= LastBoardMark)
                    marks.boardMarks[type] = counts;
            }
            int ribbons = reader.ReadByte();
            for (int i = 0; i < ribbons; i++)
            {
                byte id = reader.ReadByte();
                ushort count = reader.ReadUInt16();
                if (id <= LastRibbon)
                    marks.ribbons[id] = count;
            }
        }
        catch (EndOfStreamException)
        {
        }
        return marks;
    }

    public static RankMarkTier? TierFor(int place, int goldPlaces, int silverPlaces, int bronzePlaces) => place switch
    {
        <= 0 => null,
        _ when place <= goldPlaces => RankMarkTier.Gold,
        _ when place <= silverPlaces => RankMarkTier.Silver,
        _ when place <= bronzePlaces => RankMarkTier.Bronze,
        _ => null,
    };
}
