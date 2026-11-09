# Handoff Report: Milestone 2 — Interaction Loop, Market Merchants & Quest 1 Mechanics, and UI HUD

**Author**: teamwork_explorer_m2_3  
**Date**: 2026-11-08  
**Scope**: Milestone 2 — Hanoi Exploration & Quest 1 (Phở Gathering)  
**Target Roles**: teamwork_worker_m2, teamwork_reviewer_m2, teamwork_challenger_m2, teamwork_orchestrator  

---

## 1. Observation

### 1.1 Specification Directives
- `PROJECT.md` lines 70–74 specify Milestone 2 features:
  - **Feature 7**: Interaction Prompt Bubble (`[E] Tương tác` floating prompt when near interactables).
  - **Feature 8**: Ingredient Gathering Loop (Collect 5 authentic phở ingredients from market merchants).
  - **Feature 9**: Inventory HUD Display (Upper screen inventory tracker showing collected ingredient icons).
- `PROJECT.md` lines 108–113 specify Interface Contracts:
  - `QuestManager`: Exposes `StartQuest("Quest_Hanoi_01")`, `RecordObjectiveProgress("Item_BanhPho")`, `IsQuestCompleted("Quest_Hanoi_01")`.
  - `Inventory`: Exposes `AddItem(string itemId)`, `HasItem(string itemId)`, `GetItems()`.
  - `EventBus`: Fires `OnItemCollected`, `OnQuestUpdated`.
- `ORIGINAL_REQUEST.md` lines 18–20:
  - "Cơ chế điều khiển di chuyển nhân vật Mây mượt mà (WASD/phím mũi tên) và phím tương tác với NPC/vật thể."
  - "1. Dẫn Mây thu thập đủ các nguyên liệu nấu phở trên các quầy hàng."

### 1.2 Core Domain Code State
- `Source/Core/Quests/HanoiQuests.cs` lines 10–31 defines `Quest_Hanoi_01` ("Thu thập nguyên liệu nấu Phở") with 5 distinct mandatory objectives:
  1. `ItemIds.BanhPho` ("Bánh Phở tươi chần kỹ")
  2. `ItemIds.ThitBo` ("Thịt Bò tươi thái mỏng")
  3. `ItemIds.NuocDung` ("Nước dùng hầm xương gia truyền")
  4. `ItemIds.HanhLa` ("Hành lá & Rau mùi tươi")
  5. `ItemIds.ThaoMoc` ("Túi Thảo mộc hồi quế Lãn Ông")
- `Source/Core/Models/Inventory.cs` lines 9–26 defines item constants:
  - `ItemIds.BanhPho = "Item_BanhPho"`
  - `ItemIds.ThitBo = "Item_ThitBo"`
  - `ItemIds.NuocDung = "Item_NuocDung"`
  - `ItemIds.HanhLa = "Item_HanhLa"`
  - `ItemIds.ThaoMoc = "Item_ThaoMoc"` (with `ItemIds.GiaVi` as alias).
  - `Inventory.AddItem(itemId)` is idempotent (lines 51–60): duplicate items return `false` without duplicate insertion.
- `Source/Core/Models/QuestFlags.cs` lines 12–32 and 62–73 provides:
  - `Q1_Started`, `Q1_BanhPho_Collected`, `Q1_ThitBo_Collected`, `Q1_NuocDung_Collected`, `Q1_HanhLa_Collected`, `Q1_ThaoMoc_Collected`, `Q1_Completed`.
  - Helper methods: `GetQ1CollectedCount()` and `AreAllQ1IngredientsCollected()`.
- `Source/Core/Ports/IInputService.cs` lines 17–20:
  - `bool IsInteractJustPressed { get; }` (maps to `[E]` key or `[Space]`).
- `Source/Core/Ports/IAudioService.cs` lines 9–10:
  - `SfxType.ItemCollected`, `SfxType.InteractPrompt`, `SfxType.ButtonClick`, `SfxType.DialogueBlip`, `SfxType.PhoCookSuccess`.
- `Source/Core/Events/GameEvents.cs`:
  - `ItemCollectedEvent(string ItemId, string ItemName, int CurrentCount, int RequiredCount)`
  - `QuestStartedEvent`, `QuestProgressUpdatedEvent`, `QuestRequirementsMetEvent`, `QuestCompletedEvent`.

### 1.3 Codebase Layout Observation
- Folders `Source/UI/`, `Source/Stages/`, `Source/Graphics/`, `Source/Audio/` are scaffolded but empty.
- Test suite currently executes 192 tests passing in 83 ms (`dotnet test Tests/GameProject.Tests.csproj`).

---

## 2. Logic Chain

### 2.1 Feature 7: Interaction Prompt Bubble Architecture & Detection Logic

#### A. Proximity Math & Detection Radius
- **Detection Radius**: `48.0f` px (satisfies 45–50 px requirement).
- **Coordinate Basis**: Distance between Bé Mây's interaction point (center-bottom: `Player.Position + new Vector2(0, -Player.Height * 0.4f)`) and Interactable's interaction origin (`entity.InteractionCenter`).
- **Euclidean Distance Squared**:
  $$\Delta x = \text{player}.X - \text{entity}.X$$
  $$\Delta y = \text{player}.Y - \text{entity}.Y$$
  $$\text{distSq} = \Delta x^2 + \Delta y^2$$
  $$\text{inProximity} = (\text{distSq} \le R^2) \quad \text{where } R^2 = 48.0^2 = 2304.0$$
  Comparing squared distances avoids expensive square root calculations in the frame update loop.

