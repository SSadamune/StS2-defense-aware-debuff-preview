using System.Reflection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace DefenseAwareDebuffPreview.Compatibility;

/// <summary>
/// Bridges the Hook.ModifyDamage signature change between game versions 0.107 and 0.108.
/// </summary>
internal static class HookCompatibility
{
    private delegate decimal ModifyDamage107(
        IRunState runState,
        ICombatState combatState,
        Creature target,
        Creature? dealer,
        decimal amount,
        ValueProp props,
        CardModel? cardSource,
        ModifyDamageHookType hookType,
        CardPreviewMode previewMode,
        out IEnumerable<AbstractModel> modifiers);

    private delegate decimal ModifyDamage108(
        IRunState runState,
        ICombatState combatState,
        Creature target,
        Creature? dealer,
        decimal amount,
        ValueProp props,
        CardModel? cardSource,
        CardPlay? cardPlay,
        ModifyDamageHookType hookType,
        CardPreviewMode previewMode,
        out IEnumerable<AbstractModel> modifiers);

    private static readonly ModifyDamage107? ModifyDamage107Method;
    private static readonly ModifyDamage108? ModifyDamage108Method;

    static HookCompatibility()
    {
        MethodInfo method = ResolveModifyDamage();
        if (method.GetParameters().Length == 11)
        {
            ModifyDamage108Method = method.CreateDelegate<ModifyDamage108>();
        }
        else
        {
            ModifyDamage107Method = method.CreateDelegate<ModifyDamage107>();
        }
    }

    public static decimal ModifyDamage(
        IRunState runState,
        ICombatState combatState,
        Creature target,
        Creature? dealer,
        decimal amount,
        ValueProp props,
        CardModel? cardSource,
        CardPlay? cardPlay,
        ModifyDamageHookType hookType,
        CardPreviewMode previewMode,
        out IEnumerable<AbstractModel> modifiers)
    {
        if (ModifyDamage108Method is not null)
        {
            return ModifyDamage108Method(
                runState,
                combatState,
                target,
                dealer,
                amount,
                props,
                cardSource,
                cardPlay,
                hookType,
                previewMode,
                out modifiers);
        }

        return ModifyDamage107Method!(
            runState,
            combatState,
            target,
            dealer,
            amount,
            props,
            cardSource,
            hookType,
            previewMode,
            out modifiers);
    }

    private static MethodInfo ResolveModifyDamage()
    {
        MethodInfo[] candidates = typeof(Hook)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(method => method.Name == nameof(Hook.ModifyDamage))
            .Where(method => method.ReturnType == typeof(decimal))
            .Where(method => method.GetParameters().Length is 10 or 11)
            .ToArray();

        return candidates.SingleOrDefault(method => method.GetParameters().Length == 11)
            ?? candidates.SingleOrDefault(method => method.GetParameters().Length == 10)
            ?? throw new MissingMethodException(
                typeof(Hook).FullName,
                nameof(Hook.ModifyDamage));
    }
}
