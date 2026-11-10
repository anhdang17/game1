# Handoff Report: Milestone M3 — Old Quarter Environment Art & Particle Integration

## 1. Observation

- **Initial State**:
  - `Source/Stages/HanoiExplorationScene.cs` lines 708-715 rendered a Cartesian checkerboard grid (`Raylib.DrawLine` loops step 64x48 in `HanoiPalette.StoneMortar`).
  - Lines 717-740 rendered a flat monochrome wall rectangle and plain shutters without flower boxes or cornices.
  - Lines 743-754 rendered 2 crude steel triangles for the Long Bien Bridge trusses without sunset backlighting.
  - Lines 504-510 rendered Bà An's stall via `VectorRenderer.DrawMarketStall`, showing only an empty generic wooden stall without Bà An NPC, broth pot, or rising steam.
  - No dash trigger or cloud puff particle emissions were hooked in `HanoiExplorationScene.Update`.
  - Automated tests require private reflection fields `_stallPuzzleModal`, `_phoAssemblyModal`, `_albumViewerModal` to exist with intact visibility (`Milestone3Iteration2ProgressionEmpiricalTests.cs:246`, `ChallengerM3ProgressionStressTests.cs:576`).
  - Automated headless test suite (`ChallengerM5PackagingAndPerformanceTests.cs:58-64`) exercises scene lifecycle with uninitialized Raylib (`Raylib.IsWindowReady() == false`).

- **Modified File**:
  - `Source/Stages/HanoiExplorationScene.cs`: Exclusive ownership respected; no other files were modified.

- **Automated Verification**:
  - Command: `dotnet test Tests/GameProject.Tests.csproj -c Release`
  - Output: `Passed! - Failed: 0, Passed: 567, Skipped: 0, Total: 567, Duration: 10 s - GameProject.Tests.dll (net10.0)`
  - Exit code: 0

---

## 2. Logic Chain

1. **Grid Line Removal & Organic Cobblestones**:
   - Removed lines 708-715 (Cartesian grid loops) entirely.
   - Built a deterministic running-bond flagstone paver pattern across X: 0..1600, Y: 110..850.
   - Slabs (54px x 28px) are staggered on alternate rows with 3px mortar joints, soft corner rounding (`Raylib.DrawRectangleRounded`), and deterministic 4-tone subtle flagstone variations.

2. **Environment Art Elements**:
   - **Moss Patches**: Added `MossPatches` deterministic array along colonial wall base (Y: 110..116) and curb base (Y: 846..850), drawn with multi-layered deep moss, fresh moss, and sage lichen ellipses.
   - **4 Rain Puddles**: Placed 4 puddles at (340, 640), (780, 560), (1120, 440), and (1360, 680) with dark wet rims, sky-reflecting surface, glare highlight, and animated concentric ripple arcs (`DrawEllipseLines`).
   - **Lotus Petals & Almond Leaves**: Scattered 12 pink lotus petals (`HanoiPalette.LotusPink` with `LotusRose` tips) and 11 crimson almond leaves (`Lá bàng đỏ` with central veins) across the street.
   - **Colonial Facade**: Upgraded to warm French ochre (`HanoiPalette.WallYellow`), bottom plaster shadow, terracotta tiled overhang with ridge lines, decorative molded cornice with dentil blocks, green shutters with brass latches, and overflowing bougainvillea flower boxes with cascading magenta blooms.
   - **Swaying Red Lanterns**: Placed silk red lanterns under eaves with natural pendular sway (`MathF.Sin(_totalTime * 2.2f) * 4.0f`), gold caps, silk tassels, and pulsating 3-tier ambient glow halos (`Warm Glow`: 42px outer diffuse, 26px mid warmth, 14px inner radiant).
   - **Long Bien Bridge Silhouette**: Placed sunset sky gradient and glowing setting sun crest at the eastern street opening (X: 1300..1600), rendered Eiffel-style cantilever double-span steel trusses with vertical tower piers at X=1380 and X=1520, arched upper chords with sunset rim highlights, internal X-lattice diagonals, and rivet nodes.