#### B. Priority Selection Algorithm (Nearest Entity)
When multiple interactables are within the 48 px interaction radius (e.g. dense market stalls):
1. Filter candidates where `distSq <= R * R` AND `entity.CanInteract(context)`.
2. Find candidate with `min(distSq)`.
3. If two entities have identical distance (difference $< 0.001$), apply deterministic tie-breaking by `string.CompareOrdinal(a.Id, b.Id) < 0`.
4. The winning entity becomes `TargetInteractable`. All other entities receive `IsTarget = false`.

#### C. Floating Bubble Animation Math
- **Anchor**: Above entity visual top:
  $$X_{\text{bubble}} = \text{entity.Position.X}$$
  $$Y_{\text{bubble}} = \text{entity.Position.Y} - \text{entity.VisualHeight} - 18.0\text{px} + \Delta y_{\text{bob}}$$
- **Vertical Sine Wave Floating (Bobbing)**:
  $$\Delta y_{\text{bob}} = \sin(t_{\text{total}} \times 4.2) \times 3.5\text{px}$$
  Provides a natural, gentle, cartoony hover.
- **Fade & Scale In/Out (Easing)**:
  When an entity enters or exits focus:
  $$\text{BubbleAlpha} = \text{Math.Clamp}(\text{BubbleAlpha} \pm \Delta t \times 8.0, 0.0, 1.0)$$
  $$\text{Scale} = 0.85 + 0.15 \times \text{EaseOutBack}(\text{BubbleAlpha})$$
  Ensures the bubble pops into view smoothly rather than popping abruptly.

#### D. Bubble Visual Styling & Layout
Procedural vector rendering (no PNG dependency):
- **Container**: Rounded pill (`RectangleRounded` with `roundness: 0.5f`). Width: ~124 px, Height: ~34 px.
  - Background: Cream Ivory (`#FCFAF2`, `Color(252, 250, 242, 245)`).
  - Border: Warm Charcoal Espresso (`#362B28`, 1.5 px outline).
  - Subtle drop shadow: Offset `(0, 3)` px, `Color(0, 0, 0, 40)`.
- **Downward Pointer Notch**:
  Isosceles triangle at bottom center of bubble pointing directly to entity:
  `v1 = (X - 6, Bottom)`, `v2 = (X + 6, Bottom)`, `v3 = (X, Bottom + 7)`.
- **Keycap Badge**:
  - Pill/rounded square: 20x20 px at left of bubble.
  - Background: Warm Golden Ochre (`#E6A13B`, `Color(230, 161, 59, 255)`).
  - Glyph: Bold white `E`.
- **Action Label**:
  - Vietnamese text: `Tương tác` (or dynamic: `Nói chuyện`, `Nhận hàng`) rendered via `FontManager` at 14 px, color Dark Charcoal (`#282422`).
- **Audio Cue Debounce**:
  When `TargetInteractable` transitions from `null` to a valid entity, trigger `IAudioService.PlaySfx(SfxType.InteractPrompt)` with a 0.35s debounce guard to prevent flutter at proximity boundary.

---

### 2.2 Feature 8: Market Merchants & Quest 1 Gathering Loop

#### A. 6 Interactable Entities Specification & Locations
The Hanoi Old Quarter exploration scene features 1 primary quest giver NPC and 5 specialized market stalls/merchants:

| # | Entity ID | Display Name | Role / Zone | Target Item | Map Coords (X, Y) |
|---|-----------|--------------|-------------|-------------|-------------------|
| 1 | `Npc_BaAn` | Bà An (Bà Cụ Bán Phở) | Quán Phở Gia Truyền / Hub | Quest Giver / Turn-in | `(200, 300)` |
| 2 | `Stall_BanhPho` | Quầy Bánh Phở & Mì Sợi | Phố Hàng Buồm | `Item_BanhPho` | `(380, 220)` |
| 3 | `Stall_ThitBo` | Quầy Thịt Bò Bác Tư | Ngõ Chợ Hàng Buồm | `Item_ThitBo` | `(560, 240)` |
| 4 | `Stall_NuocDung` | Bếp Nước Dùng Nóng | Đầu Ngõ Phố Cổ | `Item_NuocDung` | `(740, 220)` |
| 5 | `Stall_HanhLa` | Gánh Rau Tươi Cô Lan | Vỉa hè Hàng Bè | `Item_HanhLa` | `(440, 420)` |
| 6 | `Stall_ThaoMoc` | Hiệu Thuốc Bắc Lãn Ông | Phố Lãn Ông | `Item_ThaoMoc` | `(660, 430)` |

#### B. Detailed Interaction States & Dialogue Flows

