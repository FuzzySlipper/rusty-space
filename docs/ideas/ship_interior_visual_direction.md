# Ship Interior Visual Direction

> Exploratory art-direction notes for `rusty-space`.
>
> The target is not a literal retro game and not a standard modern sci-fi cockpit. The desired look is a **modern, atmospheric rendering of a chunky older space-game interior language**, with selective low-resolution / unsmoothed texture character and strong realistic lighting.

This is a companion to:

- [`space_sailing_reactive_ship_ideas.md`](./space_sailing_reactive_ship_ideas.md)
- [`ship_physics_implementation_notes.md`](./ship_physics_implementation_notes.md)
- [`navigation_view_reconstruction_ideas.md`](./navigation_view_reconstruction_ideas.md)

---

# 1. Visual thesis

A useful short version:

> **A cozy lived-in retro-futurist spaceship interior built from chunky instrument architecture, rendered with modern lighting and materials but with selective low-resolution texture language and synthetic sensor displays.**

The important tension is:

```text
1990s / early-2000s visual grammar
+
modern lighting, depth, materials, and atmosphere
```

Do not attempt to reproduce a 1993 frame buffer.

Do not apply a global pixelation filter.

Do not make a generic high-resolution PBR spaceship and then sprinkle scanlines on it.

The goal is a subtler hybrid where the **forms, texture density, display language, and art decisions remember older games**, while the room itself benefits from convincing modern light and physical depth.

---

# 2. Reference vibes

These are conceptual references, not targets to clone.

## Privateer (1993)

Useful for:

- chunky cockpit framing,
- big instrument housings,
- strong silhouette divisions,
- the sense that the cockpit is a place rather than a HUD,
- instruments visually competing for physical space,
- dense machine presence around a relatively small exterior view.

The important memory is not the literal low resolution. It is the **architectural weight of the interface**.

## Rebel Galaxy Outlaw

Useful for:

- cozy cockpit inhabitation,
- saturated ambient lighting,
- tactile instrument framing,
- the feeling that sitting in the ship is pleasurable even before gameplay begins,
- retro-futurist screens and bezels without becoming a parody of old hardware.

A major goal for `rusty-space` is to preserve that kind of experiential appeal while giving the actual flight/navigation system more tactile depth.

## System Shock remake, especially its earlier visual direction

Useful for the hybrid-material idea:

- modern 3D forms and lighting,
- but selective use of coarse, visibly sampled, slightly lower-resolution texture information,
- allowing the image to retain an authored pixel-era grain without turning into a fully retro presentation.

The fictional world obviously does not contain "low-resolution textures." The lower-frequency texture detail is an art-language choice for the player.

That slight logical contradiction is acceptable because it can subconsciously establish a material world that feels designed rather than infinitely smooth and digitally generic.

## Elite Dangerous

Useful primarily for:

- ambient cockpit illumination,
- local colored light shaping the whole interior mood,
- exterior astronomical conditions feeding interior ambience,
- the emotional power of a dark room punctuated by practical displays and reflected environmental light.

`rusty-space` can push this farther than literal realism when doing so improves atmosphere or gameplay readability.

---

# 3. Core visual pillars

## 3.1 Cozy machine

The ship should feel like somewhere the player wants to remain.

Not luxurious in the yacht sense.

Comfort can come from:

- enclosure,
- warm pools of light,
- familiar machinery,
- soft clutter,
- instrument glow,
- wear patterns,
- human-scale nooks,
- recognizable repeated objects,
- and a sense that things have accumulated over time.

The ship can be ugly, patched, noisy, or industrial while still being pleasant to inhabit.

## 3.2 Chunky instrument architecture

Avoid the smooth dashboard slab.

Prefer:

- separate monitor housings,
- thick bezels,
- deep recesses,
- switch banks,
- control islands,
- structural frames,
- cable chases,
- trays,
- cabinet-like modules,
- prominent fasteners,
- obvious replaceable panels.

The cockpit should visually communicate:

> **This machine was assembled from things.**

That matters mechanically too because ship parts, repairs, and upgrades are central to the game.

## 3.3 Modern light, deliberately imperfect surface detail

Lighting should carry the high-fidelity end of the style.

Textures can carry more of the retro memory.

