# Defense-Aware Debuff Preview

一个基于 [TestTheSpireTemplateRitsu](https://github.com/shadowversebydmod/TestTheSpireTemplateRitsu) 创建的《杀戮尖塔 2》机制模组。

## 功能

- 修复原版毒素生命条预览：预览会经过目标的伤害修改与生命损失修改钩子，而不是只显示毒素基础数值。
- 正确考虑伤害上限、无形、缓冲、滑溜、硬化外壳、Beating Remnant 等防御能力。
- 多段毒素会按实际触发顺序模拟可消耗防御；预览计算不会消耗能力、格挡或生命。
- 提供公开的 `DamageDebuffPreviewCalculator.CalculateTotalHpLoss`，其他模组的伤害类 debuff 可复用同一套无副作用预览逻辑。

该模组只修正预览，不修改 `CreatureCmd.Damage` 的实际伤害结算。

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

通用计算器会自动执行所有标准伤害与生命损失钩子。对原版会在连续命中间消耗或累计状态的防御能力，项目内包含显式的无副作用模拟；第三方模组若实现了带内部状态的自定义防御钩子，可能需要在本项目中增加对应的预览状态适配。

## License

AGPL-3.0。模板及本项目的派生代码遵循仓库内 `LICENSE`。