##### 1. Bà An (`Npc_BaAn`)
- **State A: Quest 1 Not Started** (`!flags.Q1_Started`):
  - Dialogue: *"Chào bé Mây! Hôm nay bà chuẩn bị mở quán phở nhưng lại thiếu mất mấy nguyên liệu tươi ngon nhất. Cháu giúp bà dạo một vòng phố cổ xin đủ 5 món: Bánh phở tươi, Thịt bò mềm, Nước dùng ninh xương, Hành lá rau mùi và Gói thảo mộc Lãn Ông nhé!"*
  - Effect:
    - Calls `QuestManager.StartQuest("Quest_Hanoi_01")`.
    - Sets `flags.Q1_Started = true`.
    - Publishes `QuestStartedEvent`.
    - Plays `SfxType.ButtonClick`.
    - HUD displays: *"Nhiệm vụ mới: Thu thập nguyên liệu nấu Phở (0/5)"*.
- **State B: In Progress (1–4 Items)**:
  - Dialogue: *"Cháu đã gom được {count}/5 món rồi! Cố gắng ghé nốt các quầy còn lại nhé bé Mây: Bánh phở ở Hàng Buồm, Thịt bò Bác Tư, Nồi nước dùng, Rau tươi Cô Lan và Thuốc bắc Lãn Ông."*
- **State C: All 5 Collected** (`flags.AreAllQ1IngredientsCollected() && !flags.Q1_Completed`):
  - Dialogue: *"Tuyệt vời quá bé Mây ơi! Đủ cả 5 nguyên liệu tươi ngon rồi: Bánh phở dẻo mềm, thịt bò tươi, nước dùng thơm ngát, hành hoa và thảo mộc Lãn Ông. Giờ bà cháu mình cùng bắt tay vào xếp bát phở hoàn chỉnh nhé!"*
  - Effect:
    - Calls `QuestManager.CompleteQuest("Quest_Hanoi_01")`.
    - Sets `flags.Q1_Completed = true`.
    - Publishes `QuestCompletedEvent`.
    - Plays `SfxType.PhoCookSuccess`.
    - Unlocks Quest 2 prerequisite!
- **State D: Quest 1 Completed**:
  - Dialogue: *"Bà cháu mình cùng vào bếp ghép bát phở chuẩn vị Hà Nội nào cháu yêu!"* (Triggers transition to Quest 2 Phở Assembly).

##### 2. Quầy Bánh Phở & Mì Sợi (`Stall_BanhPho`)
- **Pre-Quest**: *"Bánh phở tráng mỏng sợi dai dẻo vừa ra lò sớm nay, cháu qua chào Bà An chưa?"*
- **Active Q1 (Uncollected)**:
  - Dialogue: *"Bánh phở tươi tráng mỏng sợi dai mềm của bác đây! Mang về cho Bà An nấu phở nhé bé Mây!"*
  - Grant:
    - `inventory.AddItem(ItemIds.BanhPho)`
    - `questManager.RecordObjectiveProgress(HanoiQuests.Quest1Id, ItemIds.BanhPho, 1)`
    - `flags.Q1_BanhPho_Collected = true`
    - `audioService.PlaySfx(SfxType.ItemCollected)`
    - Toast: `+ Bánh Phở Tươi`
- **Already Collected (Idempotent Reminder)**:
  - Dialogue: *"Bác đã đưa phần bánh phở tươi ngon nhất cho cháu rồi đó! Mang về cho Bà An sớm nhé!"*
  - Zero duplicate items, zero quest progress changes.

##### 3. Quầy Thịt Bò Bác Tư (`Stall_ThitBo`)
- **Pre-Quest**: *"Bác Tư chào bé Mây! Thịt bò sáng nay đỏ au tươi rói, cháu có mua gì cho gia đình không?"*
- **Active Q1 (Uncollected)**:
  - Dialogue: *"Thịt bò tươi thái mỏng bản hoa của Bác Tư đây! Nấu phở là phải có thịt tươi thế này mới ngọt nước!"*
  - Grant:
    - `inventory.AddItem(ItemIds.ThitBo)`
    - `questManager.RecordObjectiveProgress(HanoiQuests.Quest1Id, ItemIds.ThitBo, 1)`
    - `flags.Q1_ThitBo_Collected = true`
    - `audioService.PlaySfx(SfxType.ItemCollected)`
    - Toast: `+ Thịt Bò Tươi`
- **Already Collected (Idempotent Reminder)**:
  - Dialogue: *"Bác Tư đã chọn cho cháu phần nạm gầu ngon nhất rồi đấy! Mau mang về cho Bà An nhé!"*

##### 4. Bếp Nước Dùng Nóng (`Stall_NuocDung`)
- **Pre-Quest**: *"Nồi nước dùng đang liu riu sôi sùng sục, hương thơm ngào ngạt cả góc phố!"*
- **Active Q1 (Uncollected)**:
  - Dialogue: *"Nồi nước dùng ninh xương ống thơm lừng gia truyền đây! Bác đã chắt sẵn vào bình ấm, cẩn thận kẻo nóng nhé bé Mây!"*
  - Grant:
    - `inventory.AddItem(ItemIds.NuocDung)`
    - `questManager.RecordObjectiveProgress(HanoiQuests.Quest1Id, ItemIds.NuocDung, 1)`
    - `flags.Q1_NuocDung_Collected = true`
    - `audioService.PlaySfx(SfxType.ItemCollected)`
    - Toast: `+ Nước Dùng Gia Truyền`
