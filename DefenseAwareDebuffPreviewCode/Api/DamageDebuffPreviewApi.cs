using System.Diagnostics.CodeAnalysis;
using DefenseAwareDebuffPreview.Preview;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace DefenseAwareDebuffPreview.Api;

/// <summary>
/// Selects which block state is used at the beginning of a damage preview.
/// </summary>
public enum DamagePreviewBlockTiming
{
    /// <summary>Uses the target's block at the time the preview is calculated.</summary>
    Current = 0,

    /// <summary>
    /// Simulates the automatic block-clear step at the start of the target's next
    /// turn. Block is set to zero when the game's block-clear hooks allow it and
    /// retained otherwise.
    /// </summary>
    AfterAutomaticClear = 1,

    /// <summary>
    /// Simulates the point immediately before start-of-turn damage effects run on
    /// the target's next side turn. Automatic block clear is applied, and known
    /// per-turn defenses such as Hardened Shell and Beating Remnant are reset.
    /// </summary>
    AfterNextSideTurnStart = 2,
}

/// <summary>
/// One ordered damage source in a shared debuff preview.
/// Sources and their hits are simulated in enumeration order.
/// </summary>
public sealed record DamagePreviewSource<TSource>(
    TSource Source,
    IEnumerable<decimal> BaseDamagePerHit,
    ValueProp Props,
    Creature? Dealer = null,
    CardModel? CardSource = null,
    CardPlay? CardPlay = null)
    where TSource : notnull;

/// <summary>Side-effect-free HP loss attributed to each preview source.</summary>
public sealed class DamagePreviewResult<TSource>
    where TSource : notnull
{
    private readonly IReadOnlyDictionary<TSource, int> _hpLossBySource;

    internal DamagePreviewResult(
        int totalHpLoss,
        IReadOnlyDictionary<TSource, int> hpLossBySource)
    {
        TotalHpLoss = totalHpLoss;
        _hpLossBySource = hpLossBySource;
    }

    public int TotalHpLoss { get; }

    public IReadOnlyDictionary<TSource, int> HpLossBySource => _hpLossBySource;

    public int GetHpLoss(TSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return _hpLossBySource.TryGetValue(source, out int hpLoss) ? hpLoss : 0;
    }
}

/// <summary>
/// Stable public entry points for defense-aware damage debuff previews.
/// All calculations are side-effect-free.
/// </summary>
public static class DamageDebuffPreviewApi
{
    /// <summary>
    /// Simulates several ordered damage sources against one shared defensive state and
    /// returns the HP loss attributed to each source.
    /// </summary>
    public static DamagePreviewResult<TSource> CalculateBySource<TSource>(
        Creature target,
        IEnumerable<DamagePreviewSource<TSource>> sources,
        IEqualityComparer<TSource>? sourceComparer = null)
        where TSource : notnull =>
        CalculateBySource(
            target,
            sources,
            DamagePreviewBlockTiming.Current,
            sourceComparer);

    /// <summary>
    /// Simulates several ordered damage sources against one shared defensive state,
    /// beginning with the block state selected by <paramref name="blockTiming"/>.
    /// </summary>
    public static DamagePreviewResult<TSource> CalculateBySource<TSource>(
        Creature target,
        IEnumerable<DamagePreviewSource<TSource>> sources,
        DamagePreviewBlockTiming blockTiming,
        IEqualityComparer<TSource>? sourceComparer = null)
        where TSource : notnull =>
        DamageDebuffPreviewCalculator.CalculateBySourceCore(
            target,
            sources,
            blockTiming,
            sourceComparer);

    /// <summary>
    /// Attempts to simulate several ordered damage sources for a local UI preview.
    /// Returns <see langword="false"/> instead of allowing a preview failure to
    /// interrupt the caller; callers should retain their original display in that case.
    /// </summary>
    public static bool TryCalculateBySource<TSource>(
        Creature target,
        IEnumerable<DamagePreviewSource<TSource>> sources,
        [NotNullWhen(true)] out DamagePreviewResult<TSource>? result,
        IEqualityComparer<TSource>? sourceComparer = null)
        where TSource : notnull
        => TryCalculateBySource(
            target,
            sources,
            out result,
            DamagePreviewBlockTiming.Current,
            sourceComparer);

