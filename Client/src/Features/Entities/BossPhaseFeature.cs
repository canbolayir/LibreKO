using Godot;

namespace LibreKO.Features.Entities;

internal sealed class BossPhaseFeature : IFeature, ITickFeature
{
    private const float ShoutHeight = 0.4f;
    private const double ShoutHold = 3.0;
    private const double ShoutFadeIn = 1.5;
    private const int ShoutFontSize = 28;
    private static readonly Color ShoutColour = new(1f, 1f, 0f);

    private readonly IWorldContext context;
    private readonly EntitySystem entities;
    private readonly ScenerySystem scenery;
    private readonly CaptionFeature caption;
    private readonly MoviePlayerFeature movie;
    private readonly BossShoutQueue shouts = new();
    private int shoutZone = -1;

    public BossPhaseFeature(IWorldContext context, EntitySystem entities, ScenerySystem scenery, CaptionFeature caption, MoviePlayerFeature movie)
    {
        this.context = context;
        this.entities = entities;
        this.scenery = scenery;
        this.caption = caption;
        this.movie = movie;
    }

    public void Build() => entities.EntitySpawned += OnSpawned;

    public void Dispose() => entities.EntitySpawned -= OnSpawned;

    private void OnSpawned(EntityView e, EntitySnapshot info)
    {
        if (e.BossPhase is not { } phase) return;
        if (phase.Weather > 0) scenery.ShowWeather(phase.Weather, BossPhases.WeatherAmount);
        if (BossPhases.Table.Shout(phase.ShoutId) is { } shout) shouts.Enqueue(e.Id, shout);
    }

    public void Tick(double delta, double nowSec)
    {
        if (context.Zone != shoutZone)
        {
            shoutZone = context.Zone;
            shouts.Clear();
        }
        if (shouts.Next(nowSec, movie.Playing, context.Entities.Views.ContainsKey) is not { } next || !next.Shout.Banner) return;
        caption.Show(next.Shout.Text, ShoutColour, ShoutHold, ShoutFadeIn,
            context.Root.GetViewport().GetVisibleRect().Size.Y * ShoutHeight, ShoutFontSize);
    }
}