- **Already Collected (Idempotent Reminder)**:
  - Dialogue: *"Nước dùng ninh xương đã được đóng bi đông cẩn thận cho cháu rồi đó!"*

##### 5. Gánh Rau Tươi Cô Lan (`Stall_HanhLa`)
- **Pre-Quest**: *"Rau thơm vườn bãi sông Hồng Cô Lan mới hái sớm nay, xanh non mởn!"*
- **Active Q1 (Uncollected)**:
  - Dialogue: *"Hành hoa củ chẻ và rau mùi tươi mơn mởn của Cô Lan đây! Rắc lên bát phở là thơm ngát cả góc phố!"*
  - Grant:
    - `inventory.AddItem(ItemIds.HanhLa)`
    - `questManager.RecordObjectiveProgress(HanoiQuests.Quest1Id, ItemIds.HanhLa, 1)`
    - `flags.Q1_HanhLa_Collected = true`
    - `audioService.PlaySfx(SfxType.ItemCollected)`
    - Toast: `+ Hành Lá & Rau Mùi`
- **Already Collected (Idempotent Reminder)**:
  - Dialogue: *"Cô Lan đã tặng cháu bó hành mùi tươi ngon nhất sớm nay rồi nhé!"*

##### 6. Hiệu Thuốc Bắc Lãn Ông (`Stall_ThaoMoc`)
- **Pre-Quest**: *"Mùi hoa hồi và quế chi ngào ngạt khắp tiệm Lãn Ông trăm năm tuổi."*
- **Active Q1 (Uncollected)**:
  - Dialogue: *"Gói hoa hồi, quế chi, thảo quả sao vàng thơm lựng của tiệm thuốc bắc Lãn Ông đây! Linh hồn của nồi phở Hà Nội đấy cháu!"*
  - Grant:
    - `inventory.AddItem(ItemIds.ThaoMoc)`
    - `questManager.RecordObjectiveProgress(HanoiQuests.Quest1Id, ItemIds.ThaoMoc, 1)`
    - `flags.Q1_ThaoMoc_Collected = true`
    - `audioService.PlaySfx(SfxType.ItemCollected)`
    - Toast: `+ Túi Thảo Mộc Lãn Ông`
- **Already Collected (Idempotent Reminder)**:
  - Dialogue: *"Gói quế hồi thảo quả gia truyền chú đã gói ghém cẩn thận cho cháu rồi đấy!"*

#### C. Idempotency Invariant Verification
1. `Inventory.AddItem`: checks `_items.Contains(...)`; duplicate returns `false`.
2. `QuestObjective.IncrementProgress`: clamped to `TargetCount = 1`; subsequent calls return `false`.
3. `QuestFlags`: boolean properties `Q1_*_Collected`; setting `true` repeatedly is idempotent.
4. Merchant logic: checks `HasItem(ItemId)` or `QuestFlags` before executing grant logic; if already granted, executes reminder branch with no state mutations.

---

### 2.3 Feature 9: UI HUD Architecture

#### A. `InventoryBar.cs` Specification
- **Placement**: Top-center screen banner (`X = (ScreenWidth - 310) / 2, Y = 16`).
- **Dimensions**: Width: 310 px, Height: 58 px, Rounded corners (radius: 12 px).
- **Styling**:
  - Dark translucent container: `Color(24, 28, 36, 215)`.
  - Border: 1.5 px warm slate gold `Color(180, 160, 130, 190)`.
  - Subtle drop shadow: `Color(0, 0, 0, 50)`.
- **5 Slots Horizontal Layout**:
  - Slot size: 44x44 px, Spacing: 12 px.
  - Slot 1: Bánh Phở (`Item_BanhPho`)
  - Slot 2: Thịt Bò (`Item_ThitBo`)
  - Slot 3: Nước Dùng (`Item_NuocDung`)
  - Slot 4: Hành Lá (`Item_HanhLa`)
  - Slot 5: Thảo Mộc (`Item_ThaoMoc`)
- **Slot States**:
  - **Missing / Uncollected**:
    - Cavity background: `Color(36, 42, 52, 160)`.
    - Border: 1 px subtle dashed/dotted gray line `Color(80, 90, 105, 120)`.
    - Icon: Monochrome silhouette / gray outline (alpha 0.30).
  - **Acquired / Collected**:
    - Background: Warm ivory glow `Color(254, 250, 240, 255)`.
    - Border: Vibrant golden rim `Color(230, 161, 59, 255)`.
    - Icon: Full-color procedural vector drawing!
    - Corner pip: Tiny 6px emerald checkmark dot.
- **Acquisition Spring Pulse Animation**:
  - When `ItemCollectedEvent` arrives, slot triggers:
    `ScalePulse = 1.35f; RippleRadius = 10f; RippleAlpha = 1.0f;`
    In `Update(dt)`:
    `ScalePulse = Lerp(ScalePulse, 1.0f, dt * 10f);`
    `RippleRadius += dt * 40f; RippleAlpha -= dt * 2.5f;`
