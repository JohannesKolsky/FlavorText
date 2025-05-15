using System.Collections.Generic;

namespace FlavorText;

// a FlavorDef packed with a list of indices showing how the FlavorDef ingredient order relates to the actual ingredient order in-game
public class FlavorWithIndices(FlavorDef flavorDefArg, List<int> indicesArg)
{
    internal FlavorDef FlavorDef = flavorDefArg;
    internal List<int> Indices = indicesArg;
}
