using System.Reflection;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;
using TestTheSpire;

namespace DefenseAwareDebuffPreview.Tests;

[ModInitializer("Init")]
public static class Entry
{
    public static void Init()
    {
        CombatTestBootstrap.Initialize(Assembly.GetExecutingAssembly(), new CombatTestOptions
        {
            LogPrefix = "DefenseAwareDebuffPreview.Tests"
        });

        Log.Info("[DefenseAwareDebuffPreview.Tests] Mod initialized");
    }
}
