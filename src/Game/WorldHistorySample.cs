namespace Evolit.Game;

public sealed record WorldHistorySample(
    long Sequence,
    int? Day,
    string? GameTime,
    int CreaturePopulation,
    int PlantPopulation,
    int SpeciesCount,
    int SubspeciesCount)
{
    public bool HasWorldTime => Day.HasValue;

    public string AxisLabel => Day.HasValue
        ? $"День {Day.Value}"
        : $"Набл. {Sequence + 1}";

    public string TooltipLabel
    {
        get
        {
            if (!Day.HasValue)
                return $"Наблюдение {Sequence + 1}";

            return string.IsNullOrWhiteSpace(GameTime)
                ? $"День {Day.Value}"
                : $"День {Day.Value} · {GameTime}";
        }
    }
}
