# Fungal Curse: Echoes of the Mire — Systems Manifesto

Production setup guide for the modular systems shipped in `Assets/Scripts`.
Target: **Unity 6000.3**, **URP 2D Renderer**, **New Input System**, **16 PPU** pixel art.

Code delivered:
- `Player/PlayerController.cs` — velocity-injected movement, jump buffer (0.12s), coyote time (0.14s), dynamic gravity (fall 4.8 / lowJump 3.5), dash with layer-collision I-Frames.
- `Enemies/GoblinAI.cs` — de-coupled enum FSM (Patrol → Anticipation 0.25s → ActiveStrike 0.10s → Recovery 0.45s) with edge/wall raycasts.
- `Systems/ObjectPooler.cs` + `Systems/IPooledObject.cs` — GC-free spawn framework.
- `Combat/Projectile.cs` — pooled spore-bolt reference (no Instantiate/Destroy).

Project layers created automatically: **Player (8)** and **Enemies (9)**.
Input action added automatically: **Dash** (Keyboard `Left Ctrl`, Gamepad `Right Shoulder`).

---

## 1. Player & Enemy Wiring (quick start)

### Player GameObject
1. Create the player sprite. Add `Rigidbody2D` (Body Type **Dynamic**, **Freeze Rotation Z**), a `Collider2D` (Capsule recommended), and `PlayerController`.
2. Set the GameObject **Layer = Player**.
3. Child empty `GroundCheck` at the feet; assign it to *Ground Check*. Set *Ground Layer* to your tilemap layer.
4. Assign the project **InputSystem_Actions** asset to *Input Actions*.
5. Assign the `SpriteRenderer` (auto-found if left empty).
6. Tag the player **Player** so the goblin can auto-resolve its target.

### Goblin GameObject (`Goblin_Thief.prefab`)
1. Add `Rigidbody2D` (Dynamic, Freeze Rotation Z) + `Collider2D` + `GoblinAI`. Set **Layer = Enemies**.
2. Add two child empties:
   - `EdgeCheck` at the front-bottom corner → *Edge Check*.
   - `WallCheck` at body height on the front → *Wall Check*.
3. Set *Ground Layer* to the tilemap layer.
4. Leave *Player* empty to auto-find by tag, or assign explicitly.

> Both controllers flip with `SpriteRenderer.flipX` only — child probes, lights and checkpoints never get mirrored out of place.

---

## 2. PRODUCTION TILEMAP & OPTIMIZATION MANIFESTO

Goal: a single static collision surface with **no internal edges**, so the player and goblin never snag on the seams between adjacent tiles.

### 2.1 Grid & Tilemap hierarchy
1. **GameObject ▸ 2D Object ▸ Tilemap ▸ Rectangular**. This creates `Grid` → `Tilemap`.
2. On the **Grid**, set **Cell Size = (1, 1, 0)**. With 16 PPU sprites sliced at 16×16, each tile fills exactly one cell with zero sub-pixel drift.
3. Rename the child Tilemap to `Tilemap_Ground` and set its **Layer = Ground** (create this layer if needed; it is the layer your `groundLayer` masks point to).

### 2.2 Build the Tile Palette (Pixel Tiles Pack)
1. Open **Window ▸ 2D ▸ Tile Palette**, **Create New Palette** ("MireTiles").
2. Select the source textures under `Assets/Pixel Tiles pack/Blocks` and `.../Tiles`.
   For each: Import settings → **Sprite Mode = Multiple** (if it is a sheet), **Pixels Per Unit = 16**, **Filter Mode = Point (no filter)**, **Compression = None**, **Mesh Type = Full Rect**.
3. Drag the sliced sprites into the palette and paint your level onto `Tilemap_Ground`.

### 2.3 The anti-snag collision stack (the important part)
On `Tilemap_Ground` add these three components, in this order:

1. **Tilemap Collider 2D**
   - Tick **Use Composite** (this defers geometry to the Composite Collider — individual per-tile boxes are merged, not used directly).
2. **Rigidbody 2D**
   - **Body Type = Static**. A static body is the correct, cheapest choice for non-moving world geometry and is required for clean composite merging.
