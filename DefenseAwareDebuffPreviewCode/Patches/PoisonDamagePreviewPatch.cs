using HarmonyLib;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using DefenseAwareDebuffPreview.Preview;

namespace DefenseAwareDebuffPreview.Patches;

[HarmonyPatch(typeof(PoisonPower), nameof(PoisonPower.CalculateTotalDamageNextTurn))]
internal static class PoisonDamagePreviewPatch
{
    [HarmonyPrefix]
    private static bool CalculateWithDefenses(PoisonPower __instance, ref int __result)
    {
        try
        {
            var combatState = __instance.Owner.CombatState
                ?? throw new InvalidOperationException("Poison preview requires a creature in combat.");

            int triggerCount = Math.Min(
                __instance.Amount,
                1 + combatState
                    .GetOpponentsOf(__instance.Owner)
                    .Where(creature => creature.IsAlive)
                    .Sum(creature => creature.GetPowerAmount<AccelerantPower>()));

            IEnumerable<decimal> hits = Enumerable
                .Range(0, triggerCount)
                .Select(index => (decimal)(__instance.Amount - index));

            __result = DamageDebuffPreviewCalculator.CalculateTotalHpLoss(
                __instance.Owner,
                hits,
                ValueProp.Unblockable | ValueProp.Unpowered);
            return false;
        }
        catch (Exception exception)
        {
            MainFile.Logger.Error($"Failed to calculate defense-aware poison preview: {exception}");
            return true;
        }
    }
}
