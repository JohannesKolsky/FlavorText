using JetBrains.Annotations;
using Verse;
using System;

namespace FlavorText;


// variations on ingredient names to make labels and descriptions more grammatically correct
public class IngredientInflections(string plur, string sing)
{
    public string plural = plur;
    public string singular = sing;
}