3. **Composite Collider 2D**
   - **Geometry Type = Polygons** — merges all painted tiles into continuous polygon outlines. The internal tile-to-tile edges are removed entirely, which is what eliminates the "snagging" where a character catches on the seam between two flush tiles.
   - **Vertex Distance**: leave at default `0.0005`. For 16 PPU you can raise it slightly (e.g. `0.05`) to weld near-coincident vertices if you ever see micro-gaps.
   - **Offset Distance / Edge Radius**: keep `0`. A non-zero edge radius rounds corners and re-introduces catch points — leave it flat.

> Why this works: `Tilemap Collider 2D` (Use Composite) feeds raw per-cell shapes into `Composite Collider 2D`, which fuses them. With **Polygons** geometry the floor becomes one solid outline, so a moving collider slides across the whole surface as if it were a single box — no interior vertices to catch on.

### 2.4 Material to stop residual edge catching
Even with a composite, a `Box`/`Capsule` collider can nick a vertex at high speed. Mitigate:
- Assign a **Physics Material 2D** with **Friction = 0** to the *player's* collider (movement friction is handled in code via `MoveTowards`, not physics).
- Prefer a **Capsule Collider 2D** on the player; its rounded base glides over any micro-vertex far better than a box.

### 2.5 Pixel Perfect Camera (16 PPU native)
1. Select **Main Camera**. It must be **Orthographic** and use the URP renderer.
2. **Add Component ▸ Pixel Perfect Camera** (URP package, namespace `UnityEngine.Rendering.Universal`).
3. Configure exactly:
   - **Assets Pixels Per Unit = 16** — must match every sprite's import PPU. Mismatch causes shimmering.
   - **Reference Resolution** = your design resolution, e.g. **320 × 180** (16:9, scales cleanly to 1080p ×6, 720p ×4). Pick the internal pixel canvas the game is authored for.
   - **Crop Frame = None** (or `Pillarbox`/`Letterbox` if you must lock aspect).
   - **Grid Snapping = Pixel Snap** — snaps rendered sprites to the pixel grid each frame, killing sub-pixel jitter during movement.
   - **Upscale Render Texture**: enable for the cleanest integer-scaled look (renders at reference resolution then upscales). Disable if you want sprites to rotate/scale more smoothly at the cost of some shimmer.
4. All sprite imports must use **Filter Mode = Point**, **Compression = None**, and PPU **16** for the camera math to land on whole pixels.

---

## 3. URP 2D LIGHTING BLUEPRINT

Goal: sprite lights that reveal the stone textures of the Mire while preserving a dark, gritty mood — no washed-out highlights.

### 3.1 Confirm the 2D Renderer asset
This project already ships `Assets/Settings/Renderer2D.asset` and `UniversalRP.asset`.
1. **Edit ▸ Project Settings ▸ Graphics**: ensure the **Scriptable Render Pipeline Settings** points to `UniversalRP.asset`.
2. Open `UniversalRP.asset`: under **Renderer List** confirm `Renderer2D` is the **Default Renderer**. If you need a fresh one: **Create ▸ Rendering ▸ URP ▸ 2D Renderer (Renderer2D)** and assign it.
3. Every sprite that should receive light must use a **Sprite-Lit-Default** material (the default for new Sprite Renderers under the 2D renderer). Sprites using *Sprite-Unlit-Default* ignore all 2D lights — use unlit deliberately for UI/FX only.

### 3.2 Blend Styles (the wash-out control)
On `Renderer2D.asset` you have up to **4 Light Blend Styles**. These define how light values composite onto sprites.

Recommended dark-fantasy setup:
- **Blend Style 0 — "Dark Multiply" (ambient/stone):**
  - **Blend Mode = Multiply**. Multiply darkens by default and only lifts where light exists, so unlit stone stays deep and shadowed instead of flat-grey.
  - Use this style for your large fill lights.
- **Blend Style 1 — "Additive Glow" (torches/spores):**
  - **Blend Mode = Additive** for small, intense point lights (torch flames, glowing mushrooms). Keep their **Intensity low (0.4–0.8)** and **radius tight** so they pop without blowing out the scene.

