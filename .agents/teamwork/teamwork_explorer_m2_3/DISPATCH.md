## 2026-11-08T16:26:24Z
You are teamwork_explorer_m2_3.
Working directory: /Users/mac/Projects/game1
State folder: /Users/mac/Projects/game1/.agents/teamwork/teamwork_explorer_m2_3/
MANDATORY: Read ORIGINAL_REQUEST.md at /Users/mac/Projects/game1/.agents/teamwork/ORIGINAL_REQUEST.md and PROJECT.md at /Users/mac/Projects/game1/PROJECT.md before doing anything else.

Milestone 2 Scope: Hanoi Exploration & Quest 1 (Phở Gathering) — Focus: Interaction Loop, NPCs, Quest 1 Mechanics & HUD UI.
Your focus:
1. Deeply analyze the Interaction Prompt Bubble (Feature 7):
   - Proximity-based detection (radius 45-50 px), floating animated bubble above interactable entity: [E] Tương tác.
   - Priority selection if multiple interactables are within range (nearest entity).
2. Deeply analyze Market Merchants & Quest 1 Ingredients Gathering Loop (Feature 8):
   - 5 Stalls/NPCs placed across Hanoi zones:
     1. Bà An (Bà Cụ Bán Phở) — triggers Quest 1, receives all 5 ingredients.
     2. Quầy Bánh Phở & Mì Sợi — delivers Item_BanhPho.
     3. Quầy Thịt Bò Bác Tư — delivers Item_ThitBo.
     4. Bếp Nước Dùng Nóng — delivers Item_NuocDung.
     5. Gánh Rau Tươi Cô Lan — delivers Item_HanhLa.
     6. Hiệu Thuốc Bắc Lãn Ông — delivers Item_ThaoMoc.
   - Interaction logic: approaching, pressing [E], dialogue snippet, chime sound, adding item to Core Inventory, recording objective in QuestManager, HUD popup notification.
   - Idempotency: interacting again with a vendor who already gave an item gives a polite reminder dialogue without duplicating items or overflowing counts.
3. Deeply analyze UI HUD in Source/UI/:
   - InventoryBar.cs (Feature 9): Top screen bar displaying 5 ingredient slots (grayed out outline when missing, colorful icon when acquired).
   - QuestHUD.cs: Upper-left quest tracker showing active objective: Thu thập nguyên liệu phở: (n/5).
4. Provide concrete class signatures, interaction contracts, and implementation strategy for the Worker. DO NOT implement code yourself (you are read-only).
5. Write your handoff report to:
/Users/mac/Projects/game1/.agents/teamwork/teamwork_explorer_m2_3/handoff.md
Follow Handoff Protocol. Update progress.md regularly. Notify parent when done.
