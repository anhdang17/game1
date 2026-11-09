# Dispatch: M2 Remediation Explorer 3 (Inventory Alias Asymmetry & FontManager NFD Normalization)

## Task
Investigate and design the exact code remediation for:
1. `Source/Core/Models/Inventory.cs`: `AddItem` only checks exact string, not alias `Item_ThaoMoc` vs `Item_GiaVi`, and `RemoveItem` fails on alias. `HasItem` recognizes alias but `AddItem` allows adding both, resulting in count 2.
2. `Source/Graphics/FontManager.cs`: Codepoints table omits decomposed combining marks (U+0300..U+036F), and `DrawText`, `DrawTextOutline`, `MeasureText` do not normalize input strings via `.Normalize(NormalizationForm.FormC)`.

## References to Read
- `/Users/mac/Projects/game1/.agents/teamwork/ORIGINAL_REQUEST.md`
- `/Users/mac/Projects/game1/PROJECT.md`
- `/Users/mac/Projects/game1/.agents/teamwork/teamwork_challenger_m2_2/handoff.md` (Observations 4 & 5)
- `Source/Core/Models/Inventory.cs`
- `Source/Graphics/FontManager.cs`

## Output Requirements
Write your remediation design to `/Users/mac/Projects/game1/.agents/teamwork/teamwork_explorer_m2_it2_3/handoff.md` with:
- Exact file paths and line numbers
- Drop-in replacement C# code blocks
- Verification test plan.
Do NOT modify source files directly.

