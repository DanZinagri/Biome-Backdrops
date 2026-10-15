using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace BiomeBackdrops
{
    // Marked for startup because it holds a Shader, which the game wants created on the main thread.
    [StaticConstructorOnStartup]
    public static class BackdropMaterials
    {
        private const string ShaderDefName = "DZ_BB_ScreenBackdrop";

        private static readonly Shader BackdropShader = DefDatabase<ShaderDef>.GetNamed(ShaderDefName).Shader;

        // Keyed by Map.uniqueID. A null value means "this map has no backdrop", so it is not looked up again.
        // The picked picture is kept apart from its material so a brightness change does not pick again.
        private static readonly Dictionary<int, Pick> pickByMap = new Dictionary<int, Pick>();
        private static readonly Dictionary<int, Material> materialByMap = new Dictionary<int, Material>();

        // Map IDs restart in every save, so everything is dropped whenever a different game is loaded.
        private static Game cachedForGame;

        public static Material For(Map map)
        {
            if (cachedForGame != Current.Game)
            {
                ClearCache(keepPicks: false);
                cachedForGame = Current.Game;
            }
            if (!materialByMap.TryGetValue(map.uniqueID, out Material mat))
            {
                if (!pickByMap.TryGetValue(map.uniqueID, out Pick pick))
                {
                    pick = Choose(map);
                    pickByMap[map.uniqueID] = pick;
                }
                if (pick.texture != null)
                {
                    float b = BiomeBackdropsMod.Settings.brightness;
                    // colorTwo is part of MaterialPool's key, so each tint gets its own pooled material.
                    mat = MaterialPool.MatFrom(new MaterialRequest(pick.texture, BackdropShader, new Color(b, b, b, 1f))
                    {
                        colorTwo = pick.tint
                    });
                }
                materialByMap[map.uniqueID] = mat;
            }
            return mat;
        }

        // The enabled loading backgrounds, taken once per game load so settings changes apply from the next load.
        private static List<Texture2D> loadingBackgrounds;

        // keepPicks: rebuild materials only, so maps keep the picture they already show.
        // MaterialPool keeps the old materials, which is harmless.
        public static void ClearCache(bool keepPicks)
        {
            materialByMap.Clear();
            if (!keepPicks)
            {
                pickByMap.Clear();
                loadingBackgrounds = null;
            }
        }

        // tint: rgb is the hue, alpha the strength. default(Color) has alpha 0, meaning no tint.
        private struct Pick
        {
            public Texture2D texture;
            public Color tint;
        }

        private static Pick Choose(Map map)
        {
            // Orbital maps show the planet below through the void. Vanilla's space generators also
            // disable the clippers, but a modded one might not, so these maps are never touched.
            if (map.generatorDef.renderWorld || (map.Biome?.inVacuum ?? false))
            {
                return default;
            }

            int seed = map.Tile.GetHashCode();

            // A biome switched off in the settings is treated as having no extension at all
            // (no pictures, variants or tint), so it falls back to the loading backgrounds.
            BiomeDef biome = map.Biome;
            BiomeBackdropExtension ext = biome == null || BiomeBackdropsMod.Settings.disabledBiomes.Contains(biome.defName)
                ? null
                : biome.GetModExtension<BiomeBackdropExtension>();
            BackdropVariant variant = ext?.VariantFor(map.TileInfo);

            Pick pick = default;
            if (variant?.tint != null)
            {
                pick.tint = TintColor(variant.tint.Value, variant.tintStrength);
            }
            else if (ext?.tint != null)
            {
                pick.tint = TintColor(ext.tint.Value, ext.tintStrength);
            }

            List<string> paths = !(variant?.texPaths).NullOrEmpty() ? variant.texPaths : ext?.texPaths;
            if (!paths.NullOrEmpty())
            {
                pick.texture = ContentFinder<Texture2D>.Get(paths[PickIndex(seed, paths.Count)]);
            }
            else if (BiomeBackdropsMod.Settings.useLoadingBackgrounds)
            {
                loadingBackgrounds ??= LoadingBackgrounds.EnabledTextures();
                if (loadingBackgrounds.Count > 0)
                {
                    pick.texture = loadingBackgrounds[PickIndex(seed, loadingBackgrounds.Count)];
                }
            }
            return pick;
        }

        private static Color TintColor(Color tint, float strength)
        {
            return new Color(tint.r, tint.g, tint.b, Mathf.Clamp01(strength));
        }

        // Not Verse.Rand: its state belongs to the game simulation.
        private static readonly System.Random loadRandom = new System.Random();

        // The cache is rebuilt on every game load, so a random pick here means a new picture per load.
        private static int PickIndex(int seed, int count)
        {
            if (BiomeBackdropsMod.Settings.newPictureEachLoad)
            {
                return loadRandom.Next(count);
            }
            return (Gen.HashCombineInt(seed, 0x5EED) & int.MaxValue) % count;
        }
    }

    // The clipper quads around the map are drawn with map.MapEdgeMaterial every frame. Swapping
    // that material is the whole mod: no new draw calls. Only the plain vanilla gray is replaced,
    // so maps whose world object or generator sets its own edge look keep it.
    [HarmonyPatch(typeof(Map), nameof(Map.MapEdgeMaterial), MethodType.Getter)]
    public static class Map_MapEdgeMaterial_Patch
    {
        public static void Postfix(Map __instance, ref Material __result)
        {
            if (__result != MapEdgeClipDrawer.ClipMat)
            {
                return;
            }
            Material backdrop = BackdropMaterials.For(__instance);
            if (backdrop != null)
            {
                __result = backdrop;
            }
        }
    }
}
