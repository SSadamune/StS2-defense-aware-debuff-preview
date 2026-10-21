# Defense-Aware Debuff Preview

作者 / Author: **Sadaki**

## 中文简介

这是一个基于 [TestTheSpireTemplateRitsu](https://github.com/shadowversebydmod/TestTheSpireTemplateRitsu) 创建的《杀戮尖塔 2》机制模组。它让伤害类减益的伤害预览正确计算目标的防御能力，从而显示真正会失去的生命值，而不只是减益的基础伤害。

## 功能

- 修复原版中毒生命条预览：预览会经过目标的伤害修改与生命损失修改钩子，而不是只显示中毒基础数值。
- 正确考虑伤害上限、无形、缓冲、滑溜、硬化外壳、Beating Remnant 等防御能力。
- 多段中毒会按实际触发顺序模拟可消耗防御；预览计算不会消耗能力、格挡或生命。
- 提供稳定的 `DefenseAwareDebuffPreview.Api.DamageDebuffPreviewApi`，其他模组可按来源、按结算顺序共同模拟多种伤害类减益。

该模组只修正预览，不修改 `CreatureCmd.Damage` 的实际伤害结算。

## English Overview

This is a *Slay the Spire 2* mechanics mod based on [TestTheSpireTemplateRitsu](https://github.com/shadowversebydmod/TestTheSpireTemplateRitsu). It makes damage-dealing debuff previews account for the target's defensive effects, so the UI shows the HP that will actually be lost instead of only the debuff's base damage.

### Features

- Fixes the vanilla Poison health-bar preview by running the target's standard damage and HP-loss modification hooks.
- Accounts for defensive effects such as damage caps, Intangible, Buffer, Slippery, Hardened Shell, and Beating Remnant.
- Simulates consumable defenses in the correct order for multi-hit Poison without changing powers, block, or HP.
- Exposes the stable `DefenseAwareDebuffPreview.Api.DamageDebuffPreviewApi`, allowing other mods to simulate multiple ordered damage-dealing debuffs against one shared defensive state.

This mod only corrects damage previews. It does not change the actual damage resolution performed by `CreatureCmd.Damage`.

Supported game versions: 0.107.1 through 0.111.x. The mod adapts at runtime to the damage-hook signature used by the installed game.

## 公开 API / Public API

`DamageDebuffPreviewApi.CalculateBySource<TSource>` 会按照来源及其内部命中的枚举顺序，使用同一个防御状态快照连续计算，并分别返回每个来源造成的生命损失。不同来源可以使用不同的 `ValueProp`、伤害者和卡牌上下文；计算不会消耗真实格挡、能力、遗物计数或生命。

`DamageDebuffPreviewApi.CalculateBySource<TSource>` simulates sources and their hits in enumeration order against one shared defensive-state snapshot. It returns HP loss attributed to each source, supports distinct `ValueProp` and damage contexts per source, and never mutates real combat state.

任意数量的 debuff 都可以放进同一次计算。需要分别显示的 debuff 应使用不同的 `TSource` 键，并分别调用 `GetHpLoss`；相同键会被有意合并。API 只负责计算与伤害归属，每个 debuff 仍需通过自己的血条预测接口创建显示分段。所有分段都应调用同一个“收集全部来源”的预测函数，不能各自只计算自身。

Any number of debuffs can participate in one calculation. Debuffs that need separate UI segments must use distinct `TSource` keys and read their own `GetHpLoss` value; repeated keys are intentionally aggregated. The API calculates and attributes damage, while each debuff remains responsible for creating its own health-bar forecast segment. Every segment should use the same coordinator that collects all sources instead of calculating only itself.

例如，需要创建一个新的负面 Power `BurningPower`（“灼烧”）：持有者会在回合开始时受到等同于灼烧层数的可格挡伤害。如果灼烧可能与原版中毒同时触发，应建立一个共享的回合开始伤害预测函数，按照 `target.Powers` 的实际触发顺序收集两种伤害：

For example, suppose a new debuff Power named `BurningPower` deals blockable damage equal to its stacks at the start of its owner's turn. If Burning and vanilla Poison can trigger together, create one shared turn-start forecast that collects both sources in their actual `target.Powers` order:

```csharp
using DefenseAwareDebuffPreview.Api;

private static DamagePreviewResult<PowerModel> CalculateTurnStartDamageForecast(
    Creature target)
{
    var sources = new List<DamagePreviewSource<PowerModel>>();

    foreach (PowerModel power in target.Powers)
    {
        switch (power)
        {
            case PoisonPower poison:
                int triggerCount = GetPoisonTriggerCount(target, poison);
                sources.Add(new(
                    poison,
                    Enumerable.Range(0, triggerCount)
                        .Select(index => (decimal)(poison.Amount - index)),
                    ValueProp.Unblockable | ValueProp.Unpowered));
                break;

            case BurningPower burning when burning.Amount > 0:
                sources.Add(new(
                    burning,
                    [burning.Amount],
                    ValueProp.Unpowered));
                break;
        }
    }

    return DamageDebuffPreviewApi.CalculateBySource<PowerModel>(
        target,
        sources,
        ReferenceEqualityComparer.Instance);
}

private static int GetPoisonTriggerCount(Creature target, PoisonPower poison)
{
    int extraTriggers = target.CombatState?.GetOpponentsOf(target)
        .Where(creature => creature.IsAlive)
        .Sum(creature => creature.GetPowerAmount<AccelerantPower>()) ?? 0;

    return Math.Min(poison.Amount, 1 + extraTriggers);
}

// BurningPower 创建自己的血条预览分段时：
DamagePreviewResult<PowerModel> preview =
    CalculateTurnStartDamageForecast(Owner);
int burningDamage = preview.GetHpLoss(this);

// 补丁原版中毒预览时，也调用同一个函数：
int poisonDamage = CalculateTurnStartDamageForecast(poison.Owner)
    .GetHpLoss(poison);
```

灼烧实际结算时应使用与预览一致的基础伤害和 `ValueProp.Unpowered`。`BurningPower` 用 `burningDamage` 创建自己的血条预测分段；中毒预览补丁则使用同一协调函数取得 `poisonDamage`。两者不能各自只计算自身，否则格挡、Buffer 等共享防御会被重复使用。增加其他同一时点结算的伤害类 debuff 时，只需用唯一的 Power 实例作为来源键，按真实触发位置继续添加 `DamagePreviewSource<PowerModel>`。

The real Burning trigger should use the same base damage and `ValueProp.Unpowered`. `BurningPower` uses `burningDamage` for its own health-bar segment, while the Poison preview patch obtains `poisonDamage` from the same coordinator. Neither source should calculate itself in isolation, or shared defenses such as block and Buffer would be reused. Additional damage-dealing debuffs can be inserted at their real trigger positions, using each unique Power instance as its source key.

调用方需要在模组清单中声明对 `DefenseAwareDebuffPreview` `v0.1.0` 或更高版本的依赖，并在编译时引用 `DefenseAwareDebuffPreview.dll`，且不要把该 DLL 重复打包进调用方模组。

Consumers must declare `DefenseAwareDebuffPreview` version `v0.1.0` or later as a mod dependency, reference `DefenseAwareDebuffPreview.dll` at compile time, and avoid bundling a duplicate copy of the DLL.

```json
"dependencies": [
  {
    "id": "DefenseAwareDebuffPreview",
    "min_version": "v0.1.0"
  }
]
```

## 构建

模板会从 Steam 标准路径自动寻找游戏。若游戏在其他位置，创建一个不会提交到 Git 的 `LocalSettings.props`：

```xml
<Project>
  <PropertyGroup>
    <Sts2Path>C:/path/to/Slay the Spire 2</Sts2Path>
  </PropertyGroup>
</Project>
```

然后运行：

```powershell
dotnet build DefenseAwareDebuffPreview.csproj -v:minimal
```

Windows 上运行针对性战斗测试（脚本会隔离测试用 `APPDATA`，不修改真实游戏设置）：

```powershell
.\tools\Run-Sts2Tests.ps1
```

非标准安装路径可传 `-Sts2Path "D:/path/to/Slay the Spire 2"`；使用其他过滤器可传 `-Filter "名称"`。

## 兼容性说明

支持的游戏版本为 0.107.1 至 0.111.x。模组会在运行时适配游戏所使用的伤害钩子签名，因此同一个 DLL 可用于上述版本。

通用计算器会自动执行所有标准伤害与生命损失钩子。对原版会在连续命中间消耗或累计状态的防御能力，项目内包含显式的无副作用模拟；第三方模组若实现了带内部状态的自定义防御钩子，可能需要在本项目中增加对应的预览状态适配。

## License

AGPL-3.0。模板及本项目的派生代码遵循仓库内 `LICENSE`。