- **Hover Tooltip**:
  - Hovering pointer over slot displays tooltip card below the bar:
    - Line 1 (Bold 13px): Vietnamese Item Name (e.g. `Bánh Phở tươi`, `Thịt Bò tươi`).
    - Line 2 (11px): `Đã thu thập` (in emerald green) or `Chưa có (Tìm tại chợ)` (in warm gray).

#### B. Procedural Vector Icon Algorithms (Zero PNG Assets)
1. **`DrawIconBanhPho`**:
   - Background circle with soft shadow.
   - 3 overlapping smooth curved ribbon paths (representing tender white noodles) in ivory `#FCFAF2` with delicate drop shadow and soft noodle folds.
2. **`DrawIconThitBo`**:
   - Teardrop / rounded slice in crimson red `#C53030`.
   - 2 interior marbling accent streaks in soft marbled pink `#FED7D7`.
3. **`DrawIconNuocDung`**:
   - Golden-amber broth pot/droplet `#D69E2E` with honey shimmer `#ECC94B`.
   - 2 wavy rising steam plumes in soft translucent white `#E2E8F0` at 70% opacity.
4. **`DrawIconHanhLa`**:
   - Two concentric rings representing chopped scallion rounds (outer leaf green `#2F855A`, inner light lime `#9AE6B4`).
   - 2 tiny cilantro/coriander leaf petals branching sideways in deep emerald `#276749`.
5. **`DrawIconThaoMoc`**:
   - 8-lobed star anise shape (hoa hồi) in warm cinnamon brown `#7B341E` (4 crossed rounded capsules with central seed dot).
   - Rolled cinnamon stick (quế chi) behind it angled at 40° in warm hazel `#9C4221`.

#### C. `QuestHUD.cs` Specification
- **Placement**: Upper-left viewport (`X = 20, Y = 20`).
- **Dimensions**: Width: 265 px, Dynamic height: 78–92 px.
- **Styling**:
  - Parchment card: `Color(253, 251, 245, 235)`.
  - Left indicator strip: 5 px wide, Ochre gold `Color(214, 158, 46, 255)`.
  - Border: 1 px warm dark umber `Color(70, 58, 52, 200)`.
  - Corner radius: 8 px.
- **Content Hierarchy**:
  - Top category tag: `NHIỆM VỤ HIỆN TẠI` (10 px, bold, muted brown `#8C6D50`).
  - Quest Title: `Nấu Phở Hà Nội` (15 px, bold, dark espresso `#2D231E`).
  - Objective line:
    - If `Q1 InProgress`: `Thu thập nguyên liệu phở: (n/5)` (13 px, medium).
    - Progress Bar: 225 px wide, 6 px high. Background `#DDD6C9`, Fill: smooth green gradient `#38A169` to `#48BB78`.
    - If `RequirementsMet (n == 5)`: Pulsing golden alert text: `★ Mang nguyên liệu về cho Bà An!` (`Color(192, 86, 33, 255)`).
    - If `Q1 Completed`: Checkmark icon + `✓ Đã gom đủ 5 nguyên liệu!` in emerald.
- **Popup Notification Toasts**:
  - When item is acquired, toast appears at upper-right (`X = ScreenWidth - 300, Y = 24 + index * 48`):
    - Background: Dark charcoal pill with gold border.
    - Icon: Mini 20px item icon.
    - Text: `+ Đã nhận: {ItemName}`.
    - Lifespan: 2.8 seconds (Slide-in: 0.3s, Stay: 2.0s, Fade-out: 0.5s).

---

## 3. Concrete Class Signatures & Architecture

### 3.1 Pure Core Interaction Contracts (`Source/Core/Interactions/`)
Zero Raylib dependencies; fully testable in xUnit.

```csharp
namespace GameProject.Core.Interactions;

using System;
using System.Numerics;
using GameProject.Core.Models;
using GameProject.Core.Quests;
using GameProject.Core.Events;
using GameProject.Core.Ports;

public record InteractionContext(
    Vector2 PlayerPosition,
    Inventory Inventory,
    QuestManager QuestManager,
    QuestFlags QuestFlags,
    IEventBus EventBus,
    IAudioService AudioService
);

public interface IInteractable
{
    string Id { get; }
    string DisplayName { get; }
    Vector2 Position { get; }
    Vector2 InteractionCenter { get; }
    float InteractionRadius { get; } // Default 48.0f
    float VisualHeight { get; }      // For bubble anchoring (e.g. 40.0f)

    bool CanInteract(InteractionContext context);
    InteractionResult Interact(InteractionContext context);
    string GetPromptText(InteractionContext context);
}

public sealed record InteractionResult(
    bool Success,
    string DialogueSpeaker,
    string DialogueText,
    string? GrantedItemId = null,
    bool QuestStarted = false,
    bool QuestCompleted = false
);
```

### 3.2 InteractionManager (`Source/Core/Interactions/InteractionManager.cs`)

