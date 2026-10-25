using System.IO;
using System.Text.Json;
using LibreKO.Common.Infrastructure.Persistence.Seed;
using Microsoft.Extensions.Logging;

namespace LibreKO.Game.World;

public sealed record ManesSurvivalLook(byte Row, short Class, short Race, int Right, int Left, int[] Armour);

public sealed record ManesSurvivalPick(
    short Code, byte Kind, int Magic, int Item, short Count, int Price, short Hp, short AttackUp, short AttackDown)
{
    public const byte PassiveKind = 5;
    public const byte PotionKind = 100;
    private const int GroupScale = 100;

    public int Group => Code / GroupScale;
    public int Level => Code % GroupScale;
    public bool IsPassive => Kind == PassiveKind;
    public bool IsPotion => Kind == PotionKind;
    public bool IsSkill => !IsPassive && !IsPotion;
}

public sealed record ManesSurvivalTableData(List<ManesSurvivalLook> Looks, List<ManesSurvivalPick> Picks);

public interface IManesSurvivalTable
{
    IReadOnlyList<ManesSurvivalLook> Looks { get; }
    IReadOnlyList<ManesSurvivalPick> Picks { get; }
    ManesSurvivalPick? Pick(short code);
}

public sealed class ManesSurvivalTable : IManesSurvivalTable
{
    public const string FileName = "ManesSurvival.json";

    private readonly Dictionary<short, ManesSurvivalPick> picksByCode;

    public ManesSurvivalTable(ILogger<ManesSurvivalTable>? logger = null)
        : this(Load(SeedDataLocation.DataPath(FileName), logger))
    {
    }

    public ManesSurvivalTable(ManesSurvivalTableData data)
    {
        Looks = data.Looks;
        Picks = data.Picks;
        picksByCode = data.Picks.ToDictionary(pick => pick.Code);
    }

    public IReadOnlyList<ManesSurvivalLook> Looks { get; }
    public IReadOnlyList<ManesSurvivalPick> Picks { get; }

    public ManesSurvivalPick? Pick(short code) => picksByCode.GetValueOrDefault(code);

    private static ManesSurvivalTableData Load(string path, ILogger? logger)
    {
        if (!File.Exists(path))
        {
            logger?.LogWarning("{File} not found at {Path}; Manes' Survival has no skill table", FileName, path);
            return new ManesSurvivalTableData([], []);
        }

        var data = JsonSerializer.Deserialize<ManesSurvivalTableData>(File.ReadAllText(path),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        return data ?? throw new InvalidOperationException($"Failed to deserialize '{path}'.");
    }
}
