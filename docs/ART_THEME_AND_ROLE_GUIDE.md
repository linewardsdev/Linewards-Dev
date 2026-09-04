# Line Wards Art Theme And Role Guide

## Purpose

Line Wards needs to look better, but the first goal is not surface detail. The first goal is instant gameplay recognition.

Every tower and creep should be identifiable by silhouette, pose, and motion before the player reads a label or notices color. This guide defines the shared theme language for tower and creep production so art, generated placeholders, prefabs, UI icons, and future effects all point in the same direction.

## North Star

Line Wards is original ward-tech fantasy: crystal machines, rune plates, signal conduits, pressure cores, mechanical ritual objects, and clean board-game readability.

Target quality is closer to polished early-2000s strategy readability than modern noise: bold silhouettes, simple readable shapes, strong role motifs, modest texture detail, and clear team/accent color slots. The game should not chase realistic materials, dense ornament, or nostalgia copies of another RTS.

Avoid:

- Warcraft, Blizzard, or copied RTS faction language.
- Generic medieval towers, castles, monsters, or race-coded silhouettes.
- Color-only identification.
- Tiny details that vanish on a phone.
- One shared tower body with only a different glow color.

## Global Shape Rules

Each gameplay role gets a unique primary read:

| Role Type | Primary Read | Secondary Read | Motion/Effect Read |
| --- | --- | --- | --- |
| Focus damage | Long axis, aim line, weapon rail | Bolt, string, lens, barrel | Straight shot |
| Control | Wide footprint, ring, dish, field | Rotating or held halo | Slow pulse or field |
| Utility/economy | Mast, antenna, capacitor, signal node | Gold/mint relay accents | Charge or link tick |
| Splash/burst | Compact core, round shock body | Expanding rings | Radial burst |
| Long-range specialist | Tall spire, prism lens | Facets, beam aperture | Thin beam |
| Fast creep | Low, sharp, forward wedge | Tail or speed fins | Darting motion |
| Heavy creep | Wide, armored mass | Plates, low center | Weighty bob |
| Multi creep | Cluster of repeated bodies | Many small dots/shards | Jittering group |
| Stealth creep | Offset echo silhouette | Broken outline, shimmer | Phase/flicker |
| Siege creep | Directional ram/cannon mass | Warning stripe, wedge nose | Slow pressure/windup |

## Tower Role Guide

### Arrow Tower

Runtime id: `tower.arrow`

Gameplay read: cheap, reliable, single-target early defense.

Silhouette must include:

- A clear bow, crossbow, ballista, or tension-limb shape.
- A visible bolt rail, arrow spine, or focused firing line.
- A narrow forward-facing aim direction.

Good motifs:

- Crossbow limbs mounted on a ward plinth.
- Tall bow arc with a glowing string.
- Rail-bow with a crystal bolt loaded across the top.

Avoid:

- A plain spire that could be Prism.
- A generic cannon that could be Siege.
- Only using a triangle or yellow color to say "arrow."

Phone-size test: at gameplay camera distance, the player should be able to say "that is the bow/crossbow tower" without opening the build menu.

### Control Ward

Runtime id: `tower.control`

Gameplay read: area control, anti-stack, soft crowd answer.

Silhouette must include:

- A wide dish, ring, or field projector.
- A lower, broader body than Arrow and Prism.
- A visible control halo or restraint band.

Good motifs:

- Circular ward dish with a suspended center core.
- Flat ring emitter over a squat base.
- Rune clamp or containment hoop.

Avoid:

- A weapon barrel or bow limb.
- A tall spire profile.
- Looking like a passive economy beacon.

Phone-size test: it should read as "field/control" even in grayscale.

### Relay Ward

Runtime id: `tower.relay`

Gameplay read: utility, support, economy/scaling identity.

Silhouette must include:

- A mast, antenna, capacitor stack, or relay tower line.
- At least one signal node separate from the base.
- A calmer support posture than weapon towers.

Good motifs:

- Thin mast with a glowing capacitor cap.
- Twin signal rods connected to a central mint/gold core.
- Small beacon dish pointed upward rather than at creeps.

Avoid:

- Looking like Arrow's weapon rail.
- Looking like Prism's crystal spear.
- Overusing gold so it becomes unreadable on the economy UI.

Phone-size test: it should read as "support beacon," not a direct weapon.

### Pulse Ward

Runtime id: `tower.pulse`

Gameplay read: short-range burst and dense-pressure answer.

Silhouette must include:

- A compact round or drum-like core.
- One or more visible shock rings.
- A lower, heavier footprint than Prism.

Good motifs:

