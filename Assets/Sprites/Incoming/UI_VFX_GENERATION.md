# UI and action VFX generation

All raster artwork below used the built-in image_gen tool. Transparent outputs preserve real alpha; sprites were integrated through Unity, with no prefab regeneration. Native UI geometry and gradient surfaces were made in Unity, with nine-slice borders.

## Draw deck — UI_DrawDeck_v3.png

Use case: stylized-concept. Single production sprite for a mobile imperial strategy card game: a substantial draw pile of 12 playing cards, isolated on a genuinely transparent background. Portrait card silhouette, 2:3 proportions, deck shown almost directly top-down with only a small amount of perspective so the fine layered ivory and gold card edges are visible along the lower and right sides. The top card is midnight navy black enamel with an extremely thin antique gold double hairline inset border and one small elegant gold four-point compass diamond at the exact center. Clipped card corners modest and precise. Realistic premium digitally painted materials, restrained warm highlights, coherent gold light from upper left. Stack must look like cards laid flat on a table, never upright. Object occupies central 80 percent of a 1024x1536 canvas; leave fully transparent margins. No pedestal, no plinth, no stand, no podium, no labels or label plaques, no tabletop, no floor, no cast shadow beyond a very subtle contact shadow, no glows. Absolutely no text, letters, numbers, names, captions or watermark. This is one isolated deck sprite rather than an image of a UI screen.

## Assassin dagger — VFX_AssassinDagger_v3.png

Single isolated production sprite of an elegant assassin throwing dagger for a premium imperial science fiction card game. The dagger lies perfectly horizontally, sharp point directed to the RIGHT. Slender straight double-edged dark steel blade, a crisp silver cutting edge, small antique gold crossguard, short matte black wrapped grip at LEFT. Refined lethal silhouette, understated realistic digitally painted metal, white specular glint near blade tip. Entire dagger fully visible, one weapon only, no hand, no character, no badge, no icon frame, no particles, no glow, no energy arc, no smoke, no shadow, no environment. Genuine transparent background. Tight wide horizontal composition, weapon occupies central 85 percent of width, centered vertically, generous clean transparent margin. No text, numbers, letters or watermark. This will be animated as a precise fast thrown knife rather than a magic spell.

## Shockwave — VFX_CoupShockwave_v2.png

Single isolated top-down circular radial impact shockwave particle texture for an imperial sci-fi game Coup ultimate attack. Broken thin ring of white-hot light and fine irregular dust filaments expanding radially, compact inner ring with empty transparent center, subtle outward sparks around the ring, cinematic but restrained. Grayscale white and silver only so Unity can tint gold and crimson. No text, no numerals, no emblem, no objects, no scene or background, no black rectangle. Real alpha transparency outside filaments and in center. Square 1024x1024 canvas, complete circle fits inside 80 percent width.

## Smoke — VFX_ShadowSmoke_v2.png

Single isolated soft wisp of drifting violet-black smoke, almost grayscale silver-gray spectral ink plume for a refined non-gory assassin particle in a royal science fiction card game. Diffuse feathered edges, curled wispy silhouette, brightest wisps pale gray so runtime tint can color it. No black or opaque background, genuine alpha fades smoothly to zero. No figure, no skull, no text, no weapon, no blood, no border. Square 1024x1024, plume fits center 75 percent, isolated VFX sprite only.

## Rejected variant — VFX_AssassinSlash_v2.png

A single isolated diagonal crescent energy slash VFX sprite, white-hot sharp tapered blade arc with very subtle cool violet wisps trailing behind it. Minimal elegant fast assassination cut for an imperial sci-fi card game. Brightest core white so Unity can tint it. One clean curved slash only, diagonal rising from bottom left to top right, no weapon or figure, no gore, no blood, no text, no icon, no backdrop. Soft transparent edges, genuine alpha everywhere outside the narrow slash. Square canvas 1024x1024, object within center 80 percent, no clipping.

The user rejected this broad magical style. It is retained as an unused variant and is not assigned to the final assassination effect.

## Typography

Rajdhani SemiBold and Medium from the Google Fonts repository: https://github.com/google/fonts/tree/main/ofl/rajdhani . License bundled at Fonts/OFL.txt. TMP assets include a persistent dynamic punctuation fallback so ellipsis and smart quotes render without warnings.

