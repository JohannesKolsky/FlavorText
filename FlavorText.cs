using System.Collections.Generic;
using System.Linq;
using Verse;
using System.Reflection;

namespace FlavorText;

// startup message
[StaticConstructorOnStartup]
public static class FlavorText
{
    static FlavorText()
    {
        Log.Warning($"[Flavor Text] mod is now active: {FlavorDef.ActiveFlavorDefs.Count()} active FlavorDefs for the current modlist found out of {DefDatabase<FlavorDef>.AllDefs.Count()} total FlavorDefs");
    }
}
