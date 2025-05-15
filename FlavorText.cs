using System.Linq;
using Verse;

namespace FlavorText;

// startup message
[StaticConstructorOnStartup]
public class FlavorText
{
    static FlavorText()
    {
        Log.Message($"[Flavor Text] mod is now active.");
        Log.Warning($"{FlavorDef.ActiveFlavorDefs.Count()} active FlavorDefs for the current modlist found out of {DefDatabase<FlavorDef>.AllDefs.Count()} total FlavorDefs");
        /*DefDatabase<ThingCategoryDef>.ResolveAllReferences();  // TODO: this attempts to avoid having to ResolveReferences in CompFlavor, but rn I don't think this does anything*/
    }
}
