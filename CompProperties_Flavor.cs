using Verse;

namespace FlavorText;

public class CompProperties_Flavor : CompProperties
{
    public RulePackDef sideDishConjunctions;

    public CompProperties_Flavor()
    {
        compClass = typeof(CompFlavor);
    }
}
