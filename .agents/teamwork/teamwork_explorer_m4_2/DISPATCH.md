# Dispatch: Milestone 4 Explorer 2 (Bút Chì Hint Engine & Companion Guide)

## Objective
Investigate and design the complete Hint Engine and Bút Chì Companion System for Milestone 4:
1. Hint rules engine mapping current quest state and environmental context to actionable hints:
   - Quest 1 (Phở Gathering): ingredients remaining, which stall to visit.
   - Quest 2 (Phở Assembly): cooking order guidance (Bánh phở -> Thịt bò -> Hành lá -> Nước dùng -> Gia vị).
   - Quest 3 (Stall Grid BFS): sliding puzzle hints, path orientation toward Long Biên Bridge.
   - Exploration / idle hints: idle detection (>45 seconds without quest progress).
2. On-demand hint trigger: `[H]` key or dedicated UI button.
3. Bút Chì Companion Visual Representation: animated speech bubble, companion character portrait/sprite, gentle bobbing animation, and non-intrusive sound cue.
4. Clean Architecture separation: domain hint rules in `Source/Core/` with zero Raylib dependency; UI bubble and renderer in `Source/UI/`.
5. Automated test strategy covering all quest stages, edge cases, and idle timers.

## Inputs
- `/Users/mac/Projects/game1/.agents/teamwork/ORIGINAL_REQUEST.md` (Verbatim user request, especially § R3)
- `/Users/mac/Projects/game1/PROJECT.md` (Architecture, Feature Inventory items 25, 26, Interface Contracts)

## Output Requirements
Produce a comprehensive 5-component handoff report at `/Users/mac/Projects/game1/.agents/teamwork/teamwork_explorer_m4_2/handoff.md`:
- Observation (existing codebase files, current hint service)
- Logic Chain (rule engine design, state mapping, idle detection)
- Caveats (avoiding hint spam, debounce, headless testability)
- Conclusion & Drop-In Code Specification
- Verification Method (commands to compile and test)
Send a message when done.


## 2026-11-09T01:46:04Z
You are Milestone 4 Explorer 2 (Bút Chì Hint Engine & Companion Guide).
Working directory: /Users/mac/Projects/game1
State folder: /Users/mac/Projects/game1/.agents/teamwork/teamwork_explorer_m4_2/

Read:
- /Users/mac/Projects/game1/.agents/teamwork/teamwork_explorer_m4_2/DISPATCH.md
- /Users/mac/Projects/game1/.agents/teamwork/ORIGINAL_REQUEST.md
- /Users/mac/Projects/game1/PROJECT.md

Investigate the hint engine and Bút Chì companion in the codebase:
- `Source/Core/` hint services and quest rules
- `Source/UI/` Bút Chì guide UI, speech bubble, and idle timer (>45s)
- Design complete drop-in enhancements or fixes needed for M4.

Write your 5-component handoff report to `/Users/mac/Projects/game1/.agents/teamwork/teamwork_explorer_m4_2/handoff.md` and send a message when done.
