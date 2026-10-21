using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.ValueProps;

namespace DefenseAwareDebuffPreview.Preview;

/// <summary>
/// Side-effect-free damage simulation for debuffs that deal one or more hits.
/// The calculation follows the same damage and HP-loss hooks as CreatureCmd.Damage,
/// but never consumes block, powers, relic counters, or HP.
/// </summary>
public static class DamageDebuffPreviewCalculator
{
    public static int CalculateTotalHpLoss(
        Creature target,
        IEnumerable<decimal> baseDamagePerHit,
        ValueProp props,
        Creature? dealer = null,
        CardModel? cardSource = null,
        CardPlay? cardPlay = null)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(baseDamagePerHit);

        var combatState = target.CombatState
            ?? throw new InvalidOperationException("Damage previews require a creature in combat.");
        var runState = combatState.RunState;
        var listeners = runState.IterateHookListeners(combatState).ToList();
        var defenses = new PreviewDefenseState(listeners, target, props, dealer, cardSource);
        decimal remainingBlock = target.PetOwner?.Creature.Block ?? target.Block;
        var remainingHp = target.CurrentHp;
        var totalHpLoss = 0;

        foreach (decimal baseDamage in baseDamagePerHit)
        {
            if (remainingHp <= 0)
            {
                break;
            }

            decimal damage = Hook.ModifyDamage(
                runState,
                combatState,
                target,
                dealer,
                baseDamage,
                props,
                cardSource,
                cardPlay,
                ModifyDamageHookType.All,
                CardPreviewMode.None,
                out _);

            decimal blockedDamage = props.HasFlag(ValueProp.Unblockable)
                ? 0m
                : Math.Min(remainingBlock, damage);
            remainingBlock = Math.Max(0m, remainingBlock - (int)blockedDamage);

            decimal hpLoss = Math.Max(damage - blockedDamage, 0m);
            hpLoss = defenses.ModifyBeforeOsty(target, hpLoss);

            Creature hpLossTarget = Hook.ModifyUnblockedDamageTarget(
                combatState,
                target,
                hpLoss,
                props,
                dealer);

            hpLoss = defenses.ModifyAfterOsty(hpLossTarget, hpLoss);
            int resolvedHpLoss = (int)Math.Clamp(hpLoss, 0m, 999999999m);

            // NHealthBar previews HP that will disappear, not overkill damage.
            int lossFromOriginalTarget = hpLossTarget == target
                ? Math.Min(resolvedHpLoss, remainingHp)
                : 0;

            totalHpLoss += lossFromOriginalTarget;
            remainingHp -= lossFromOriginalTarget;
            defenses.AfterHitResolved(hpLossTarget, resolvedHpLoss);
        }

