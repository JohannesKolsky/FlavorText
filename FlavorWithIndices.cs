using System.Collections.Generic;

namespace FlavorText;

// a FlavorDef packed with a list of indices showing how the FlavorDef ingredient order relates to the actual ingredient order in-game
public class FlavorWithIndices
{
    public FlavorDef def;
    public List<int> indices;

    public FlavorWithIndices(FlavorDef flavorDefArg, List<int> indicesArg)
    {
        def = flavorDefArg;
        indices = indicesArg;
    }
}