This creates a useful contrast:

- physically convincing shadow,
- strong indirect color,
- readable specular response,
- rich darkness,
- believable emissive spill,

combined with:

- lower texel density than a prestige photoreal asset,
- visibly discrete painted detail,
- occasional nearest-ish sampling or limited filtering where appropriate,
- broad rather than microscopic material noise,
- simple readable surface motifs.

The room can feel visually sophisticated without requiring every bolt to possess an 8K normal map documenting its childhood.

## 3.4 Synthetic perception

Displays, navigation imagery, exterior reconstructions, warnings, and some environmental cues are deliberately generated interpretations of sensor data.

This gives the art permission to mix:

- natural-looking objects,
- diagrammatic overlays,
- reconstruction artifacts,
- pixel-scale display language,
- false color,
- and projection effects.

## 3.5 Lived-in specificity

The interior should not look like a clean modular asset kit after assembly.

It needs evidence of occupation:

- old labels,
- patched paint,
- replaced panels,
- different generations of hardware,
- tape,
- handwritten notes,
- worn contact areas,
- cable additions,
- aftermarket modules,
- storage habits,
- personal junk,
- ugly adapters.

This becomes increasingly important as scavenged parts alter the vessel.

---

# 4. Do not globally pixelate the image

A full-screen retro treatment would work against the main strengths of the concept.

It would reduce:

- subtle lighting,
- depth,
- material contrast,
- atmosphere,
- visual comfort,
- and the pleasure of simply existing aboard the ship.

The game should not resemble a modern scene photographed through a fake 320x200 filter.

Retro character should instead emerge from **where detail is spent and how surfaces are authored**.

Useful retro carriers:

- texture resolution,
- screen rendering,
- fonts,
- icon design,
- decals,
- display refresh artifacts,
- simplified shape language,
- limited per-object palette choices,
- some intentionally hard transitions in texture detail.

Modern carriers:

- lighting,
- shadows,
- material response,
- depth,
- silhouette,
- volumetric ambience if useful,
- animation,
- camera response,
- spatial audio.

---

# 5. Texture strategy: low-frequency authored detail

The useful part of a lower-resolution texture look is not "make things blurry."

It is that the artist must make stronger decisions about what detail matters.

Prefer textures where:

- scratches are actual readable marks rather than procedural micro-noise,
- panels have broad stains rather than fifty layers of grunge,
- paint wear forms recognizable islands,
- warning stripes remain graphically bold,
- material transitions are obvious,
- labels and decals have strong shapes.

A modern photoreal pipeline often accumulates huge amounts of high-frequency detail that disappear into visual oatmeal at gameplay distance.

This project can deliberately avoid that.

## Possible practical rules to test

Not final requirements, just prototype hypotheses:

- modest texture resolutions for ordinary structural modules,
- larger textures reserved for hero consoles and repeated atlases,
- mipmapping retained for stability,
- anisotropic filtering used cautiously rather than removing all texture character,
- selective nearest or intentionally coarse sampling only on displays / specific decorative materials,
- no global nearest-neighbor world texture rule,
- limited use of tiny procedural roughness noise,
- hand-authored broad roughness / wear regions.

The goal is **coarse intentionality**, not texture shimmer.

---

# 6. PBR is still useful

The style does not require abandoning physically based materials.

PBR is valuable because lighting is one of the main tools for giving the ship weight and atmosphere.

The trick is to use a restrained material vocabulary.

Useful families:

- painted metal,
- exposed worn metal,
- rubber / polymer trim,
- old plastic housings,
- fabric seating,
- dirty glass,
- emissive screens,
- translucent indicator covers,
- cable insulation,
- brushed industrial surfaces.

Avoid making every object a showcase of complicated multilayer material scanning.

A surface can have physically coherent response while its texture information remains deliberately broad and slightly coarse.

---

# 7. Lighting should do a huge amount of the work

Full-bright retro lighting is the wrong direction.

The interior should be allowed to become quite dark.

Important sources:

- instrument emission,
- practical ceiling / task lights,
- colored status lamps,
- engineering machinery glow,
- exterior stellar light,
- planetary reflected light,
- nearby station illumination,
- emergency lighting,
- temporary electrical failures.

## Lighting can exceed literal plausibility

