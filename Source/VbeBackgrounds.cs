using System.Collections.Generic;
using System.Linq;
using RimWorld;
using VBE;
using Verse;

namespace BiomeBackdrops
{
    // Every reference to Vanilla Backgrounds Expanded lives here, so this class is only JIT-compiled
    // (and VBE.dll only needed) when VBE is active.
    public static class VbeBackgrounds
    {
        public const string PackageId = "vanillaexpanded.backgrounds";

        // Every still background VBE knows: vanilla DLC art, add-on packs (Hero Backgrounds and the like)
        // and the player's custom folder. VBE's own menu selection is not applied: which ones are used
        // as backdrops is chosen in this mod's settings instead.
        public static List<LoadingBackground> All()
        {
            return VBEMod.AllDefsInOrder
                .Where(def => !def.animated && (!def.isVanilla || ExpansionActive(def.defName)))
                .Select(def => new LoadingBackground(def.defName, def.LabelCap, () => def.Texture))
                .ToList();
        }

        // VBE makes a def for every expansion, owned or not, named after its ExpansionDef.
        private static bool ExpansionActive(string defName)
        {
            ExpansionDef exp = DefDatabase<ExpansionDef>.GetNamedSilentFail(defName);
            return exp == null || exp.Status == ExpansionStatus.Active;
        }
    }
}