```csharp
namespace GameProject.Core.Interactions;

using System;
using System.Collections.Generic;
using System.Numerics;

public sealed class InteractionManager
{
    private readonly List<IInteractable> _interactables = new();
    public const float DefaultRadius = 48.0f;

    public IReadOnlyList<IInteractable> Interactables => _interactables;
    public IInteractable? CurrentTarget { get; private set; }

    public float BubbleAlpha { get; private set; }
    public float BubbleBobTimer { get; private set; }

    public void Register(IInteractable interactable)
    {
        ArgumentNullException.ThrowIfNull(interactable);
        if (!_interactables.Contains(interactable))
            _interactables.Add(interactable);
    }

    public void Unregister(IInteractable interactable) => _interactables.Remove(interactable);
    public void Clear() => _interactables.Clear();

    public IInteractable? FindNearestInteractable(Vector2 playerPosition, InteractionContext context)
    {
        IInteractable? nearest = null;
        float minDistanceSq = float.MaxValue;

        foreach (var entity in _interactables)
        {
            if (!entity.CanInteract(context))
                continue;

            float r = entity.InteractionRadius > 0 ? entity.InteractionRadius : DefaultRadius;
            float rSq = r * r;

            float dx = entity.InteractionCenter.X - playerPosition.X;
            float dy = entity.InteractionCenter.Y - playerPosition.Y;
            float distSq = dx * dx + dy * dy;

            if (distSq <= rSq)
            {
                if (distSq < minDistanceSq)
                {
                    minDistanceSq = distSq;
                    nearest = entity;
                }
                else if (Math.Abs(distSq - minDistanceSq) < 0.001f && nearest != null)
                {
                    // Deterministic tie-break by ID
                    if (string.CompareOrdinal(entity.Id, nearest.Id) < 0)
                    {
                        nearest = entity;
                    }
                }
            }
        }

        return nearest;
    }

    public void Update(float deltaTime, Vector2 playerPosition, InteractionContext context)
    {
        BubbleBobTimer += deltaTime;
        var prevTarget = CurrentTarget;
        CurrentTarget = FindNearestInteractable(playerPosition, context);

        if (CurrentTarget != null)
        {
            BubbleAlpha = Math.Clamp(BubbleAlpha + deltaTime * 8.0f, 0.0f, 1.0f);
            if (prevTarget != CurrentTarget)
            {
                context.AudioService.PlaySfx(SfxType.InteractPrompt);
            }
        }
        else
        {
            BubbleAlpha = Math.Clamp(BubbleAlpha - deltaTime * 8.0f, 0.0f, 1.0f);
        }
    }

    public InteractionResult? TryInteract(InteractionContext context)
    {
        if (CurrentTarget == null || !CurrentTarget.CanInteract(context))
            return null;

        return CurrentTarget.Interact(context);
    }
}
```

### 3.3 Concrete Merchants Implementation (`Source/Stages/Interactables/`)