If a local astronomical condition produces an appealing ambient hue inside the ship, use it even if a strict radiometric model says the effect would be subtler.

For example:

- a gas giant can push a broad colored bounce into the cockpit,
- a blue-white star can create cold edge light through the viewing area,
- a red stellar environment can gently contaminate shadow color,
- a station approach can produce moving bands of artificial light.

The fiction is already full of synthetic viewing surfaces and smart glass. There is plenty of room for tasteful cheating.

The main requirement is internal consistency, not astronomical photometry.

---

# 8. The helm should be an object, not a HUD pedestal

Because most active gameplay happens while navigating, the helm must carry an enormous amount of visual identity.

It should have:

- physical depth,
- a clear sitting position,
- large readable instrument blocks,
- dedicated navigation display area,
- secondary system readouts,
- obvious mechanical hierarchy,
- room for later scavenged additions.

The navigation screen should look physically built into the ship rather than becoming a borderless game viewport.

Possible construction:

- deep bezel,
- physical control strip beneath it,
- side repeaters,
- warning lamps at the periphery,
- optional small exterior viewing area beyond / above it.

The player should immediately understand where the "helm" is when walking through the room.

---

# 9. Displays are where stronger retro treatment can live

Displays can push much harder into old visual language than the physical world.

Possible characteristics:

- coarse render resolution,
- limited palettes per instrument,
- bright phosphor-like color choices,
- chunky bitmap-ish typography,
- scan / reconstruction artifacts,
- low-frequency refresh behavior,
- block diagrams,
- oscilloscope-like strips,
- simple ship silhouettes,
- bold warning symbology.

Different equipment families can have distinct display cultures:

### Old industrial

- green / amber monochrome,
- low information density,
- very stable and readable,
- chunky type.

### Civilian modern

- richer color,
- clean diagrams,
- better sensor reconstruction,
- softer integration with the surrounding panel.

### Military salvage

- dense data,
- hard contrast,
- utilitarian symbology,
- ugly adapter framing when grafted into a civilian helm.

### Pirate / improvised

- mismatched displays,
- repurposed modules,
- unconventional layouts,
- bizarre firmware artifacts.

The important caution:

> Better hardware should not simply mean prettier high-resolution screens.

Old or damaged hardware can look fantastic in its own particular way.

---

# 10. Exterior windows: use an economy of views

A full submarine interior with no outside view would be technically convenient and fictionally defensible, but loses some of the cozy "I am inside a spaceship" appeal.

A huge panoramic canopy would provide the opposite problem: beautiful, but expensive and visually demanding in every interior state.

The likely sweet spot is **selective windows**.

Possible arrangement:

- one hero forward viewing area around the helm,
- one or two small side ports,
- perhaps one secondary observation slit / nook elsewhere,
- most of the ship enclosed.

This gives the outside universe emotional presence without requiring every wall to participate in an exterior rendering problem.

---

# 11. Exterior views do not need to be literal simulation views

The navigation simulation, the navigation display, and what appears through physical windows can be related but distinct render spaces.

```text
A. navigation gameplay space
B. synthetic navigation reconstruction
C. interior exterior-view proxy
```

The interior set should not sit at literal scale inside the flight simulation.

Instead, window views can be driven from a **contextual proxy scene** containing only what the interior needs to sell the current situation.

Possible inputs:

- dominant star direction and color,
- nearby planet / moon presence,
- broad asteroid density,
- station proximity,
- major ship contacts,
- local field activity,
- current travel regime.

The proxy can exaggerate distances, compress depth, simplify geometry, or omit irrelevant objects.

The requirement is emotional and contextual truth rather than coordinate truth.

---

# 12. Progressive exterior-view implementation

Do not solve the final window technology on day one.

## Level 1: ambient sky fake

- stars,
- one directional stellar light,
- optional nebular background,
- no direct relationship to navigation state.

Useful for validating interior mood.

## Level 2: contextual ambience

Feed the current system / local region into the interior:

- star color,
- planetary glow,
- asteroid silhouettes,
- station light,
- field activity.

No literal shared geometry required.

## Level 3: ship-relative proxy scene

Build a small secondary scene around the stationary interior camera:

