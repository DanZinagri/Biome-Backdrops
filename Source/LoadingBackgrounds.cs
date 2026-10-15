using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace BiomeBackdrops
{
    public class LoadingBackground
    {
        // VBE def name, or ExpansionDef name without VBE. VBE names its vanilla entries after the
        // ExpansionDef, so a switched-off DLC background stays off whether VBE is active or not.
        public readonly string key;
        public readonly string label;
        private readonly Func<Texture2D> getTexture;

        public LoadingBackground(string key, string label, Func<Texture2D> getTexture)
        {
            this.key = key;
            this.label = label;
            this.getTexture = getTexture;
        }

        // Loaded on first use (a backdrop pick or the settings list), not at startup.
        public Texture2D Texture => getTexture();
    }

    public static class LoadingBackgrounds
    {
        private static List<LoadingBackground> all;

        // Built on first use, so VBE's defs are complete by then.
        public static List<LoadingBackground> All
        {
            get
            {
                if (all == null)
                {
                    if (ModsConfig.IsActive(VbeBackgrounds.PackageId))
                    {
                        all = VbeBackgrounds.All();
                    }
                    if (all.NullOrEmpty())
                    {
                        all = ModLister.AllExpansions
                            .Where(exp => exp.Status == ExpansionStatus.Active && !exp.backgroundPath.NullOrEmpty())
                            .Select(exp => new LoadingBackground(exp.defName, exp.LabelCap, () => exp.BackgroundImage))
                            .ToList();
                    }
                }
                return all;
            }
        }

        // The ones switched on in this mod's settings.
        public static List<Texture2D> EnabledTextures()
        {
            return All
                .Where(bg => !BiomeBackdropsMod.Settings.disabledBackgrounds.Contains(bg.key))
                .Select(bg => bg.Texture)
                .Where(tex => tex != null)
                .ToList();
        }
    }
}
