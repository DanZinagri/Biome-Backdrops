# Biome Backdrops

RimWorld 1.7 only. Shows a screen-fixed picture in the void beyond the map edge.

## How it works

Vanilla draws four large quads around the map every frame (`MapEdgeClipDrawer.DrawClippers`) with `map.MapEdgeMaterial`, a flat gray `MetaOverlay` material. A Harmony postfix on that getter returns a material using `DZ_BB_ScreenBackdrop` instead. The shader samples the texture by `gl_FragCoord` and the engine's global `_ScreenParams`, so the picture ignores camera pan and zoom.

## Adding backdrops to a biome

Put the images in `Textures/` of any mod and patch the biome:

```xml
<Patch>
	<Operation Class="PatchOperationAddModExtension">
		<xpath>Defs/BiomeDef[defName="TemperateForest"]</xpath>
		<value>
			<li Class="BiomeBackdrops.BiomeBackdropExtension">
				<texPaths>
					<li>BiomeBackdrops/TemperateForest_A</li>
					<li>BiomeBackdrops/TemperateForest_B</li>
				</texPaths>
			</li>
		</value>
	</Operation>
</Patch>
```

With several paths, each map picks one at random every time a save is loaded and keeps it until the next load. With **New picture on every load** off, the pick comes from the world tile instead, so the same map always shows the same picture.

### Variants by tile

`variants` gives tiles of the biome their own pictures. The first variant that matches the map's tile is used. If none matches, the biome's `texPaths` is used, and if that is empty too, the map falls back to the loading backgrounds.

Inside a variant, every condition you set has to match, and a condition you leave out matches anything:

- `hilliness`: any of `Flat`, `SmallHills`, `LargeHills`, `Mountainous`. This is the value the world tile inspector shows (`Tile.HillinessLabel`), so tiles with the Plateau or Cliffs mutators count as Mountainous. You can't settle `Impassable` tiles to my knowledge so while a hilliness setting probably won't come up.
- `mutators`: matches when the tile has any of the listed `TileMutatorDef`s, such as `Coast`, `River`, `Caves` or `Mountain`, which come with the base game. Odyssey adds many more, including `Lake`, `Valley`, `HotSprings` and `Fjord`. Put `MayRequire="Ludeon.RimWorld.Odyssey"` on Odyssey-only entries.

```xml
<li Class="BiomeBackdrops.BiomeBackdropExtension">
	<texPaths><li>BiomeBackdrops/Forest</li></texPaths>
	<variants>
		<li>
			<hilliness><li>Mountainous</li></hilliness>
			<texPaths><li>BiomeBackdrops/MountainForest</li></texPaths>
		</li>
		<li>
			<mutators><li>Coast</li></mutators>
			<texPaths><li>BiomeBackdrops/ForestCoast</li></texPaths>
		</li>
	</variants>
</li>
```

### Tint

You can put `tint` and `tintStrength` on the extension, on a variant, or on both. A tint lets you reuse one picture across several biomes and shift its colours to suit each one.

- `tint` is the colour to shift toward, written as `(r, g, b)` with values from 0 to 1. The shader keeps each pixel's brightness and only moves its hue, so the picture doesn't get darker.
- `tintStrength` sets how far to shift, from 0 (no change) to 1 (fully recoloured, like a duotone). The default is 0.3, and a gentle shift is around 0.15 to 0.3.
- A variant without its own `tint` uses the extension's tint.
- An extension with only a tint and no `texPaths` tints the loading background that biome falls back to.
- These can be applied to varients as well just put them inside the varient section as well

```xml
<li Class="BiomeBackdrops.BiomeBackdropExtension">
	<tint>(0.85, 0.75, 0.5)</tint>
	<tintStrength>0.25</tintStrength>
</li>
```

This mod is a framework and ships no pictures. `1.7/Patches/Examples_BiomeBackdrops.xml` is fully commented out and shows seven patterns, each on a different biome: a single picture, several pictures, mountains only, a default with variants, picture reuse with a tint, a tint-only biome, and a DLC biome with an Odyssey mutator.

## Settings

Settings have two tabs. Changes to the biome and background lists apply the next time a save is loaded. Maps that are already loaded keep their current picture.

**Biomes tab**

- **Use loading backgrounds**: on by default. Biomes without a backdrop of their own use a loading background, chosen the same way as biome pictures.
- **New picture on every load**: on by default. Picks a random picture from the map's pool on each game load. Off: the tile picks a fixed one.
  - Changing brightness rebuilds materials but keeps the current picks.
  - Changing either of these two options picks again right away.
- **Brightness**: 10 to 100%, 60% by default. Multiplies the picture so it doesn't compete with the map.
- **Biome list**: lists every biome that has a `BiomeBackdropExtension`, each with its own toggle. A biome you switch off is treated as if it had no extension: no pictures, no variants, no tint. Its maps fall back to the loading backgrounds, or the gray void if those are off too. Stored as `disabledBiomes`, a list of defNames.

**Loading backgrounds tab**

- Every possible loading background has a thumbnail and its own toggle. Enable all and Disable all buttons are at the top. Stored as `disabledBackgrounds`. Because the settings store what is switched off, new backgrounds from newly added mods start switched on.
- With Vanilla Backgrounds Expanded active, the list is every still background VBE knows (`VBEMod.AllDefsInOrder`): vanilla DLC art, add-on packs, and the player's custom folder.
  - Animated (video) entries and art for inactive DLCs are skipped.
  - VBE's own menu selection is ignored. This tab is the only place that chooses which backgrounds become backdrops.
  - VBE is read through reflection in `VbeBackgrounds.cs`, and the DLL has no reference to it. Even a reference whose code only runs when VBE is active breaks without VBE: the lambda caches the compiler generates get static fields typed with VBE classes, and at startup the game resolves the type of every static field (`ReportProbablyMissingAttributes`).
- Without VBE, the list is the active expansions' `ExpansionDef.BackgroundImage`. VBE names its vanilla entries after the ExpansionDef, so a DLC background you switch off stays off whether or not VBE is installed.
