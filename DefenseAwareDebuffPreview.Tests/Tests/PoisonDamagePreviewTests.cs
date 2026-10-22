using System.Reflection;
using DefenseAwareDebuffPreview.Api;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using TestTheSpire;
using Xunit;

namespace DefenseAwareDebuffPreview.Tests.Cases;

public sealed class PoisonDamagePreviewTests : CombatTestSuite
{
    protected override void ConfigureBattle(CombatTestBattleBuilder battle) =>
        battle
            .Player<Ironclad>()
            .AddEnemy<BigDummy>()
            .WithSeed("defense-aware-debuff-preview");

    [Fact]
    public async Task Buffer_is_included_without_being_consumed_by_preview()
    {
        await ExecuteTrackedAction();
        await ApplyPower<PoisonPower>(EnemyAt(0), 10);
        await ApplyPower<BufferPower>(EnemyAt(0), 1);

        PoisonPower poison = Assert.IsType<PoisonPower>(EnemyAt(0).GetPower<PoisonPower>());
        BufferPower buffer = Assert.IsType<BufferPower>(EnemyAt(0).GetPower<BufferPower>());
        int hpBefore = EnemyAt(0).CurrentHp;

        Assert.Equal(10, poison.CalculateTotalDamageNextTurn());
        Assert.Equal(0, CalculateDefenseAwarePreview(poison));
        Assert.Equal(1, buffer.Amount);
        Assert.Equal(hpBefore, EnemyAt(0).CurrentHp);

        await EndTurn();

        Assert.Equal(hpBefore, EnemyAt(0).CurrentHp);
    }

    [Fact]
    public async Task Intangible_caps_the_preview_and_actual_poison_damage()
    {
        await ExecuteTrackedAction();
        await ApplyPower<PoisonPower>(EnemyAt(0), 10);
        await ApplyPower<IntangiblePower>(EnemyAt(0), 1);

        PoisonPower poison = Assert.IsType<PoisonPower>(EnemyAt(0).GetPower<PoisonPower>());
        int hpBefore = EnemyAt(0).CurrentHp;

        Assert.Equal(1, poison.CalculateTotalDamageNextTurn());
        Assert.Equal(1, CalculateDefenseAwarePreview(poison));

        await EndTurn();

        Assert.Equal(hpBefore - 1, EnemyAt(0).CurrentHp);
    }

    [Fact]
    public async Task Multiple_hits_simulate_consumable_defenses_in_order()
    {
        await ExecuteTrackedAction();
        await ApplyPower<PoisonPower>(EnemyAt(0), 3);
        await ApplyPower<BufferPower>(EnemyAt(0), 1);
        await ApplyPower<AccelerantPower>(Player.Creature, 1);

        PoisonPower poison = Assert.IsType<PoisonPower>(EnemyAt(0).GetPower<PoisonPower>());
        int hpBefore = EnemyAt(0).CurrentHp;

        Assert.Equal(5, poison.CalculateTotalDamageNextTurn());
        Assert.Equal(2, CalculateDefenseAwarePreview(poison));
        Assert.Equal(1, EnemyAt(0).GetPower<BufferPower>()?.Amount);

        await EndTurn();

        Assert.Equal(hpBefore - 2, EnemyAt(0).CurrentHp);
    }

    [Fact]
    public async Task Poison_model_preview_is_replaced_only_inside_health_bar_scope()
    {
        await ExecuteTrackedAction();
        await ApplyPower<PoisonPower>(EnemyAt(0), 10);
        await ApplyPower<BufferPower>(EnemyAt(0), 1);

        PoisonPower poison = Assert.IsType<PoisonPower>(EnemyAt(0).GetPower<PoisonPower>());
        int hpBefore = EnemyAt(0).CurrentHp;
        Type scopeType = typeof(MainFile).Assembly.GetType(
            "DefenseAwareDebuffPreview.Patches.PoisonHealthBarPreviewScopePatch")
            ?? throw new InvalidOperationException("Poison health-bar preview scope patch was not found.");
        MethodInfo enterScope = scopeType.GetMethod(
            "EnterPreviewScope",
            BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("Poison preview scope entry method was not found.");
        MethodInfo exitScope = scopeType.GetMethod(
            "ExitPreviewScope",
            BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("Poison preview scope exit method was not found.");

        Assert.Equal(10, poison.CalculateTotalDamageNextTurn());

        enterScope.Invoke(null, null);
        try
        {
            Assert.Equal(0, poison.CalculateTotalDamageNextTurn());
        }
        finally
        {
            exitScope.Invoke(null, new object?[] { null });
        }

        Assert.Equal(10, poison.CalculateTotalDamageNextTurn());
        Assert.Equal(1, EnemyAt(0).GetPower<BufferPower>()?.Amount);
        Assert.Equal(hpBefore, EnemyAt(0).CurrentHp);
    }

    private async Task ExecuteTrackedAction() =>
        await Play(await AddToHand<StrikeIronclad>(), EnemyAt(0));

    private static int CalculateDefenseAwarePreview(PoisonPower poison)
    {
        var combatState = poison.Owner.CombatState
            ?? throw new InvalidOperationException("Poison preview test requires combat state.");
        int triggerCount = Math.Min(
            poison.Amount,
            1 + combatState
                .GetOpponentsOf(poison.Owner)
                .Where(creature => creature.IsAlive)
                .Sum(creature => creature.GetPowerAmount<AccelerantPower>()));

        Assert.True(DamageDebuffPreviewApi.TryCalculateTotalHpLoss(
            poison.Owner,
            Enumerable.Range(0, triggerCount)
                .Select(index => (decimal)(poison.Amount - index)),
            ValueProp.Unblockable | ValueProp.Unpowered,
            out int hpLoss));

        return hpLoss;
    }
}
