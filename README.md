# Defense-Aware Debuff Preview

作者 / Author: **Sadaki**

## 中文简介

这是一个基于 [TestTheSpireTemplateRitsu](https://github.com/shadowversebydmod/TestTheSpireTemplateRitsu) 创建的《杀戮尖塔 2》机制模组。它让伤害类减益的伤害预览正确计算目标的防御能力，从而显示真正会失去的生命值，而不只是减益的基础伤害。

## 功能

- 修复原版中毒生命条预览：预览会经过目标的伤害修改与生命损失修改钩子，而不是只显示中毒基础数值。
- 正确考虑伤害上限、无形、缓冲、滑溜、硬化外壳、Beating Remnant 等防御能力。
- 多段中毒会按实际触发顺序模拟可消耗防御；预览计算不会消耗能力、格挡或生命。
- 可显式预测“目标下回合自动清除格挡之后”的伤害，避免把即将清除的旧格挡计入 DOT 预览。
- 提供稳定的 `DefenseAwareDebuffPreview.Api.DamageDebuffPreviewApi`，其他模组可按来源、按结算顺序共同模拟多种伤害类减益。
- 纯显示模组，不注册游戏内容模型；多人模式下只有希望看到修正预览的玩家需要安装。

该模组只在 `NHealthBar` 的生命条刷新阶段修正预览，不修改 `CreatureCmd.Damage` 的实际伤害结算，也不会改变玩法代码直接调用 `PoisonPower.CalculateTotalDamageNextTurn()` 时的原版返回值。

## English Overview

This is a *Slay the Spire 2* mechanics mod based on [TestTheSpireTemplateRitsu](https://github.com/shadowversebydmod/TestTheSpireTemplateRitsu). It makes damage-dealing debuff previews account for the target's defensive effects, so the UI shows the HP that will actually be lost instead of only the debuff's base damage.

### Features

- Fixes the vanilla Poison health-bar preview by running the target's standard damage and HP-loss modification hooks.
- Accounts for defensive effects such as damage caps, Intangible, Buffer, Slippery, Hardened Shell, and Beating Remnant.
- Simulates consumable defenses in the correct order for multi-hit Poison without changing powers, block, or HP.
- Can explicitly forecast damage after the target's next automatic block-clear step, so expiring block is not counted against a later DOT.
- Exposes the stable `DefenseAwareDebuffPreview.Api.DamageDebuffPreviewApi`, allowing other mods to simulate multiple ordered damage-dealing debuffs against one shared defensive state.
- Registers no gameplay content models and is client-side display only; other players in a multiplayer lobby do not need it installed.

This mod only corrects previews while `NHealthBar` is refreshing. It does not change the actual damage resolution performed by `CreatureCmd.Damage`, nor the vanilla result returned when gameplay code calls `PoisonPower.CalculateTotalDamageNextTurn()` directly.

Supported game versions: 0.107.1 through 0.111.x. The mod adapts at runtime to the damage-hook signature used by the installed game.

## 公开 API / Public API

`DamageDebuffPreviewApi.CalculateBySource<TSource>` 会按照来源及其内部命中的枚举顺序，使用同一个防御状态快照连续计算，并分别返回每个来源造成的生命损失。不同来源可以使用不同的 `ValueProp`、伤害者和卡牌上下文；计算不会消耗真实格挡、能力、遗物计数或生命。

`DamageDebuffPreviewApi.CalculateBySource<TSource>` simulates sources and their hits in enumeration order against one shared defensive-state snapshot. It returns HP loss attributed to each source, supports distinct `ValueProp` and damage contexts per source, and never mutates real combat state.

UI 集成应优先调用 `TryCalculateBySource` 或 `TryCalculateTotalHpLoss`。它们会捕获预览异常并返回 `false`，调用方此时应保留原版显示。已有的 `CalculateBySource` 和 `CalculateTotalHpLoss` 签名继续保留，以兼容已经编译的调用方。

UI integrations should prefer `TryCalculateBySource` or `TryCalculateTotalHpLoss`. They catch preview exceptions and return `false`, allowing the caller to retain its original display. The existing `CalculateBySource` and `CalculateTotalHpLoss` signatures remain available for binary compatibility.

现有重载默认使用 `DamagePreviewBlockTiming.Current`，即从调用时的真实格挡开始计算。若一批 DOT 在目标下回合的自动格挡清除之后触发，应调用带 `DamagePreviewBlockTiming` 的新重载并传入 `DamagePreviewBlockTiming.AfterAutomaticClear`。该模式会执行游戏的 `Hook.ShouldClearBlock` 判定：通常从 0 格挡开始；Barricade、Blur 等阻止清除的效果存在时则保留格挡。不要把清除前和清除后的来源放入同一批计算。

Existing overloads default to `DamagePreviewBlockTiming.Current`, starting from the target's block when the API is called. For a batch of DOTs that triggers after the target's next automatic block-clear step, use the new overload with `DamagePreviewBlockTiming.AfterAutomaticClear`. This mode evaluates the game's `Hook.ShouldClearBlock` predicate: it normally starts from zero block, while effects such as Barricade or Blur retain block. Do not combine sources from opposite sides of the block-clear boundary in one batch.

这些 API 只应用于 UI 绘制或预测阶段。返回值是本地派生显示数据，不得用于写入 `RunState`、卡组、Power、存档或发送网络消息。若调用方根据预览结果改变实际战斗状态，则调用方属于 gameplay 模组，必须将自己的 `affects_gameplay` 设为 `true`。

These APIs are only for UI rendering or forecast evaluation. Their results are local derived display data and must not be used to mutate `RunState`, decks, powers, saves, or send network messages. A consumer that uses preview results to change authoritative combat state is a gameplay mod and must set its own `affects_gameplay` to `true`.

任意数量的 debuff 都可以放进同一次计算。需要分别显示的 debuff 应使用不同的 `TSource` 键，并分别调用 `GetHpLoss`；相同键会被有意合并。API 只负责计算与伤害归属，每个 debuff 仍需通过自己的血条预测接口创建显示分段。所有分段都应调用同一个“收集全部来源”的预测函数，不能各自只计算自身。

Any number of debuffs can participate in one calculation. Debuffs that need separate UI segments must use distinct `TSource` keys and read their own `GetHpLoss` value; repeated keys are intentionally aggregated. The API calculates and attributes damage, while each debuff remains responsible for creating its own health-bar forecast segment. Every segment should use the same coordinator that collects all sources instead of calculating only itself.

本模组无法自动发现任意外部模组的 DOT，也不能把已经分别返回的独立 API 调用事后合并。若多个模组的 DOT 共享滑溜、Buffer、格挡或伤害上限，集成方必须建立一个共同的预测协调函数，把同一触发阶段的全部来源按真实顺序放进一次 `CalculateBySource` 调用。否则每次独立调用都会重新取得完整防御快照，例如同一层滑溜可能被多个 DOT 重复使用。

The library cannot discover arbitrary third-party DOTs or retroactively merge independent API calls whose results have already been returned. When DOTs from multiple mods share Slippery, Buffer, block, or a damage cap, the integration must provide one forecast coordinator that submits every source in the same trigger phase, in real resolution order, to one `CalculateBySource` call. Separate calls each receive a fresh defensive snapshot and can therefore reuse the same Slippery charge incorrectly.

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
        DamagePreviewBlockTiming.AfterAutomaticClear,
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

// 在明确的生命条 UI/预测路径中修正原版中毒显示时，也调用同一个函数：
int poisonDamage = CalculateTurnStartDamageForecast(poison.Owner)
    .GetHpLoss(poison);
```

灼烧实际结算时应使用与预览一致的基础伤害和 `ValueProp.Unpowered`。`BurningPower` 用 `burningDamage` 创建自己的血条预测分段；明确限定在显示阶段的中毒集成则使用同一协调函数取得 `poisonDamage`。不要全局替换 `PoisonPower.CalculateTotalDamageNextTurn` 等模型方法，集成应限定在生命条绘制或其他明确的预测路径。两者不能各自只计算自身，否则格挡、Buffer 等共享防御会被重复使用。增加其他同一时点结算的伤害类 debuff 时，只需用唯一的 Power 实例作为来源键，按真实触发位置继续添加 `DamagePreviewSource<PowerModel>`。

The real Burning trigger should use the same base damage and `ValueProp.Unpowered`. `BurningPower` uses `burningDamage` for its own health-bar segment, while a display-specific Poison integration obtains `poisonDamage` from the same coordinator. Do not globally replace model methods such as `PoisonPower.CalculateTotalDamageNextTurn`; limit integration to health-bar rendering or another explicit forecast path. Neither source should calculate itself in isolation, or shared defenses such as block and Buffer would be reused. Additional damage-dealing debuffs can be inserted at their real trigger positions, using each unique Power instance as its source key.

调用方需要在模组清单中声明对 `DefenseAwareDebuffPreview` `v0.1.1` 或更高版本的依赖，并在编译时引用 `DefenseAwareDebuffPreview.dll`，且不要把该 DLL 重复打包进调用方模组。已有公开签名继续保持二进制兼容。

Consumers must declare `DefenseAwareDebuffPreview` version `v0.1.1` or later as a mod dependency, reference `DefenseAwareDebuffPreview.dll` at compile time, and avoid bundling a duplicate copy of the DLL. Existing public signatures remain binary compatible.

```json
"dependencies": [
  {
    "id": "DefenseAwareDebuffPreview",
    "min_version": "v0.1.1"
  }
]
```

## 构建

构建和测试需要 .NET 9 SDK，以及版本兼容的《杀戮尖塔 2》。首次构建还需要能够访问 NuGet.org，或已在本机缓存所需的 NuGet 包。`Godot.NET.Sdk`、`TestTheSpire` 和 xUnit 等编译及测试依赖不直接包含在本仓库中，会由 `dotnet restore` 自动取得，无需另行克隆 TestTheSpire 仓库。因此，在没有网络且没有相应 NuGet 缓存的全新环境中，仅凭本仓库和游戏本体无法构建并运行测试。

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

Windows 上运行针对性战斗测试（脚本会隔离测试用 `APPDATA`，不修改真实游戏设置或存档）：

```powershell
.\tools\Run-Sts2Tests.ps1
```

非标准安装路径可传 `-Sts2Path "D:/path/to/Slay the Spire 2"`；使用其他过滤器可传 `-Filter "名称"`。

测试构建会把主模组、测试模组及测试依赖复制到指定游戏安装目录的 `mods` 文件夹，因此运行脚本的用户需要对该目录具有写入权限。

## 兼容性说明

支持的游戏版本为 0.107.1 至 0.111.x。模组会在运行时适配游戏所使用的伤害钩子签名，因此同一个 DLL 可用于上述版本。

通用计算器会自动执行所有标准伤害与生命损失钩子。对原版会在连续命中间消耗或累计状态的防御能力，项目内包含显式的无副作用模拟；第三方模组若实现了带内部状态的自定义防御钩子，可能需要在本项目中增加对应的预览状态适配。`AfterAutomaticClear` 只调用无副作用的 `ShouldClearBlock` 判定；若第三方模组在 `AfterPreventingBlockClear` 中进一步改变保留的格挡量，也需要专门的预览适配。

## License

AGPL-3.0。模板及本项目的派生代码遵循仓库内 `LICENSE`。
