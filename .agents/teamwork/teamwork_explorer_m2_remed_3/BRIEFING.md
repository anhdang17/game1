# BRIEFING — 2026-11-09T00:32:00Z

## Mission
Investigate Inventory alias symmetry (Item_ThaoMoc / Item_GiaVi) and FontManager Unicode FormC normalization / decomposed combining marks, providing drop-in C# fixes.

## 🔒 My Identity
- Archetype: explorer
- Roles: investigation, synthesis
- Working directory: /Users/mac/Projects/game1
- State folder: /Users/mac/Projects/game1/.agents/teamwork/teamwork_explorer_m2_remed_3
- Original parent: e7f3d2fb-3839-4a66-a5bc-d1756013c7ba
- Milestone: Milestone 2 Remediation

## 🔒 Key Constraints
- Read-only investigation — do NOT implement
- Do NOT modify source files directly
- Write report to /Users/mac/Projects/game1/.agents/teamwork/teamwork_explorer_m2_remed_3/handoff.md
- Lazy senior dev: shortest working diff, solve root cause, minimum code that works, no unnecessary abstractions

## Current Parent
- Conversation ID: e7f3d2fb-3839-4a66-a5bc-d1756013c7ba
- Updated: 2026-11-09T00:32:00Z

## Investigation State
- **Explored paths**:
  - `Source/Core/Models/Inventory.cs`
  - `Source/Graphics/FontManager.cs`
  - `Tests/Challenger2EmpiricalTests.cs` (lines 457-481, 783-818)
  - `Tests/InteractionAndQuest1Tests.cs` (lines 363-380)
  - `Source/Stages/WorldMapScene.cs` & `Source/Stages/AlbumViewerScene.cs` (detected pre-existing compile errors)
- **Key findings**:
  1. `Inventory.cs`: `AddItem` only checked exact string; calling `AddItem(GiaVi)` when `ThaoMoc` is present added both (count = 2). `RemoveItem(GiaVi)` failed to remove `ThaoMoc`, creating a direct asymmetry with `HasItem(GiaVi) == true`.
  2. `FontManager.cs`: Codepoints table contains 762 codepoints (strictly asserted by existing test `Graphics_FontManager_GetVietnameseCodepoints_Covers762Glyphs` and `VietnameseUnicode_DecomposedNFDDiacritics_NotInCodepointsTable_RevealsNFCRequirement`). Adding combining marks to `GetVietnameseCodepoints()` would break existing tests and fail in Raylib (which lacks OpenType glyph shaping). The proper fix is Form C normalization (`.Normalize(NormalizationForm.FormC)`) in `DrawText`, `DrawTextOutline`, and `MeasureText`, transforming decomposed NFD strings into precomposed glyphs already in the 762 codepoints table.
  3. Pre-existing compilation error detected in `WorldMapScene.cs:126` and `AlbumViewerScene.cs:219,223` that blocks `dotnet test`. Documented exact fixes for worker.
- **Unexplored areas**: None within Explorer 3 scope.

## Key Decisions Made
- `Inventory.AddItem`: check `HasItem(itemId)` prior to addition, guaranteeing idempotency across aliases and exact IDs.
- `Inventory.RemoveItem`: check `IsThaoMocOrGiaVi` to allow symmetric removal across aliases.
- `FontManager`: add `NormalizeText(string? text)` helper, normalize input in `DrawText`, `DrawTextOutline`, `MeasureText`, keep `GetVietnameseCodepoints()` at 762.
- Updated `Challenger2EmpiricalTests.cs` alias test to assert symmetry instead of characterizing the previous bug.

## Artifact Index
- DISPATCH.md — Task instructions
- BRIEFING.md — Persistent context & memory
- progress.md — Liveness heartbeat & progress log
- handoff.md — Final remediation report
