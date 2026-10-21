using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models.Powers;
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

        Assert.Equal(0, poison.CalculateTotalDamageNextTurn());
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

        Assert.Equal(2, poison.CalculateTotalDamageNextTurn());
        Assert.Equal(1, EnemyAt(0).GetPower<BufferPower>()?.Amount);

        await EndTurn();

        Assert.Equal(hpBefore - 2, EnemyAt(0).CurrentHp);
    }

    private async Task ExecuteTrackedAction() =>
        await Play(await AddToHand<StrikeIronclad>(), EnemyAt(0));
}