```csharp
namespace GameProject.Stages.Interactables;

using System.Numerics;
using GameProject.Core.Interactions;
using GameProject.Core.Models;
using GameProject.Core.Quests;
using GameProject.Core.Events;
using GameProject.Core.Ports;

public sealed class BaAnNpc : IInteractable
{
    public string Id => "Npc_BaAn";
    public string DisplayName => "Bà An";
    public Vector2 Position { get; set; }
    public Vector2 InteractionCenter => Position + new Vector2(0, -20);
    public float InteractionRadius => 48.0f;
    public float VisualHeight => 48.0f;

    public BaAnNpc(Vector2 position) => Position = position;

    public bool CanInteract(InteractionContext context) => true;

    public string GetPromptText(InteractionContext context) =>
        context.QuestFlags.Q1_Completed ? "[E] Nói chuyện" : "[E] Tương tác";

    public InteractionResult Interact(InteractionContext context)
    {
        // State 1: Quest 1 Not Started
        if (!context.QuestFlags.Q1_Started)
        {
            context.QuestManager.StartQuest(HanoiQuests.Quest1Id);
            context.QuestFlags.Q1_Started = true;
            context.AudioService.PlaySfx(SfxType.ButtonClick);

            return new InteractionResult(
                Success: true,
                DialogueSpeaker: "Bà An",
                DialogueText: "Chào bé Mây! Hôm nay bà chuẩn bị nấu nồi phở gia truyền nhưng thiếu mất mấy nguyên liệu ngon nhất. Cháu giúp bà ghé các quầy hàng trong phố cổ xin đủ 5 món: Bánh phở tươi, Thịt bò mềm, Nước dùng ninh xương, Hành lá rau mùi và Gói thảo mộc thơm lừng nhé!",
                QuestStarted: true
            );
        }

        // State 2: Requirements Met -> Complete Quest 1
        if (context.QuestFlags.AreAllQ1IngredientsCollected() && !context.QuestFlags.Q1_Completed)
        {
            context.QuestManager.CompleteQuest(HanoiQuests.Quest1Id);
            context.QuestFlags.Q1_Completed = true;
            context.AudioService.PlaySfx(SfxType.PhoCookSuccess);

            return new InteractionResult(
                Success: true,
                DialogueSpeaker: "Bà An",
                DialogueText: "Tuyệt vời quá bé Mây ơi! Đủ cả 5 nguyên liệu hảo hạng rồi! Giờ bà cháu mình cùng bắt tay vào xếp bát phở hoàn chỉnh nhé!",
                QuestCompleted: true
            );
        }

        // State 3: Quest 1 Completed
        if (context.QuestFlags.Q1_Completed)
        {
            return new InteractionResult(
                Success: true,
                DialogueSpeaker: "Bà An",
                DialogueText: "Bà cháu mình cùng vào bàn ghép bát phở chuẩn vị Hà Nội nào cháu yêu!"
            );
        }

        // State 4: In Progress Reminder
        int count = context.QuestFlags.GetQ1CollectedCount();
        return new InteractionResult(
            Success: true,
            DialogueSpeaker: "Bà An",
            DialogueText: $"Cháu đã tìm được {count}/5 món rồi! Cố gắng ghé nốt các quầy còn lại nhé bé Mây: Bánh phở Hàng Buồm, Thịt bò Bác Tư, Nồi nước dùng, Rau tươi Cô Lan và Thuốc bắc Lãn Ông."
        );
    }
}

public sealed class PhoIngredientMerchant : IInteractable
{
    public string Id { get; }
    public string DisplayName { get; }
    public string ItemId { get; }
    public string ItemName { get; }
    public Vector2 Position { get; set; }
    public Vector2 InteractionCenter => Position + new Vector2(0, -18);
    public float InteractionRadius => 48.0f;
    public float VisualHeight => 42.0f;

    private readonly string _grantDialogue;
    private readonly string _reminderDialogue;
    private readonly string _preQuestDialogue;

    public PhoIngredientMerchant(
        string id,
        string displayName,
        string itemId,
        string itemName,
        Vector2 position,
        string grantDialogue,
        string reminderDialogue,
        string preQuestDialogue)
    {
        Id = id;
        DisplayName = displayName;
        ItemId = itemId;
        ItemName = itemName;
        Position = position;
        _grantDialogue = grantDialogue;
        _reminderDialogue = reminderDialogue;
        _preQuestDialogue = preQuestDialogue;
    }

    public bool CanInteract(InteractionContext context) => true;

    public string GetPromptText(InteractionContext context) =>
        context.Inventory.HasItem(ItemId) ? "[E] Nói chuyện" : "[E] Nhận nguyên liệu";

    public InteractionResult Interact(InteractionContext context)
    {
        // 1. Not started yet
        if (!context.QuestFlags.Q1_Started)
        {
            return new InteractionResult(
                Success: true,
                DialogueSpeaker: DisplayName,
                DialogueText: _preQuestDialogue
            );
        }

        // 2. Already collected -> Idempotent reminder
        if (context.Inventory.HasItem(ItemId))
        {
            return new InteractionResult(
                Success: true,
                DialogueSpeaker: DisplayName,
                DialogueText: _reminderDialogue
            );
        }

        // 3. Grant ingredient
        bool added = context.Inventory.AddItem(ItemId);
        if (added)
        {
            context.QuestManager.RecordObjectiveProgress(HanoiQuests.Quest1Id, ItemId, 1);
            context.QuestFlags.SetFlag($"Q1_{ItemId.Replace("Item_", "")}_Collected", true);
            context.EventBus.Publish(new ItemCollectedEvent(ItemId, ItemName, 1, 1));
            context.AudioService.PlaySfx(SfxType.ItemCollected);

            return new InteractionResult(
                Success: true,
                DialogueSpeaker: DisplayName,
                DialogueText: _grantDialogue,
                GrantedItemId: ItemId
            );
        }

        return new InteractionResult(
            Success: true,
            DialogueSpeaker: DisplayName,
            DialogueText: _reminderDialogue
        );
    }
}
```

### 3.4 Presentation UI Signatures (`Source/UI/`)

```csharp
namespace GameProject.UI;

using System;
using System.Collections.Generic;
using System.Numerics;
using Raylib_cs;
using GameProject.Core.Models;
using GameProject.Core.Quests;
using GameProject.Core.Events;

public sealed class InventoryBar
{
    public sealed class SlotViewModel
    {
        public string ItemId { get; init; } = "";
        public string DisplayName { get; init; } = "";
        public bool IsCollected { get; set; }
        public float ScalePulse { get; set; } = 1.0f;
        public float RippleRadius { get; set; }
        public float RippleAlpha { get; set; }
    }

    public IReadOnlyList<SlotViewModel> Slots { get; }
    public Rectangle Bounds { get; private set; }

    public InventoryBar(IEventBus eventBus);
    public void Update(float deltaTime, Inventory inventory, Vector2 pointerPos);
    public void Draw(Font font);
}

public sealed class QuestHUD
{
    public sealed class ToastMessage
    {
        public string Text { get; init; } = "";
        public string ItemId { get; init; } = "";
        public float Timer { get; set; } = 2.8f;
        public float Alpha { get; set; } = 1.0f;
    }

    public Rectangle Bounds { get; private set; }
    public IReadOnlyList<ToastMessage> ActiveToasts { get; }

    public QuestHUD(IEventBus eventBus);
    public void Update(float deltaTime, QuestManager questManager, QuestFlags flags);
    public void ShowToast(string text, string itemId = "");
    public void Draw(Font font);
}

public sealed class InteractionPromptBubble
{
    public void Draw(Vector2 entityScreenPos, float visualHeight, string actionText, float alpha, float bobTimer, Font font);
}
```

---

