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
        await ApplyPower<PoisonPower>(Player.Creature, 10);
        await ApplyPower<BufferPower>(Player.Creature, 1);

        PoisonPower poison = Assert.IsType<PoisonPower>(Player.Creature.GetPower<PoisonPower>());
        BufferPower buffer = Assert.IsType<BufferPower>(Player.Creature.GetPower<BufferPower>());
        int hpBefore = Player.Creature.CurrentHp;

        Assert.Equal(0, poison.CalculateTotalDamageNextTurn());
        Assert.Equal(1, buffer.Amount);
        Assert.Equal(hpBefore, Player.Creature.CurrentHp);

        await poison.Trigger();

        Assert.Equal(hpBefore, Player.Creature.CurrentHp);
    }

    [Fact]
    public async Task Intangible_caps_the_preview_and_actual_poison_damage()
    {
        await ExecuteTrackedAction();
        await ApplyPower<PoisonPower>(Player.Creature, 10);
        await ApplyPower<IntangiblePower>(Player.Creature, 1);

        PoisonPower poison = Assert.IsType<PoisonPower>(Player.Creature.GetPower<PoisonPower>());
        int hpBefore = Player.Creature.CurrentHp;

        Assert.Equal(1, poison.CalculateTotalDamageNextTurn());

        await poison.Trigger();

        Assert.Equal(hpBefore - 1, Player.Creature.CurrentHp);
    }

    [Fact]
    public async Task Multiple_hits_simulate_consumable_defenses_in_order()
    {
        await ExecuteTrackedAction();
        await ApplyPower<PoisonPower>(Player.Creature, 3);
        await ApplyPower<BufferPower>(Player.Creature, 1);
        await ApplyPower<AccelerantPower>(EnemyAt(0), 1);

        PoisonPower poison = Assert.IsType<PoisonPower>(Player.Creature.GetPower<PoisonPower>());
        int hpBefore = Player.Creature.CurrentHp;

        Assert.Equal(2, poison.CalculateTotalDamageNextTurn());
        Assert.Equal(1, Player.Creature.GetPower<BufferPower>()?.Amount);

        await poison.Trigger();

        Assert.Equal(hpBefore - 2, Player.Creature.CurrentHp);
    }

    private async Task ExecuteTrackedAction() =>
        await Play(await AddToHand<StrikeIronclad>(), EnemyAt(0));
}
