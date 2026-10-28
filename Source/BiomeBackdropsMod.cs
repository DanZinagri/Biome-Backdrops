using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace BiomeBackdrops
{
    public class BiomeBackdropsSettings : ModSettings
    {
        public bool useLoadingBackgrounds = true;
        public bool newPictureEachLoad = true;
        public float brightness = 0.6f;

        // Stored as "off" lists so new biomes and backgrounds from other mods start switched on.
        public HashSet<string> disabledBiomes = new HashSet<string>();
        public HashSet<string> disabledBackgrounds = new HashSet<string>();

        public override void ExposeData()
        {
            Scribe_Values.Look(ref useLoadingBackgrounds, "useLoadingBackgrounds", true);
            Scribe_Values.Look(ref newPictureEachLoad, "newPictureEachLoad", true);
            Scribe_Values.Look(ref brightness, "brightness", 0.6f);
            Scribe_Collections.Look(ref disabledBiomes, "disabledBiomes", LookMode.Value);
            Scribe_Collections.Look(ref disabledBackgrounds, "disabledBackgrounds", LookMode.Value);
            disabledBiomes ??= new HashSet<string>();
            disabledBackgrounds ??= new HashSet<string>();
        }
    }

    public class BiomeBackdropsMod : Mod
    {
        public static BiomeBackdropsSettings Settings;

        private const float RowHeight = 40f;
        private const float ThumbWidth = 64f;

        private enum Tab { Biomes, LoadingBackgrounds }

        private Tab tab = Tab.Biomes;
        private Vector2 biomeScroll;
        private Vector2 backgroundScroll;

        // What the picks were made with when the settings window opened.
        private bool openedUseLoading;
        private bool openedNewEachLoad;
        private bool windowOpen;

        public BiomeBackdropsMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<BiomeBackdropsSettings>();
            // The assembly is passed explicitly: on .NET 10 (1.7) the parameterless PatchAll() reads the calling
            // assembly off the stack, which breaks when the JIT inlines the call (Harmony issue #395).
            new Harmony("DanZinagri.BiomeBackdrops").PatchAll(Assembly.GetExecutingAssembly());
        }

        public override string SettingsCategory() => "Biome Backdrops";

        public override void DoSettingsWindowContents(Rect inRect)
        {
            if (!windowOpen)
            {
                windowOpen = true;
                openedUseLoading = Settings.useLoadingBackgrounds;
                openedNewEachLoad = Settings.newPictureEachLoad;
            }

            // DrawTabs hangs the tabs above the rect it is given.
            Rect content = inRect;
            content.yMin += TabDrawer.TabHeight;
            List<TabRecord> tabs = new List<TabRecord>
            {
                new TabRecord("Biomes", () => tab = Tab.Biomes, tab == Tab.Biomes),
                new TabRecord("Loading backgrounds", () => tab = Tab.LoadingBackgrounds, tab == Tab.LoadingBackgrounds),
            };
            TabDrawer.DrawTabs(content, tabs);
            Widgets.DrawMenuSection(content);
            content = content.ContractedBy(10f);

            if (tab == Tab.Biomes)
            {
                DoBiomesTab(content);
            }
            else
            {
                DoBackgroundsTab(content);
            }
        }

        private void DoBiomesTab(Rect rect)
        {
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(rect);
            listing.CheckboxLabeled("Use loading backgrounds", ref Settings.useLoadingBackgrounds,
                "When a biome has no backdrop of its own (or is switched off below), show one of the loading backgrounds instead. Which ones is set on the Loading backgrounds tab. Off: those maps keep the plain gray void.");
            listing.CheckboxLabeled("New picture on every load", ref Settings.newPictureEachLoad,
                "When a biome has several backdrops (or loading backgrounds are used), pick a random one each time a save is loaded. It stays the same until the next load. Off: each map always shows the same picture, chosen from its world tile.");
            Settings.brightness = listing.SliderLabeled($"Brightness: {Settings.brightness.ToStringPercent()}", Settings.brightness, 0.1f, 1f,
                tooltip: "Darkens the backdrop so it does not compete with the map itself.");
            listing.GapLine();
            listing.Label("Biomes with backdrops. A switched-off biome is treated as having none (no pictures, variants or tint). Applies the next time a save is loaded.");
            float used = listing.CurHeight;
            listing.End();

            List<BiomeDef> biomes = DefDatabase<BiomeDef>.AllDefs
                .Where(b => b.HasModExtension<BiomeBackdropExtension>())
                .OrderBy(b => b.label)
                .ToList();
            Rect outRect = new Rect(rect.x, rect.y + used, rect.width, rect.height - used);
            if (biomes.Count == 0)
            {
                Widgets.Label(outRect, "No biome has a backdrop defined.");
                return;
            }

            Rect viewRect = new Rect(0f, 0f, outRect.width - 16f, biomes.Count * RowHeight);
            Widgets.BeginScrollView(outRect, ref biomeScroll, viewRect);
            for (int i = 0; i < biomes.Count; i++)
            {
                BiomeDef biome = biomes[i];
                Rect row = new Rect(0f, i * RowHeight, viewRect.width, RowHeight);
                if (i % 2 == 1)
                {
                    Widgets.DrawLightHighlight(row);
                }
                bool on = !Settings.disabledBiomes.Contains(biome.defName);
                string label = $"{biome.LabelCap}  <color=#999999>{biome.defName} - {Summary(biome.GetModExtension<BiomeBackdropExtension>())}</color>";
                Widgets.CheckboxLabeled(row.ContractedBy(4f, 8f), label, ref on);
                SetOff(Settings.disabledBiomes, biome.defName, !on);
            }
            Widgets.EndScrollView();
        }

        private static string Summary(BiomeBackdropExtension ext)
        {
            List<string> parts = new List<string>();
            if (!ext.texPaths.NullOrEmpty())
            {
                parts.Add($"{ext.texPaths.Count} picture(s)");
            }
            if (!ext.variants.NullOrEmpty())
            {
                parts.Add($"{ext.variants.Count} variant(s)");
            }
            if (ext.tint != null)
            {
                parts.Add("tinted");
            }
            return parts.ToCommaList();
        }

        private void DoBackgroundsTab(Rect rect)
        {
            List<LoadingBackground> all = LoadingBackgrounds.All;

            Listing_Standard listing = new Listing_Standard();
            listing.Begin(rect);
            listing.Label(ModsConfig.IsActive(VbeBackgrounds.PackageId)
                ? "Backgrounds from Vanilla Backgrounds Expanded, including ones other mods add and your custom folder. Used for biomes without a backdrop of their own. Applies the next time a save is loaded."
                : "Your active DLCs' main menu backgrounds. Install Vanilla Backgrounds Expanded for more. Used for biomes without a backdrop of their own. Applies the next time a save is loaded.");
            Rect buttons = listing.GetRect(30f);
            if (Widgets.ButtonText(buttons.LeftPart(0.3f), "Enable all"))
            {
                Settings.disabledBackgrounds.Clear();
            }
            if (Widgets.ButtonText(new Rect(buttons.x + buttons.width * 0.35f, buttons.y, buttons.width * 0.3f, buttons.height), "Disable all"))
            {
                Settings.disabledBackgrounds.AddRange(all.Select(bg => bg.key));
            }
            listing.Gap(6f);
            float used = listing.CurHeight;
            listing.End();

            Rect outRect = new Rect(rect.x, rect.y + used, rect.width, rect.height - used);
            Rect viewRect = new Rect(0f, 0f, outRect.width - 16f, all.Count * RowHeight);
            Widgets.BeginScrollView(outRect, ref backgroundScroll, viewRect);
            for (int i = 0; i < all.Count; i++)
            {
                LoadingBackground bg = all[i];
                Rect row = new Rect(0f, i * RowHeight, viewRect.width, RowHeight);
                if (i % 2 == 1)
                {
                    Widgets.DrawLightHighlight(row);
                }
                // Only rows in view load their texture.
                if (row.yMax >= backgroundScroll.y && row.y <= backgroundScroll.y + outRect.height)
                {
                    Texture2D tex = bg.Texture;
                    if (tex != null)
                    {
                        GUI.DrawTexture(new Rect(row.x + 2f, row.y + 2f, ThumbWidth, RowHeight - 4f), tex, ScaleMode.ScaleAndCrop);
                    }
                }
                bool on = !Settings.disabledBackgrounds.Contains(bg.key);
                Rect checkRow = new Rect(row.x + ThumbWidth + 10f, row.y, row.width - ThumbWidth - 10f, row.height).ContractedBy(4f, 8f);
                Widgets.CheckboxLabeled(checkRow, bg.label, ref on);
                SetOff(Settings.disabledBackgrounds, bg.key, !on);
            }
            Widgets.EndScrollView();
        }

        private static void SetOff(HashSet<string> offSet, string key, bool off)
        {
            if (off)
            {
                offSet.Add(key);
            }
            else
            {
                offSet.Remove(key);
            }
        }

        public override void WriteSettings()
        {
            base.WriteSettings();
            // Only a change to where pictures come from re-picks them; brightness alone just rebuilds materials.
            // The biome and background lists are left for the next load.
            bool sourcesChanged = windowOpen
                && (openedUseLoading != Settings.useLoadingBackgrounds || openedNewEachLoad != Settings.newPictureEachLoad);
            windowOpen = false;
            BackdropMaterials.ClearCache(keepPicks: !sourcesChanged);
        }
    }
}
