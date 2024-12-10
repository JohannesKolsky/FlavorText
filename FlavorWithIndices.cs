using System.Collections.Generic;

namespace FlavorText;
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
