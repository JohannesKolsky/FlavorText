using Verse;
using System.Collections.Generic;

namespace FlavorText;


// variations on ingredient names to make labels and descriptions more grammatically correct
class IngredientInflections : Dictionary<ThingDef, string>
{
    public ThingDef ingredient;
    public string singular;
    public string plural;
    public string adjective;
}