    /// <summary>
    /// Attempts to simulate several ordered damage sources beginning with the block
    /// state selected by <paramref name="blockTiming"/>.
    /// </summary>
    public static bool TryCalculateBySource<TSource>(
        Creature target,
        IEnumerable<DamagePreviewSource<TSource>> sources,
        [NotNullWhen(true)] out DamagePreviewResult<TSource>? result,
        DamagePreviewBlockTiming blockTiming,
        IEqualityComparer<TSource>? sourceComparer = null)
        where TSource : notnull
    {
        try
        {
            result = CalculateBySource(target, sources, blockTiming, sourceComparer);
            return true;
        }
        catch (Exception exception)
        {
            MainFile.Logger.Error($"Failed to calculate a defense-aware multi-source preview: {exception}");
            result = null;
            return false;
        }
    }

    /// <summary>Simulates one damage source and returns its total HP loss.</summary>
    public static int CalculateTotalHpLoss(
        Creature target,
        IEnumerable<decimal> baseDamagePerHit,
        ValueProp props,
        Creature? dealer = null,
        CardModel? cardSource = null,
        CardPlay? cardPlay = null) =>
        DamageDebuffPreviewCalculator.CalculateTotalHpLoss(
            target,
            baseDamagePerHit,
            props,
            DamagePreviewBlockTiming.Current,
            dealer,
            cardSource,
            cardPlay);

    /// <summary>
    /// Simulates one damage source beginning with the block state selected by
    /// <paramref name="blockTiming"/> and returns its total HP loss.
    /// </summary>
    public static int CalculateTotalHpLoss(
        Creature target,
        IEnumerable<decimal> baseDamagePerHit,
        ValueProp props,
        DamagePreviewBlockTiming blockTiming,
        Creature? dealer = null,
        CardModel? cardSource = null,
        CardPlay? cardPlay = null) =>
        DamageDebuffPreviewCalculator.CalculateTotalHpLoss(
            target,
            baseDamagePerHit,
            props,
            blockTiming,
            dealer,
            cardSource,
            cardPlay);

    /// <summary>
    /// Attempts to simulate one damage source for a local UI preview. Returns
    /// <see langword="false"/> instead of allowing a preview failure to interrupt
    /// the caller; callers should retain their original display in that case.
    /// </summary>
    public static bool TryCalculateTotalHpLoss(
        Creature target,
        IEnumerable<decimal> baseDamagePerHit,
        ValueProp props,
        out int hpLoss,
        Creature? dealer = null,
        CardModel? cardSource = null,
        CardPlay? cardPlay = null)
        => TryCalculateTotalHpLoss(
            target,
            baseDamagePerHit,
            props,
            out hpLoss,
            DamagePreviewBlockTiming.Current,
            dealer,
            cardSource,
            cardPlay);

    /// <summary>
    /// Attempts to simulate one damage source beginning with the block state selected
    /// by <paramref name="blockTiming"/>.
    /// </summary>
    public static bool TryCalculateTotalHpLoss(
        Creature target,
        IEnumerable<decimal> baseDamagePerHit,
        ValueProp props,
        out int hpLoss,
        DamagePreviewBlockTiming blockTiming,
        Creature? dealer = null,
        CardModel? cardSource = null,
        CardPlay? cardPlay = null)
    {
        try
        {
            hpLoss = CalculateTotalHpLoss(
                target,
                baseDamagePerHit,
                props,
                blockTiming,
                dealer,
                cardSource,
                cardPlay);
            return true;
        }
        catch (Exception exception)
        {
            MainFile.Logger.Error($"Failed to calculate a defense-aware preview: {exception}");
            hpLoss = 0;
            return false;
        }
    }
}
