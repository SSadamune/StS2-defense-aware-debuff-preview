using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using DefenseAwareDebuffPreview.Api;
using System.Reflection;

namespace DefenseAwareDebuffPreview.Patches;

[HarmonyPatch(typeof(PoisonPower), nameof(PoisonPower.CalculateTotalDamageNextTurn))]
internal static class PoisonDamagePreviewPatch
{
    [HarmonyPrefix]
    private static bool CalculateWithDefenses(PoisonPower __instance, ref int __result)
    {
        if (!PoisonHealthBarPreviewScopePatch.IsActive)
        {
            return true;
        }

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

            return !DamageDebuffPreviewApi.TryCalculateTotalHpLoss(
                __instance.Owner,
                hits,
                ValueProp.Unblockable | ValueProp.Unpowered,
                out __result,
                DamagePreviewBlockTiming.AfterNextSideTurnStart);
        }
        catch (Exception exception)
        {
            MainFile.Logger.Error($"Failed to calculate defense-aware poison preview: {exception}");
            return true;
        }
    }
}

/// <summary>
/// Restricts the Poison model-method patch to the two vanilla health-bar rendering
/// paths that consume it. Calls made by combat logic keep the vanilla result.
/// </summary>
[HarmonyPatch]
internal static class PoisonHealthBarPreviewScopePatch
{
    [ThreadStatic]
    private static int _depth;

    internal static bool IsActive => _depth > 0;

    private static IEnumerable<MethodBase> TargetMethods()
    {
        foreach (string methodName in new[] { "RefreshForeground", "RefreshText" })
        {
            MethodInfo? method = AccessTools.DeclaredMethod(typeof(NHealthBar), methodName);
            if (method is null)
            {
                MainFile.Logger.Error(
                    $"Could not find NHealthBar.{methodName}; vanilla Poison preview will be retained.");
                continue;
            }

            yield return method;
        }
    }

    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    private static void EnterPreviewScope() => _depth++;

    [HarmonyFinalizer]
    [HarmonyPriority(Priority.Last)]
    private static Exception? ExitPreviewScope(Exception? __exception)
    {
        _depth = Math.Max(0, _depth - 1);
        return __exception;
    }
}