## 4. Caveats
1. **Headless Execution Constraint**: Presentation rendering calls Raylib (`Raylib.DrawRectangleRounded`, etc.). Raylib windowing must not be invoked during automated xUnit headless tests. All state logic, proximity calculations, dialogue state machines, and inventory tracking are cleanly encapsulated in `Source/Core/Interactions/` with zero Raylib dependency.
2. **Vietnamese Glyph Support**: When rendering `[E] Tương tác`, `Thu thập nguyên liệu phở`, and merchant dialogue strings, `FontManager.cs` (designed by `explorer_m2_2`) must load TTF fonts with Unicode ranges `0x0020-0x024F` and `0x1EA0-0x1EF9` to ensure no question-mark glyph corruption occurs.
3. **Map Coordinate Consistency**: The positions assigned to the 6 merchants (`(200, 300)`, `(380, 220)`, `(560, 240)`, `(740, 220)`, `(440, 420)`, `(660, 430)`) fit inside a standard 1280x720 or 1920x1080 Hanoi map layout. Ensure they do not overlap with obstacle bounding boxes established by `explorer_m2_1`.
4. **Item ID Alias Support**: `Inventory.cs` lines 79–86 already supports an alias between `Item_ThaoMoc` and `Item_GiaVi`. Merchants explicitly use `ItemIds.ThaoMoc`, but consumers querying `Item_GiaVi` will find it valid.

---

## 5. Conclusion
1. **Interaction Prompt Bubble**: A robust proximity detection system operating on squared distances with a 48 px radius, deterministic nearest-entity priority selection, smooth 3.5 px floating bobbing, and procedural pill badge rendering.
2. **Quest 1 Gathering Loop**: A complete 6-entity network (Bà An + 5 merchants) covering the full lifecycle (Pre-Quest guidance -> Item Grant with Chime SFX + Toast -> Idempotent Reminders -> Bà An Turn-in & Quest 2 unlock). Duplication and overflow are mathematically prevented.
3. **UI HUD**:
   - `InventoryBar.cs`: Top-centered 5-slot hotbar with missing/collected procedural vector icon rendering, acquisition pulse spring animation, and hover tooltips.
   - `QuestHUD.cs`: Upper-left quest tracker with dynamic `(n/5)` progress counter, visual progress bar, turn-in alert, and toast notification queue.
4. **Architecture**: Clean domain separation guarantees 100% headless testability in xUnit with 0ms display server overhead.

---

## 6. Verification Method

### 6.1 Independent Automated Test Verification
Run the automated test suite from project root:
```bash
dotnet test Tests/GameProject.Tests.csproj
```
Current baseline: 192 passed in < 100 ms.

### 6.2 Recommended Verification Test Suites for Worker & Challengers
The Worker/Test Writer should implement the following headless tests in `Tests/`:
1. `InteractionSystemTests.cs`:
   - `FindNearestInteractable_OutOfRange_ReturnsNull`: Entity at (100, 0), player at (0, 0), radius 48 -> returns null.
   - `FindNearestInteractable_WithinRange_ReturnsEntity`: Entity at (30, 0), player at (0, 0), radius 48 -> returns entity.
   - `FindNearestInteractable_MultipleEntities_PicksNearest`: Entity A at (40, 0), Entity B at (25, 0) -> picks Entity B.
   - `FindNearestInteractable_EqualDistance_DeterministicTieBreak`: Entity A ("Stall_A") and Entity B ("Stall_B") at equidistant points -> deterministic by Id.
   - `InteractionBubble_BobTimerAndAlpha_SmoothInterpolation`: Verify alpha reaches 1.0 when entity active and interpolates to 0.0 when target lost.
2. `Quest1GatheringTests.cs`:
   - `Merchant_PreQuestInteraction_ReturnsGuidanceWithoutGivingItem`: Talking to merchant before talking to Bà An gives greeting, does not grant item.
   - `BaAn_InitialInteraction_StartsQuest1`: Interacting with Bà An sets `Q1_Started = true` and `QuestManager.IsQuestInProgress("Quest_Hanoi_01") == true`.
   - `Merchant_ActiveQuestInteraction_GrantsItemAndRecordsProgress`: Interacting with Bánh Phở merchant adds item to inventory, advances quest objective to 1, sets flag.
   - `Merchant_DuplicateInteraction_IsIdempotent`: Interacting 5 times with Bánh Phở merchant keeps inventory count at 1, objective count at 1, returns polite reminder dialogue.
   - `GatherAll5_BaAnTurnsIn_CompletesQuest1_EnablesQuest2`: After collecting all 5, interacting with Bà An transitions Quest 1 to Completed and unlocks Quest 2 (`CanStartQuest("Quest_Hanoi_02") == true`).
3. `UIHUDModelTests.cs`:
   - `InventoryBar_SlotModel_ReflectsCollectedItems`: Verify slots 1-5 correctly map `IsCollected` based on `Inventory.HasItem`.
   - `QuestHUD_ProgressFormatting_ShowsCorrectFraction`: Verify string format `"Thu thập nguyên liệu phở: (n/5)"` for n = 0 to 5.
   - `ToastQueue_Lifecycle_SlidesAndFades`: Verify toast timeout and removal after 2.8 seconds.
