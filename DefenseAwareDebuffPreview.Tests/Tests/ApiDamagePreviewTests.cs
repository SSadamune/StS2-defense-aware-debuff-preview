using DefenseAwareDebuffPreview.Api;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.ValueProps;
using TestTheSpire;
using Xunit;

namespace DefenseAwareDebuffPreview.Tests.Cases;

public sealed class ApiDamagePreviewTests : CombatTestSuite
{
    protected override void ConfigureBattle(CombatTestBattleBuilder battle) =>
        battle
            .Player<Ironclad>()
            .AddEnemy<BigDummy>()
            .WithSeed("defense-aware-debuff-preview-api");

    [Fact]
    public async Task Ordered_sources_share_defenses_and_preserve_damage_attribution()
    {
        await Play(await AddToHand<StrikeIronclad>(), EnemyAt(0));
        await CreatureCmd.GainBlock(Player.Creature, 2m, ValueProp.Unpowered, null);
        await ApplyPower<BufferPower>(Player.Creature, 1);

        var poison = new object();
        var burning = new object();
        int hpBefore = Player.Creature.CurrentHp;

        DamagePreviewResult<object> poisonFirst = DamageDebuffPreviewApi.CalculateBySource<object>(
            Player.Creature,
            [
                new(poison, [5m], ValueProp.Unblockable | ValueProp.Unpowered),
                new(burning, [4m], ValueProp.Unpowered),
            ],
            ReferenceEqualityComparer.Instance);

        Assert.Equal(2, poisonFirst.TotalHpLoss);
        Assert.Equal(0, poisonFirst.GetHpLoss(poison));
        Assert.Equal(2, poisonFirst.GetHpLoss(burning));

        DamagePreviewResult<object> burningFirst = DamageDebuffPreviewApi.CalculateBySource<object>(
            Player.Creature,
            [
                new(burning, [4m], ValueProp.Unpowered),
                new(poison, [5m], ValueProp.Unblockable | ValueProp.Unpowered),
            ],
            ReferenceEqualityComparer.Instance);

        Assert.Equal(5, burningFirst.TotalHpLoss);
        Assert.Equal(0, burningFirst.GetHpLoss(burning));
        Assert.Equal(5, burningFirst.GetHpLoss(poison));

        Assert.Equal(2, Player.Creature.Block);
        Assert.Equal(1, Player.Creature.GetPower<BufferPower>()?.Amount);
        Assert.Equal(hpBefore, Player.Creature.CurrentHp);
    }

    [Fact]
    public async Task Sources_share_hardened_shell_remaining_damage_cap()
    {
        await Play(await AddToHand<StrikeIronclad>(), EnemyAt(0));
        await ApplyPower<HardenedShellPower>(Player.Creature, 4);

        var poison = new object();
        var burning = new object();
        int hpBefore = Player.Creature.CurrentHp;

        DamagePreviewResult<object> preview = DamageDebuffPreviewApi.CalculateBySource<object>(
            Player.Creature,
            [
                new(poison, [3m], ValueProp.Unblockable | ValueProp.Unpowered),
                new(burning, [3m], ValueProp.Unpowered),
            ],
            ReferenceEqualityComparer.Instance);

        Assert.Equal(4, preview.TotalHpLoss);
        Assert.Equal(3, preview.GetHpLoss(poison));
        Assert.Equal(1, preview.GetHpLoss(burning));
        Assert.Equal(4, Player.Creature.GetPower<HardenedShellPower>()?.DisplayAmount);
        Assert.Equal(hpBefore, Player.Creature.CurrentHp);
    }

    [Fact]
    public async Task Sources_share_slippery_charges_in_order()
    {
        await Play(await AddToHand<StrikeIronclad>(), EnemyAt(0));
        await CreatureCmd.GainBlock(Player.Creature, 10m, ValueProp.Unpowered, null);
        await ApplyPower<SlipperyPower>(Player.Creature, 1);

        var poison = new object();
        var burning = new object();
        int hpBefore = Player.Creature.CurrentHp;

        DamagePreviewResult<object> preview = DamageDebuffPreviewApi.CalculateBySource<object>(
            Player.Creature,
            [
                new(poison, [5m], ValueProp.Unblockable | ValueProp.Unpowered),
                new(burning, [5m], ValueProp.Unpowered),
            ],
            DamagePreviewBlockTiming.AfterAutomaticClear,
            ReferenceEqualityComparer.Instance);

        Assert.Equal(6, preview.TotalHpLoss);
        Assert.Equal(1, preview.GetHpLoss(poison));
        Assert.Equal(5, preview.GetHpLoss(burning));
        Assert.Equal(10, Player.Creature.Block);
        Assert.Equal(1, Player.Creature.GetPower<SlipperyPower>()?.Amount);
        Assert.Equal(hpBefore, Player.Creature.CurrentHp);
    }