- selected nearby rocks,
- station pieces,
- planetary disc,
- distant traffic,
- field particles.

Positions can be compressed / remapped from navigation context.

## Level 4: shared assets and derived state

Reuse:

- asteroid meshes,
- station silhouettes,
- ship proxies,
- planet materials,
- environmental palettes,

between the navigation reconstruction and the interior exterior proxy.

They do not need identical projection rules.

---

# 13. Leaving the helm can rebuild the outside context

The player is not expected to walk around while actively performing high-speed navigation.

That creates an excellent cheat boundary.

When the player leaves the helm:

1. capture the current meaningful navigation context,
2. derive a stationary / slowly evolving exterior proxy,
3. populate the visible window scene,
4. continue broad environmental ambience while the player wanders.

This does not need to be a frozen cubemap.

A small proxy scene can maintain:

- gentle asteroid drift,
- station-relative movement,
- star lighting,
- occasional traffic,
- distant field effects.

The player receives continuity without requiring the actual navigation simulation to remain literally positioned outside every window.

---

# 14. Modular interior kit strategy

The interior should be built as a kit from the beginning, even if the first prototype only contains one ship.

Useful module families:

## Structure

- wall bays,
- floor sections,
- ceiling sections,
- doorway frames,
- bulkheads,
- corridor joins,
- window frames.

## Control architecture

- monitor housings,
- helm consoles,
- switch banks,
- side panels,
- instrument clusters,
- diagnostic racks.

## Ship systems

- capacitor cabinets,
- cooling equipment,
- field controller racks,
- conduits,
- pumps,
- power units,
- removable equipment bays.

## Habitation

- bunk modules,
- storage,
- galley pieces,
- seats,
- tables,
- lockers,
- sanitary / utility modules if needed.

## Dressing

- wires,
- hoses,
- labels,
- taped repairs,
- tools,
- containers,
- personal effects,
- spare parts.

This supports:

- alternate ships,
- altered layouts,
- visible upgrades,
- salvaged equipment,
- damage states,
- and procedural / semi-procedural assembly if useful later.

---

# 15. Modules need variation layers so the ship does not look procedural

A modular kit can easily become sterile.

Use variation layers:

- alternate paint states,
- decals,
- grime masks,
- replacement panels,
- cable overlays,
- damage patches,
- object clutter,
- lighting differences,
- part-family differences.

The player's ship should increasingly look like a history of repair decisions rather than a level editor exercise.

A later salvaged military component can be visibly too large, mounted on an adapter plate, and lit differently from the civilian modules around it.

---

# 16. Art direction should reinforce mechanical state

The ship's visuals can communicate its build and condition.

Examples:

- oversized emitter hardware produces an obvious additional cabinet or external feed assembly,
- damaged stabilization makes a panel vibrate under load,
- a hot drive adds warning light and changing emissive behavior,
- a huge capacitor installation occupies space and adds heavy bus cables,
- an improvised controller creates ugly physical adapters at the helm,
- bad cooling produces condensation / heat shimmer / fan noise in the relevant compartment.

The player should be able to walk through the ship and see evidence of the same state that changes handling.

---

# 17. Color should be local and practical, not globally themed

Avoid making every ship interior one giant neon color grade.

Instead build mood from overlapping practical sources:

- amber engineering panels,
- green old diagnostic screens,
- violet or blue navigation displays,
- red emergency lamps,
- warm habitation light,
- cold exterior spill.

The overall image can become saturated because these sources interact, but the room should still feel physically structured.

This is more interesting than choosing "the purple spaceship" and tinting every asset purple.

---

# 18. Dark does not mean unreadable

The desired mood relies on darkness and localized illumination, but interaction spaces must remain readable.

Useful cheats:

- gentle practical fill around interactable clusters,
- emissive edge cues,
- slightly exaggerated bounce,
- material roughness chosen for useful highlights,
- local adaptation / exposure control,
- selective instrument illumination.

The player should feel that the ship is dim without continually fighting the image to find a door handle.

---

# 19. Navigation reconstruction should visually belong to the ship

The synthetic navigation view is not a separate art project.

Its palette, typography, scan behavior, reconstruction effects, and framing should derive from the physical helm hardware.

When the player sits down:

