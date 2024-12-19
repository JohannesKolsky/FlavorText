using Verse;

namespace FlavorText;

// startup message
[StaticConstructorOnStartup]
public class FlavorText
{
    static FlavorText()
    {
        Log.Message("[Flavor Text] mod is now active.");
        /*DefDatabase<ThingCategoryDef>.ResolveAllReferences();  // TODO: this attempts to avoid having to ResolveReferences in CompFlavor, but rn I don't think this does anything*/
    }
}