- Reactor puck with expanding circular plates.
- Round pressure core with nested rings.
- Short pillar with a visible burst diaphragm.

Avoid:

- Fireball/mage-tower language.
- Tall beam tower profile.
- Ring details so thin they disappear on mobile.

Phone-size test: if the tower is firing, the role should be obvious from the radial burst.

### Prism Ward

Runtime id: `tower.prism`

Gameplay read: long-range specialist and priority target answer.

Silhouette must include:

- A tall crystalline lens-spire.
- Faceted prism or aperture geometry.
- A clear beam/focus direction.

Good motifs:

- Glass obelisk with an embedded lens.
- Split crystal forks focusing into one beam.
- Tall spire over a small stabilizing base.

Avoid:

- A bow silhouette, even if it is also long and narrow.
- A generic wizard tower.
- Wide rings that make it read as Control or Pulse.

Phone-size test: it should read as the tallest precision tower in the set.

## Creep Role Guide

### Runner

Runtime id: `creep.runner`

Gameplay read: cheap speed pressure.

Silhouette must include:

- Low, sharp, forward-pointing body.
- Tail, fin, or speed-line shape.
- Minimal armor.

Good motifs:

- Dart construct.
- Arrowhead signal shard.
- Skittering needle core.

Avoid:

- Round blob bodies.
- Heavy armor plates.
- Looking like a tiny tower projectile.

### Brute

Runtime id: `creep.brute`

Gameplay read: health pressure.

Silhouette must include:

- Wide armored body.
- Big shoulder/side plates or heavy shell.
- Slow, grounded stance.

Good motifs:

- Armored ward golem.
- Pressure core inside a plated shell.
- Heavy rune block with a visible weak core.

Avoid:

- Long cannon/ram shapes that could be Siege.
- Too many tiny spikes.
- Narrow speed shapes.

### Swarm

Runtime id: `creep.swarm`

Gameplay read: many small bodies and dense pressure.

Silhouette must include:

- Multiple visible units or repeated shards.
- Clustered footprint wider than a Runner.
- Jitter or separation in motion.

Good motifs:

- Signal mites.
- Small glass shardlings.
- Spark cluster with 3-5 readable bodies.

Avoid:

- One single blob.
- So many parts that the lane becomes visual static.
- Tiny bodies with no shared outline.

### Shade

Runtime id: `creep.shade`

Gameplay read: stealth or low-visibility pressure.

Silhouette must include:

- Broken or offset outline.
- Echo copy, shimmer ring, or phase shadow.
- Still-readable core body.

Good motifs:

- Ghost-signal construct.
- Split prism echo.
- Hollow core with delayed afterimage.

Avoid:

- Pure transparency as the only cue.
- Full invisibility that hides gameplay information.
- Looking like Swarm because of too many echoes.

### Siege

Runtime id: `creep.siege`

Gameplay read: slow, high-threat lane pressure.

Silhouette must include:

- Heavy directional front.
- Ram, drill, cannon, or wedge pressure shape.
- Larger warning profile than Brute.

Good motifs:

- Crystal battering ram.
- Slow pressure engine with a glowing impact nose.
- Heavy core sled with danger markings.

Avoid:

- Generic tank or medieval siege engine.
- Brute's rounded armor profile.
- Tiny turret detail that disappears on mobile.

## Shared Accent System

Use accents consistently:

| Accent | Meaning |
| --- | --- |
| Player color | Ownership or sender identity only. |
| Mint/cyan | Signal, ward energy, active targeting, support. |
| Gold | Economy, relay, income, reward. |
| Red/coral | Leak danger, heavy damage, invalid state. |
| Pale violet/blue | Prism, stealth, arcane lensing. |

Never make a role depend on accent color alone. A grayscale screenshot should still distinguish the five tower roles and five creep roles.

## Icon Rules

Build/send icons should be simplified versions of the same silhouettes:

- Arrow: bow limb plus bolt rail.
- Control: ring/dish.
- Relay: mast and signal node.
- Pulse: compact core plus ring.
- Prism: tall faceted spire.
- Runner: dart.
- Brute: armored block.
- Swarm: three clustered dots/shards.
- Shade: core plus offset echo.
- Siege: ram/wedge.

Icons should not invent a different metaphor than the in-game model.

## Production Acceptance Checklist

A tower or creep visual is not accepted until:

- It reads by silhouette without labels.
- It reads in grayscale.
- It reads at normal portrait phone framing.
- It remains readable during heavy sends.
- It has an obvious UI icon counterpart.
- It has a source folder and license/source notes.
- It does not borrow protected faction, unit, building, icon, or UI language from another game.

