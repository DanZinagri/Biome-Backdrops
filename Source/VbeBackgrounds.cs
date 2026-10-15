using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace BiomeBackdrops
{
    // Reads Vanilla Backgrounds Expanded through reflection so the DLL has no compile-time reference
    // to it. A direct reference is not enough to make VBE optional: compiler-generated lambda caches
    // get static fields typed with VBE's classes, and the game resolves every static field type at
    // startup (StaticConstructorOnStartupUtility.ReportProbablyMissingAttributes), which throws
    // TypeLoadException when VBE is absent.
    public static class VbeBackgrounds
    {
        public const string PackageId = "vanillaexpanded.backgrounds";

        // Every still background VBE knows: vanilla DLC art, add-on packs (Hero Backgrounds and the like)
        // and the player's custom folder. VBE's own menu selection is not applied: which ones are used
        // as backdrops is chosen in this mod's settings instead.
        public static List<LoadingBackground> All()
        {
            Type defType = GenTypes.GetTypeInAnyAssembly("VBE.BackgroundImageDef");
            FieldInfo animatedField = defType == null ? null : AccessTools.Field(defType, "animated");
            FieldInfo isVanillaField = defType == null ? null : AccessTools.Field(defType, "isVanilla");
            PropertyInfo textureProp = defType == null ? null : AccessTools.Property(defType, "Texture");
            if (animatedField == null || isVanillaField == null || textureProp == null)
            {
                Log.Warning("[Biome Backdrops] Vanilla Backgrounds Expanded is active but its background defs could not be read; using the DLC backgrounds instead.");
                return new List<LoadingBackground>();
            }

            List<LoadingBackground> result = new List<LoadingBackground>();
            foreach (Def def in AllDefsInOrder(defType))
            {
                if ((bool)animatedField.GetValue(def))
                {
                    continue;
                }
                if ((bool)isVanillaField.GetValue(def) && !ExpansionActive(def.defName))
                {
                    continue;
                }
                result.Add(new LoadingBackground(def.defName, def.LabelCap, () => textureProp.GetValue(def) as Texture2D));
            }
            return result;
        }

        // VBE's own order (core, vanilla, user, then by mod and label), or the database order if that
        // property is ever renamed.
        private static IEnumerable<Def> AllDefsInOrder(Type defType)
        {
            PropertyInfo ordered = AccessTools.Property(GenTypes.GetTypeInAnyAssembly("VBE.VBEMod"), "AllDefsInOrder");
            if (ordered?.GetValue(null) is IEnumerable defs)
            {
                return defs.OfType<Def>();
            }
            return GenDefDatabase.GetAllDefsInDatabaseForDef(defType);
        }

        // VBE makes a def for every expansion, owned or not, named after its ExpansionDef.
        private static bool ExpansionActive(string defName)
        {
            ExpansionDef exp = DefDatabase<ExpansionDef>.GetNamedSilentFail(defName);
            return exp == null || exp.Status == ExpansionStatus.Active;
        }
    }
}