        return totalHpLoss;
    }

    private sealed class PreviewDefenseState
    {
        private readonly IReadOnlyList<AbstractModel> _listeners;
        private readonly ValueProp _props;
        private readonly Creature? _dealer;
        private readonly CardModel? _cardSource;
        private readonly Dictionary<BufferPower, int> _bufferCharges = new();
        private readonly Dictionary<SlipperyPower, int> _slipperyCharges = new();
        private readonly Dictionary<HardenedShellPower, decimal> _hardenedShellRemaining = new();
        private readonly Dictionary<BeatingRemnant, decimal> _beatingRemnantRemaining = new();

        public PreviewDefenseState(
            IReadOnlyList<AbstractModel> listeners,
            Creature target,
            ValueProp props,
            Creature? dealer,
            CardModel? cardSource)
        {
            _listeners = listeners;
            _props = props;
            _dealer = dealer;
            _cardSource = cardSource;

            foreach (AbstractModel listener in listeners)
            {
                switch (listener)
                {
                    case BufferPower buffer when buffer.Owner == target:
                        _bufferCharges[buffer] = buffer.Amount;
                        break;
                    case SlipperyPower slippery when slippery.Owner == target:
                        _slipperyCharges[slippery] = slippery.Amount;
                        break;
                    case HardenedShellPower shell when shell.Owner == target:
                        _hardenedShellRemaining[shell] = Math.Max(
                            0m,
                            shell.ModifyHpLostBeforeOstyLate(
                                target,
                                decimal.MaxValue,
                                props,
                                dealer,
                                cardSource));
                        break;
                    case BeatingRemnant remnant when remnant.Owner.Creature == target:
                        _beatingRemnantRemaining[remnant] = Math.Max(
                            0m,
                            remnant.ModifyHpLostAfterOsty(
                                target,
                                decimal.MaxValue,
                                props,
                                dealer,
                                cardSource));
                        break;
                }
            }
        }

        public decimal ModifyBeforeOsty(Creature target, decimal amount)
        {
            foreach (AbstractModel listener in _listeners)
            {
                amount = listener.ModifyHpLostBeforeOsty(
                    target,
                    amount,
                    _props,
                    _dealer,
                    _cardSource);
            }

            foreach (AbstractModel listener in _listeners)
            {
                if (listener is HardenedShellPower shell &&
                    _hardenedShellRemaining.TryGetValue(shell, out decimal remaining))
                {
                    amount = Math.Min(amount, remaining);
                    continue;
                }

                amount = listener.ModifyHpLostBeforeOstyLate(
                    target,
                    amount,
                    _props,
                    _dealer,
                    _cardSource);
            }

            return amount;
        }

        public decimal ModifyAfterOsty(Creature target, decimal amount)
        {
            foreach (AbstractModel listener in _listeners)
            {
                if (listener is SlipperyPower slippery &&
                    _slipperyCharges.TryGetValue(slippery, out int charges))
                {
                    if (charges > 0)
                    {
                        amount = slippery.ModifyHpLostAfterOsty(
                            target,
                            amount,
                            _props,
                            _dealer,
                            _cardSource);
                    }
                    continue;
                }

                if (listener is BeatingRemnant remnant &&
                    _beatingRemnantRemaining.TryGetValue(remnant, out decimal remaining))
                {
                    amount = Math.Min(amount, remaining);
                    continue;
                }

                amount = listener.ModifyHpLostAfterOsty(
                    target,
                    amount,
                    _props,
                    _dealer,
                    _cardSource);
            }

            foreach (AbstractModel listener in _listeners)
            {
                if (listener is BufferPower buffer &&
                    _bufferCharges.TryGetValue(buffer, out int charges))
                {
                    if (charges > 0)
                    {
                        decimal modified = buffer.ModifyHpLostAfterOstyLate(
                            target,
                            amount,
                            _props,
                            _dealer,
                            _cardSource);

                        if (decimal.Truncate(modified) != decimal.Truncate(amount))
                        {
                            _bufferCharges[buffer] = charges - 1;
                        }

                        amount = modified;
                    }
                    continue;
                }

                amount = listener.ModifyHpLostAfterOstyLate(
                    target,
                    amount,
                    _props,
                    _dealer,
                    _cardSource);
            }

            return amount;
        }

        public void AfterHitResolved(Creature target, int hpLoss)
        {
            if (hpLoss <= 0)
            {
                return;
            }

            foreach (SlipperyPower slippery in _slipperyCharges.Keys.ToArray())
            {
                if (slippery.Owner == target && _slipperyCharges[slippery] > 0)
                {
                    _slipperyCharges[slippery]--;
                }
            }

            foreach (HardenedShellPower shell in _hardenedShellRemaining.Keys.ToArray())
            {
                if (shell.Owner == target)
                {
                    _hardenedShellRemaining[shell] = Math.Max(
                        0m,
                        _hardenedShellRemaining[shell] - hpLoss);
                }
            }

            foreach (BeatingRemnant remnant in _beatingRemnantRemaining.Keys.ToArray())
            {
                if (remnant.Owner.Creature == target)
                {
                    _beatingRemnantRemaining[remnant] = Math.Max(
                        0m,
                        _beatingRemnantRemaining[remnant] - hpLoss);
                }
            }
        }
    }
}
