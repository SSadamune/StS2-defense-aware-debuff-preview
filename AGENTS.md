# AGENTS.md

## 开发前检查

- 运行 `git status --short --ignored`，区分源码与 `.godot/`、`bin/`、`obj/`、`artifacts/` 等生成物。
- 确认 `DefenseAwareDebuffPreview.csproj` 能定位 STS2；非标准安装路径写入 ignored 的 `LocalSettings.props`。
- 需要查游戏 API 时，运行 `dotnet msbuild DefenseAwareDebuffPreview.csproj -t:DecompileSts2`，并优先查 `local/sts2-decompiled/`。
- 修改伤害预览时，对照 `CreatureCmd.Damage`、`Hook.ModifyDamage`、`Hook.ModifyHpLost` 和 `PoisonPower.CalculateTotalDamageNextTurn` 的当前游戏实现。
- 预览逻辑必须无副作用：不得消耗格挡、能力层数、遗物计数或生命。
- 连续命中测试至少覆盖普通伤害、伤害上限、可消耗防御和预览不改变战斗状态。
- 构建：`dotnet build DefenseAwareDebuffPreview.csproj -v:minimal`。
- 针对性测试：`dotnet msbuild DefenseAwareDebuffPreview.Tests/DefenseAwareDebuffPreview.Tests.csproj -restore -t:RunSts2Tests -p:Sts2TestArgs=--sts2-test-filter=PoisonDamagePreviewTests`。
