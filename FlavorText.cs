using System.Collections.Generic;
using System.Linq;
using Verse;
using System.Reflection;

namespace FlavorText;

// startup message
[StaticConstructorOnStartup]
public class FlavorText
{
    static FlavorText()
    {
        Log.Message($"[Flavor Text] mod is now active.");
        Log.Warning($"{FlavorDef.ActiveFlavorDefs.Count()} active FlavorDefs for the current modlist found out of {DefDatabase<FlavorDef>.AllDefs.Count()} total FlavorDefs");
    }
}