3. **Bà An Pho Stall & Steam Integration**:
   - Replaced `VectorRenderer.DrawMarketStall` with `VectorRenderer.DrawBaAnPhoStall(BaAn.Position, "Quán Phở Bà An", _totalTime)` in `yQueue`.
   - Instantiated `ParticleEmitter _particles = new();` (exposed via `public ParticleEmitter Particles => _particles;`).
   - Continuously emitted broth pot steam in `Update(dt)` at `_particles.EmitSteam(BaAn.Position + new Vector2(16f, -24f), dt)` both during exploration and active dialogue.

4. **Cloud Glide & Dash Integration**:
   - Configured `Kinematics.Friction = 1200.0f` for smooth, cloud-like gliding stops.
   - Hooked dash input in `Update(dt)`: `if (InputService.IsDashJustPressed && Kinematics.TryDash(InputService.MovementDirection)) _particles.EmitCloudPuff(Kinematics.Position, count: 4);`.
   - When dashing: emitted trailing cloud puff bursts (`count: 4`) each frame.
   - When moving: throttled cloud puff trail emission (`count: 4` every 0.12s) and triggered footstep sound on footstep trigger.
   - Emitted subtle magic sparkles at Bút Chì's lead tip (`HanoiPalette.ButChiMagicCyan`).
   - Advanced particles via `_particles.Update(deltaTime)` and rendered in world-space camera mode via `_particles.Draw()`.

5. **Test Invariants & Headless Safety**:
   - Retained private reflection fields `_stallPuzzleModal`, `_phoAssemblyModal`, `_albumViewerModal` intact.
   - Guarded `Draw()`, `DrawStreetEnvironment()`, `DrawLongBienGateEntity()`, and `DrawDialogueModal()` with `if (!Raylib.IsWindowReady()) return;`.

---

## 3. Caveats

- **Caveat 1**: Audio triggers (`StepStreet`) rely on `AudioService`; in headless test environments this safely defaults to `NullAudioService`.
- **Caveat 2**: Procedural visual elements (cobblestones, moss, puddles, petals, leaves) intentionally use deterministic coordinate tables and arithmetic hashing rather than `System.Random` in `Draw()` to prevent per-frame screen flicker and zero GC allocations.
- No other caveats.

---

## 4. Conclusion

Milestone M3 is fully implemented in `Source/Stages/HanoiExplorationScene.cs`:
- Grid lines are permanently eliminated.
- Organic cobblestone ground, moss clusters, puddles with sky reflections/ripples, lotus petals, and almond leaves are procedurally rendered.
- Colonial facades feature molded cornices, green shutters, bougainvillea flower boxes, and swaying red lanterns with 3-tier ambient glow halos.
- Long Bien Bridge boasts cantilever steel trusses with warm sunset backlighting.
- Bà An's stall is integrated with full NPC visuals, and her broth pot emits continuous rising steam.
- Bé Mây features cloud glide, dash kinematics, and trailing cloud puffs.
- All 567 unit tests pass with zero failures.

---

## 5. Verification Method

1. **Automated Unit & Regression Tests**:
   ```bash
   dotnet test Tests/GameProject.Tests.csproj -c Release
   ```
   **Expected**: 567 Passed, 0 Failed, 0 Skipped.

2. **Visual & Gameplay Verification**:
   ```bash
   dotnet run
   ```
   - Verify street ground has organic cobblestone slabs with soft mortar joints, no Cartesian grid lines.
   - Verify moss at base of walls and curbs, 4 reflective rain puddles with gentle ripples, and scattered lotus petals / almond leaves.
   - Verify colonial yellow shophouses with green shutters, flower boxes, and warm red lanterns swaying gently.
   - Verify Long Bien Bridge trusses in the eastern sky with sunset backlighting.
   - Verify Bà An NPC at her stall with steaming copper broth pot.
   - Press `Space` / `Shift` to dash; observe cloud puff bursts and smooth gliding deceleration.
