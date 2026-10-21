using HarmonyLib;
using MegaCrit.Sts2.Core.Modding;

namespace DefenseAwareDebuffPreview;

[ModInitializer(nameof(Initialize))]
public static class MainFile
{
    public const string ModId = "DefenseAwareDebuffPreview";

    public static MegaCrit.Sts2.Core.Logging.Logger Logger { get; } =
        new(ModId, MegaCrit.Sts2.Core.Logging.LogType.Generic);

    public static void Initialize()
    {
        new Harmony(ModId).PatchAll();
        Logger.Info("Defense-aware damage debuff previews enabled.");
    }
}
