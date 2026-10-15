using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace BiomeBackdrops
{
    // Put on a BiomeDef to give its maps a backdrop. The first variant whose conditions match the
    // map's world tile supplies the pictures; otherwise texPaths is used, and with no texPaths the
    // loading backgrounds. With several paths, one is picked per map (see "New picture on every load").
    public class BiomeBackdropExtension : DefModExtension
    {
        public List<string> texPaths = new List<string>();

        // Optional tint, applied to whatever this biome shows, loading backgrounds included.
        // Variants without a tint of their own use this one.
        public Color? tint;

        // How far the picture is pushed toward the tint: 0 = untouched, 1 = fully recolored.
        public float tintStrength = 0.3f;

        public List<BackdropVariant> variants = new List<BackdropVariant>();

        public BackdropVariant VariantFor(Tile tile)
        {
            return tile == null ? null : variants.FirstOrDefault(v => v.Matches(tile));
        }

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors())
            {
                yield return error;
            }
            if (texPaths.NullOrEmpty() && variants.NullOrEmpty() && tint == null)
            {
                yield return "BiomeBackdropExtension has no texPaths, variants or tint.";
            }
            for (int i = 0; i < variants.Count; i++)
            {
                if (variants[i].texPaths.NullOrEmpty() && variants[i].tint == null)
                {
                    yield return $"BiomeBackdropExtension variant {i} has no texPaths or tint.";
                }
            }
        }
    }

    // All set conditions must match; a condition left empty matches anything.
    public class BackdropVariant
    {
        // The hilliness shown on the world tile inspector (Tile.HillinessLabel), so the Plateau and
        // Cliffs mutators count as Mountainous. Any listed value matches.
        public List<Hilliness> hilliness = new List<Hilliness>();

        // Tile mutators (Odyssey): matches when the tile has any of these.
        public List<TileMutatorDef> mutators = new List<TileMutatorDef>();

        // Empty: the biome's own texPaths (or loading backgrounds), e.g. for a tint-only variant.
        public List<string> texPaths = new List<string>();

        public Color? tint;

        public float tintStrength = 0.3f;

        public bool Matches(Tile tile)
        {
            if (!hilliness.NullOrEmpty() && !hilliness.Contains(tile.HillinessLabel))
            {
                return false;
            }
            if (!mutators.NullOrEmpty() && !tile.Mutators.Any(mutators.Contains))
            {
                return false;
            }
            return true;
        }
    }
}