- the screen becomes the dominant gameplay representation,
- but visible bezel / nearby controls retain the sense of occupying a room,
- system lights and ambient illumination react around it,
- reconstruction transitions reinforce that the computer is building a human-readable model of space.

The display and the cockpit should feel designed by the same fictional technology culture.

---

# 20. Reconstruction transition as visual identity

A short synthetic-resolution effect could become a recurring visual motif.

Possible sequence:

1. rough mass / silhouette appears,
2. mesh faces are slightly exploded away from object center,
3. confidence / scan information flashes through,
4. faces collapse into their reconstructed positions,
5. normal synthetic shading resolves,
6. trajectory and field overlays settle on top.

Keep it very short.

It should feel like the navigation computer snapping an interpretation into focus, not playing a cutscene.

The same grammar can be reused for:

- new contacts,
- entering local reconstruction mode,
- leaving field-navigation mode,
- sensor reacquisition,
- arriving after an intersystem transition,
- damaged sensor uncertainty.

---

# 21. Prototype art milestones

## Prototype A: one cockpit corner

Build:

- seat,
- helm housing,
- one large display,
- one side console,
- one structural wall bay,
- one practical light.

Test the basic hybrid:

- modern lighting,
- chunky forms,
- intentionally modest texture detail.

Do not build a whole ship yet.

## Prototype B: texture comparison board

Render the same cockpit with:

1. ordinary high-resolution filtered modern textures,
2. deliberately lower-frequency / lower-resolution authored textures,
3. aggressively retro nearest-sampled textures.

The desired result likely lives between 1 and 3, much closer to 2.

This should be treated as an art-direction experiment rather than decided philosophically.

## Prototype C: display integration

Add:

- reconstructed navigation imagery,
- bitmap-ish type,
- strong emissive screen light,
- chunky physical bezel.

The display should light the room rather than feel pasted onto it.

## Prototype D: one exterior viewing area

Start with a fake contextual space background.

Determine how much window is actually necessary to make the room feel like a spaceship.

## Prototype E: one lived-in secondary nook

Add something non-operational:

- bunk,
- tiny galley,
- engineering stool,
- observation seat,
- storage corner.

This tests whether the ship feels worth wandering around when not navigating.

---

# 22. Anti-goals

Avoid:

- full-screen pixelation,
- CRT filters over the entire world,
- intentionally ugly retro rendering as a fidelity exercise,
- flat full-bright interiors,
- immaculate generic hard-surface sci-fi,
- infinite tiny procedural surface noise,
- every panel being a touchscreen,
- smooth monolithic cockpit architecture,
- panoramic glass everywhere,
- literal astronomical rendering outside every window,
- asset-kit modularity with no signs of occupation,
- "retro" meaning only scanlines and chromatic aberration.

The goal is not nostalgia cosplay.

It is to recover some of the **visual decisiveness, chunky tactility, and cockpit intimacy** of older space games while keeping the lighting, depth, atmosphere, and comfort available to a modern renderer.

---

# 23. Short art-direction summary

```text
FORM
chunky, modular, bezel-heavy, physically assembled

TEXTURE
selectively coarse, authored, lower-frequency, never globally pixelated

MATERIAL
simple coherent PBR families, not photogrammetry maximalism

LIGHT
modern, dark, practical, colored, atmospheric, willing to cheat for mood

DISPLAY
stronger retro language, synthetic reconstruction, bitmap-ish information design

WINDOWS
few but emotionally valuable, backed by a contextual proxy scene rather than literal scale

INTERIOR
stationary set, lived-in, cozy, increasingly shaped by salvage and repair
```

The guiding image is:

> **A ship interior that feels like the cockpit you remember from an old space game, except you can stand up, walk into it, see the colored instrument light crawling across battered physical surfaces, and discover that the coarse little screen world is the ship's deliberate reconstruction of an enormous universe outside.**

Style: Authored neo-retro

or

world-space neo-retro

Not necessarily a recognized genre label, but it describes the philosophy unusually well:

retro influence lives primarily in asset construction and visual grammar, while the renderer remains modern, stable and free to use contemporary lighting.

In summary:

Prefer retro authored into the scene over retro imposed on the final image: coarse texel language, chunky forms and restrained surface detail should remain stable under a clean modern camera