    [Fact]
    public async Task Damage_after_automatic_block_clear_ignores_block_that_will_clear()
    {
        await Play(await AddToHand<StrikeIronclad>(), EnemyAt(0));
        await CreatureCmd.GainBlock(Player.Creature, 6m, ValueProp.Unpowered, null);

        int hpBefore = Player.Creature.CurrentHp;

        int currentStatePreview = DamageDebuffPreviewApi.CalculateTotalHpLoss(
            Player.Creature,
            [4m],
            ValueProp.Unpowered);
        int afterClearPreview = DamageDebuffPreviewApi.CalculateTotalHpLoss(
            Player.Creature,
            [4m],
            ValueProp.Unpowered,
            DamagePreviewBlockTiming.AfterAutomaticClear);

        Assert.Equal(0, currentStatePreview);
        Assert.Equal(4, afterClearPreview);
        Assert.Equal(6, Player.Creature.Block);
        Assert.Equal(hpBefore, Player.Creature.CurrentHp);
    }

    [Fact]
    public async Task Damage_after_automatic_block_clear_retains_block_when_clear_is_prevented()
    {
        await Play(await AddToHand<StrikeIronclad>(), EnemyAt(0));
        await CreatureCmd.GainBlock(Player.Creature, 6m, ValueProp.Unpowered, null);
        await ApplyPower<BarricadePower>(Player.Creature, 1);

        int hpBefore = Player.Creature.CurrentHp;

        Assert.True(DamageDebuffPreviewApi.TryCalculateTotalHpLoss(
            Player.Creature,
            [4m],
            ValueProp.Unpowered,
            out int hpLoss,
            DamagePreviewBlockTiming.AfterAutomaticClear));

        Assert.Equal(0, hpLoss);
        Assert.Equal(6, Player.Creature.Block);
        Assert.NotNull(Player.Creature.GetPower<BarricadePower>());
        Assert.Equal(hpBefore, Player.Creature.CurrentHp);
    }

    [Fact]
    public async Task Any_number_of_unique_sources_are_reported_separately()
    {
        await Play(await AddToHand<StrikeIronclad>(), EnemyAt(0));

        var firstDebuff = new object();
        var secondDebuff = new object();
        var thirdDebuff = new object();
        int hpBefore = Player.Creature.CurrentHp;

        DamagePreviewResult<object> preview = DamageDebuffPreviewApi.CalculateBySource<object>(
            Player.Creature,
            [
                new(firstDebuff, [2m], ValueProp.Unblockable | ValueProp.Unpowered),
                new(secondDebuff, [3m], ValueProp.Unblockable | ValueProp.Unpowered),
                new(thirdDebuff, [4m], ValueProp.Unblockable | ValueProp.Unpowered),
            ],
            ReferenceEqualityComparer.Instance);

        Assert.Equal(9, preview.TotalHpLoss);
        Assert.Equal(2, preview.GetHpLoss(firstDebuff));
        Assert.Equal(3, preview.GetHpLoss(secondDebuff));
        Assert.Equal(4, preview.GetHpLoss(thirdDebuff));
        Assert.Equal(hpBefore, Player.Creature.CurrentHp);
    }

    [Fact]
    public async Task Try_api_contains_preview_failures_without_changing_combat_state()
    {
        await Play(await AddToHand<StrikeIronclad>(), EnemyAt(0));
        await CreatureCmd.GainBlock(Player.Creature, 3m, ValueProp.Unpowered, null);
        await ApplyPower<BufferPower>(Player.Creature, 1);

        int hpBefore = Player.Creature.CurrentHp;

        bool succeeded = DamageDebuffPreviewApi.TryCalculateTotalHpLoss(
            Player.Creature,
            ThrowAfterFirstHit(),
            ValueProp.Unpowered,
            out int hpLoss);

        Assert.False(succeeded);
        Assert.Equal(0, hpLoss);
        Assert.Equal(3, Player.Creature.Block);
        Assert.Equal(1, Player.Creature.GetPower<BufferPower>()?.Amount);
        Assert.Equal(hpBefore, Player.Creature.CurrentHp);
    }

    [Fact]
    public async Task Main_assembly_registers_no_content_model_types()
    {
        await Play(await AddToHand<StrikeIronclad>(), EnemyAt(0));

        Type[] contentModelTypes = typeof(MainFile)
            .Assembly
            .GetTypes()
            .Where(type => typeof(AbstractModel).IsAssignableFrom(type))
            .ToArray();

        Assert.Empty(contentModelTypes);
        Assert.False(ModManager.Mods
            .Single(mod => mod.manifest?.id == MainFile.ModId)
            .manifest?.affectsGameplay ?? true);
    }

    private static IEnumerable<decimal> ThrowAfterFirstHit()
    {
        yield return 5m;
        throw new InvalidOperationException("Intentional preview failure.");
    }
}
