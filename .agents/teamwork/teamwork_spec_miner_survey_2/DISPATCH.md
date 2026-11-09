## 2026-11-08T15:10:27Z
You are teamwork_spec_miner_survey_2.
Working directory: /Users/mac/Projects/game1
State folder: /Users/mac/Projects/game1/.agents/teamwork/teamwork_spec_miner_survey_2/
You MUST read ORIGINAL_REQUEST.md at: /Users/mac/Projects/game1/.agents/teamwork/ORIGINAL_REQUEST.md

Your mission:
Extract and mine the complete, precise game design specifications, story requirements, state machine, data models, and feature inventory for the 2D Vertical Slice "Du Ký Bé Mây":
1. Lore, Art Style, and Protagonists: Bé Mây, Bút Chì, Cuốn Sổ Ký Ức (Memory Journal), restoring colors to the world, visual tone (flat cartoon, vibrant colors, rounded shapes), Long Biên bridge, Hanoi Old Quarter, cyclo/bicycles, lotus flowers, phở stall.
2. World Map & Progression System (R1): 2D map, selecting unlocked locations, visual indication of color restoration per region, persistent Save/Load schema (unlocked levels, collected journal pages, stamps, quest flags, player progress).
3. Level 1 Exploration & 3 Hanoi Quests (R2):
   - Quest 1: Collect phở ingredients across market stalls (list of ingredients, interaction mechanics, inventory tracking).
   - Quest 2: Assemble the authentic bowl of phở via drag-and-drop / ordering puzzle (ingredient order, validation, completion feedback).
   - Quest 3: Rearrange street stalls to clear the path to Long Biên Bridge (grid/spatial puzzle, collision, path clearance detection).
   - Level completion: Gate unlock to next level, reward 1 Memory Journal page, 1 lotus stamp, triggering Gray Shadow (Bóng Xám) letter / narrative cutscene.
4. Dialogue, Quest & Hint Systems (R3):
   - Quest Manager state machine and event dispatching.
   - Dialogue System: JSON schema for dialogues, speaker avatars, branch/linear dialogue between Bé Mây, Bút Chì, and local Hanoi NPCs.
   - Hint System: Bút Chì interactive hints (on-demand or dynamic prompt when player is stuck).
5. Collection Album System (R4):
   - Album UI: viewing Memory Journal pages, travel stamps (lotus stamp), cultural fact cards / trivia about Hanoi.
6. Acceptance criteria mapping & testability requirements.
7. Write your comprehensive specification mining report and handoff to:
`/Users/mac/Projects/game1/.agents/teamwork/teamwork_spec_miner_survey_2/handoff.md`
Follow the Handoff Protocol (Observation, Logic Chain, Caveats, Conclusion, Verification Method).
Update your `progress.md` regularly. Send a message to parent when done.
