# Where the proposed assets go from here

Addendum to this folder's review, written after three uplift experiments on the same two
meshes. **This is input for `GRAPHICS_AA_UPLIFT.md`'s waves, not a competing plan** — where
it disagrees with that document, that document wins; where it adds, fold it in.

## What the experiments showed

**1. Shading and lighting alone moved the models a full tier.** `uplift_shading_cycles.png`
is the *identical geometry* as `proposed_beauty.png` — 1,124 and 1,008 triangles, no new
vertices, no textures. The only changes were the ones `GRAPHICS_AA_UPLIFT.md` §4 already
prescribes: simple albedo, authored roughness at 0.38–0.44, metal only on trim, a cool rim
light for silhouette separation, a desaturated board that does not compete, and AO plus
contact shadow from a real ground plane.

That is direct evidence for the doc's own claim that *"the single highest-value art change
is authored roughness with simple albedo, not more detail."* It cost nothing in budget.

**2. A naive geometry-detail pass made things worse.** `uplift_failed_inset.png` is a
procedural panel-inset plus a 2-segment hardened bevel across every large side face. It
raised the tower from 1,124 to 4,036 triangles and **destroyed the stepped plinth** — the
one element the game-size test had shown was carrying the read. Kept here deliberately: it
is the clearest possible argument that detail applied without silhouette judgment is a
regression, not an improvement.

**3. The proposals are still not AAA, and more polygons will not get them there.** They are
clean stylized blockouts. What they lack against the reference in §4.1 is surface detail
that survives, edge wear, albedo variation, bloom-integrated emissive, and secondary forms —
almost none of which is *mesh density*.

## The thing you actually saw online

Blender MCP demos that produce "extremely detailed models" are, in the overwhelming
majority, not hand-assembling primitives through Python the way this session did. They are
calling the **generative 3D integrations built into the same addon**:

- **Hyper3D Rodin** (`generate_hyper3d_model_via_text` / `_via_images`)
- **Tencent Hunyuan3D** (`generate_hunyuan3d_model`)

Both are wired into the addon already and both are **switched off** in this setup — I left
all four third-party integrations unchecked during install because they ship data to
external services and need API keys. They can be turned on in the BlenderMCP sidebar panel
in about ten seconds.

**But turning them on would walk straight back into this review's findings.** Rodin and
Hunyuan are the same class of tool as Meshy — the tool that produced the two assets this
folder just critiqued for being 15,000 triangles, seven 1024 maps, baked-in lighting, and a
silhouette that dissolves at 46px. Swapping one text-to-3D generator for another changes
the flavour, not the failure mode.

**Where they genuinely help:** anything pre-rendered, where triangle count and baked
lighting are irrelevant — send-dock card art, upgrade and store screens, icons, marketing
shots. That is a real gap in this project and generative 3D is well suited to it. Recommend
enabling them for that purpose, scoped explicitly, rather than for board assets.

## What "AAA" means for a game rendered at 46–105px

This is the reframe worth arguing about before any more asset work happens.

The measured on-screen sizes are 16–105px for creeps. At that size, perceived quality is
carried by **silhouette, value hierarchy, material response, lighting, and effects** — not
by mesh density or texel density. `GRAPHICS_AA_UPLIFT.md` §4.1 reaches the same conclusion
from the reference frames: the reference is *low-poly with excellent shading*.

So the honest statement of the goal is: **AAA-feeling shading over correctly-budgeted
low-poly forms**, plus high-detail generative work reserved for 2D and pre-rendered
surfaces. A 15,000-triangle creep is not closer to AAA than a 1,000-triangle one — it is
the same silhouette with a worse frame budget.

## Ordered recommendation

Sequenced by visual return per unit of work. Items 1–3 need no new art at all.

1. **One shared stylized shader for all 30 units.** Rim term, gradient ramp, AO tint.
   §4 records that 119 of 149 materials are stock `URP/Lit` with zero Shader Graph.
   This is the largest single jump available and it is a shader task, not an art task.
2. **Authored roughness at 0.35–0.5 with simplified albedo**, replacing Meshy's generated
   albedo. Tune by eye against a capture; do not adopt a number on trust.
3. **Bake and bind AO.** Item 6 found the ORM red channel is 0.000 in all 21 packed maps —
   there is no AO on disk to recover, so it has to be baked. Blender MCP is genuinely good
   at this: it is a batch operation with an objective result.
4. **Decimate + LOD the existing Meshy meshes.** Neither prefab has a `LODGroup`. This
   captures most of the cost win *without* losing the character the proposals gave up, and
   is the single highest-value change to the assets themselves.
5. **High-to-low normal bake.** This is where surface detail belongs — in the normal map,
   not the mesh. Sculpt or boolean a high-poly, bake to the 1k low-poly. It is also the one
   place where a generative high-detail mesh is genuinely useful for a board asset: as
   *bake source*, never as the shipped mesh.
6. **Dark contour separation.** §4.1 observed the reference carries a darkened edge that
   keeps units readable over both light and dark boards. LTW has nothing equivalent.
7. **Lighting and grade as an owned, revisited task**, not one-time setup.

## Honest limits of this toolchain

Worth stating plainly so expectations are calibrated:

- **Procedural primitive assembly through MCP tops out around where these models are.**
  It is strong at parametric hard-surface (the stepped plinth), batch operations, bakes,
  LOD generation, material setup, measurement, and renders. It is weak at organic form,
  sculpted detail, and anything needing high-frequency visual taste — each iteration is a
  full render round-trip, and experiment 2 shows what happens when a geometry operation is
  applied without judgment.
- **The models here have no UVs, no rig, and no animation.** They are silhouette and budget
  studies.
- **Nothing here has been seen in the running game.** Same weakness R1 keeps naming. The
  Cycles renders are not the URP shading path and should not be read as a preview of it.

## Suggested next step

Not more asset generation. Take **recommendation 1** — the shared stylized shader — and
apply it to the existing Meshy roster. It is testable, it is the biggest measurable jump,
and it would tell us whether the current assets are actually the problem or whether they
have simply never been lit properly. My strong expectation, on the evidence of experiment 1,
is the latter.