> Rule of thumb: **Multiply for area lighting, Additive for small emissive accents.** Additive everywhere is what causes washout.

### 3.3 Global light & ambient floor
1. **GameObject ▸ Light ▸ 2D ▸ Global Light 2D**. This is your ambient base.
2. Set its **Color** to a cold desaturated tone (e.g. deep blue-grey `#2A3340`) and **Intensity ≈ 0.12–0.20**. A low global keeps the mire dark; point lights provide contrast.
3. Assign it to **Blend Style 0 (Multiply)** via the light's *Blend Style* dropdown.

### 3.4 Sprite (point/spot) lights on stone
1. **GameObject ▸ Light ▸ 2D ▸ Spot Light 2D** (Freeform/Point) for torches.
2. Settings to avoid washout:
   - **Intensity 0.4–0.8** (not 1+).
   - **Falloff** moderate-to-high so light fades into darkness rather than ending in a hard bright disc.
   - **Color** warm but not pure white (e.g. `#E0A060`) to read as firelight against cold stone.
   - **Target Sorting Layers**: restrict to the layers you want lit (e.g. `Default`, `Ground`) so background parallax stays dark.
3. **Normal Maps (optional, high impact):** if you author/generate normal maps for the Pixel Tiles, enable **Light ▸ Quality ▸ Use Normal Map** and set the sprite material to **Sprite-Lit-Default** with a Secondary Texture `_NormalMap`. This makes stone catch raking light along its carved edges — the core of the "gritty" look — without raising overall brightness.

### 3.5 Shadows (optional)
1. Add **Shadow Caster 2D** to solid tilemap chunks / pillars (`Cave_Pillar`).
2. On each Spot Light 2D enable **Shadows ▸ Strength ≈ 0.6–0.8**. Partial strength keeps shadows readable without crushing detail to pure black.

### 3.6 Tone control via Volume (final polish)
1. The project ships `DefaultVolumeProfile.asset`. Add a **Global Volume** to the scene referencing it.
2. Add overrides:
   - **Color Adjustments**: lower **Post Exposure** slightly, drop **Saturation** (-15 to -25) for grit, and pull **Contrast** up (+10 to +20).
   - **Vignette**: subtle (Intensity ≈ 0.25) to draw focus inward and reinforce the oppressive mire atmosphere.
   - Avoid heavy **Bloom**; if used at all, keep Threshold high so only the brightest torch cores glow — this preserves the dark aesthetic.

---

## 4. ARCHITECTURAL RESTRICTIONS — enforced in code

| Rule | Where enforced |
|------|----------------|
| No `AddForce` for horizontal movement | `PlayerController.ApplyHorizontalMovement` uses `linearVelocity` + `MoveTowards`. |
| No `transform.localScale` flipping | `PlayerController.HandleFacing` / `GoblinAI.ApplyFacingVisual` use `SpriteRenderer.flipX`. |
| No inflated gravity scale | `PlayerController` keeps `baseGravityScale` fixed and applies `FallMultiplier 4.8` / `LowJumpMultiplier 3.5` dynamically in `ApplyDynamicGravity`. |
| No Instantiate/Destroy for projectiles | `ObjectPooler` recycles instances; `Projectile.ReturnToPool` calls `Despawn`, never `Destroy`. |

### Spawning a projectile (example call site)
```csharp
// muzzle.right defines the flight direction; rotation is baked into transform.right.
ObjectPooler.Instance.Spawn(
    "PlayerProjectile",
    muzzle.position,
    Quaternion.FromToRotation(Vector3.right, aimDirection));
```

### ObjectPooler scene setup
1. Create an empty `--- Systems ---` GameObject, add **ObjectPooler**.
2. Add one **Pool** entry: *Tag* = `PlayerProjectile`, *Prefab* = your projectile prefab (with `Projectile` + trigger `Collider2D` + `Rigidbody2D`), *Initial Size* = 32, *Expandable* = true.
3. Ensure the projectile prefab's `Collider2D` has **Is Trigger = true** and its *Collision Mask* targets walls + enemy layers.
