# Handoff Report: Milestone 4 Dialogue System & JSON Graph Parser

**Agent**: Milestone 4 Explorer 1 (`teamwork_explorer_m4_1`)  
**Mission**: Investigate and design the complete Dialogue System, JSON graph parser, Typewriter UI (`DialogueBox.cs`), NPC interactions, and headless test architecture for Milestone 4.  
**Date**: 2026-11-09T01:52:00Z  

---

## 1. Observation

Direct examination of the workspace revealed the following facts:

1. **Dialogue Content Data**:
   - Directory `Data/` and `Source/Data/` are present but empty (0 files).
   - Neither `Data/dialogues_hanoi.json` nor `Source/Data/dialogues_hanoi.json` exists yet.
   - `PROJECT.md` (§ Code Layout lines 45–49) specifies `Source/Data/dialogues_hanoi.json` and `quests_hanoi.json`.

2. **Core Dialogue Domain (`Source/Core/Dialogue/DialogueService.cs`)**:
   - Defines `DialogueBranch` (fields: `choiceText`, `nextNodeId`, `requiredQuestFlag`), `DialogueNode` (fields: `id`, `speaker`, `speakerAvatar`, `avatarPosition`, `text`, `sfxVoiceBlip`, `branches`, `nextNodeId`, `actionTrigger`), `DialogueGraph` (fields: `dialogueId`, `startNodeId`, `nodes`), and `IDialogueService` / `DialogueService`.
   - `LoadDialogueJson(string json)` only parses a single `DialogueGraph` object. It cannot deserialize a JSON array (`List<DialogueGraph>`) or an envelope (`{"dialogues": [...]}`).
   - `IDialogueService` lacks `LoadDialogueFile(string path)` (required by `PROJECT.md` line 130).
   - `DialogueBranch.RequiredQuestFlag` is defined in model but **never checked** during traversal or branch selection.
   - `DialogueService` has an `OnActionTriggered` event, but does **not publish** narrative events (`DialogueStartedEvent`, `DialogueLineAdvancedEvent`, `DialogueChoiceSelectedEvent`, `DialogueEndedEvent`) to `IEventBus` (even though these events are already defined in `Source/Core/Events/GameEvents.cs:39-42`).
   - `SelectChoice(int choiceIndex)` exists, but `SelectChoice(string choiceId)` (specified in `PROJECT.md` line 132) is missing.

3. **UI Presentation Layer (`Source/UI/DialogueBox.cs`)**:
   - `Source/UI/DialogueBox.cs` does not exist.
   - Currently, `Source/Stages/HanoiExplorationScene.cs` (lines 600–623) uses an inline static method `DrawDialogueModal(InteractionResult result)` which renders a static 820x124 rectangle with static text and no typewriter effect, no avatar portraits, and no branch choice options.
   - `GameState.Dialogue` is already defined in `Source/Core/State/GameState.cs:27`.

4. **Hanoi Exploration NPC Integration (`Source/Stages/HanoiExplorationScene.cs`)**:
   - Lines 65, 263–331: Interacting with Bà An, Merchants, or Long Biên Gate invokes `InteractionManager.TryInteract(context)`, which returns an `InteractionResult` with static `DialogueSpeaker` and `DialogueText`.
   - `HanoiExplorationScene` holds `_activeDialogue` and dismisses it on `IsInteractJustPressed` / `IsConfirmJustPressed` / `IsCancelJustPressed`.
   - If Bà An interaction completes Quest 1, line 275 pushes `GameState.MiniGamePho`.
   - If Long Biên gate interaction completes Quest 3, line 293 opens `_grayShadowLetterModal` and pushes `GameState.Cutscene`.
   - 410 existing headless tests (e.g. `InteractionAndQuest1Tests.cs:416`, `Challenger2EmpiricalTests.cs:79, 179, 517`, `ChallengerM3ProgressionStressTests.cs:326-367`) rely on `InteractionResult.DialogueSpeaker` and `InteractionResult.DialogueText`. Any change must retain 100% backward compatibility.

5. **Audio & Visual Presentation Support**:
   - `Source/Core/Ports/IAudioService.cs:23-24` defines `SfxType.DialogueBlip` and `SfxType.ChoiceSelected`.
   - `Source/Audio/RaylibAudioService.cs:109` synthesizes `SfxType.DialogueBlip` as a 587.33 Hz soft tactile tone.
   - `Source/Graphics/FontManager.cs` provides `NormalizeText(string)` (Unicode Form C canonical composition) and 762 Vietnamese glyph codepoints (`GetVietnameseCodepoints()`).
   - `Source/Graphics/VectorRenderer.cs` provides procedural drawing helpers for Bé Mây, Bút Chì, awning styles, and rounded rectangles.

6. **Current Test Status**:
   - Running `dotnet test Tests/GameProject.Tests.csproj` passes: **410 passed, 0 failed** in 205 ms.
   - `Tests/DialogueTests.cs` currently has 6 tests verifying single-graph loading, linear advance, branch index selection, action triggers, broken links, and malformed JSON resilience.

---

## 2. Logic Chain

1. **Multi-Graph JSON Architecture**:
   - Hanoi slice contains dialogues for Bà An (intro, in-progress, turn-in, post-pho), merchants (Bác Cả, Bác Tư, Chú Long, Cô Lan, Lãn Ông), Long Biên gate, and points of interest (flower bicycle, vintage cyclo, tea table). Storing each in a separate file would cause file descriptor sprawl; storing all in `Data/dialogues_hanoi.json` is optimal.
   - Therefore, `DialogueService.LoadDialogueJson` must inspect the JSON root. If it is an array or an object with a `"dialogues"` property, it loads all contained graphs into `_dialogues[id]`. If it is a single graph, it loads that graph (preserving existing `DialogueTests.cs` test cases).

2. **File Resolution Resilience**:
   - Tests execute from `Tests/bin/Debug/net10.0/`, while game runs from workspace root `/Users/mac/Projects/game1/`.
   - `DialogueService.LoadDialogueFile(string path)` must probe candidate locations:
     `path`, `Path.Combine(AppContext.BaseDirectory, path)`, `Path.Combine(AppContext.BaseDirectory, "Data", Path.GetFileName(path))`, and `Path.Combine(AppContext.BaseDirectory, "Source", "Data", Path.GetFileName(path))`.

3. **EventBus Integration & Decoupling**:
   - `IDialogueService` constructor accepts optional `IEventBus? eventBus = null`.
   - When present, `StartDialogue()` publishes `DialogueStartedEvent` and `DialogueLineAdvancedEvent`.
   - `AdvanceToNext()` publishes `DialogueLineAdvancedEvent` or `DialogueEndedEvent`.
   - `SelectChoice()` publishes `DialogueChoiceSelectedEvent` and `DialogueLineAdvancedEvent`.
   - When `eventBus == null`, existing tests without event buses continue to function cleanly.

4. **Conditional Branching & Choice Resolution**:
   - `DialogueBranch` can optionally have `choiceId`.
   - `SelectChoice(string choiceId)` matches by `ChoiceId`, `NextNodeId`, `ChoiceText`, or parsed numeric index.
   - `GetAvailableBranches(DialogueNode node, QuestFlags? flags)` filters out branches where `RequiredQuestFlag` is specified and `flags.GetFlag(RequiredQuestFlag) == false`. This enables dynamic conversational branches based on quest state.

5. **Typewriter Presentation (`DialogueBox.cs`)**:
   - Pure UI component residing in `Source/UI/DialogueBox.cs`.
   - Typewriter engine reveals characters at `TypewriterSpeed = 38.0f` chars/sec.
   - Slicing uses `FontManager.NormalizeText(text).Substring(0, count)`. Because Form C precomposes Vietnamese tone marks into single UTF-16 characters, slicing will never split a vowel from its diacritic mark.
   - Audio feedback: Every 2–3 characters revealed, plays `AudioService.PlaySfx(SfxType.DialogueBlip, 0.25f)`.
   - Fast-Forward: If user presses Enter, Space, E, or clicks while typing is in progress, text immediately fast-forwards to 100% completion (`IsTypingComplete = true`).
   - Frame Debounce / Hysteresis: The frame that fast-forwards typing is consumed, so user cannot accidentally trigger Choice 0 on the same click/keystroke.

6. **Branch Choices Navigation**:
   - When node has branches and typing completes, choices are rendered as rounded pill buttons with golden highlight (`HanoiPalette.WallYellow`) and selection arrow `▶`.
   - Keyboard: `[W]` / `[Up]` and `[S]` / `[Down]` cycle through choices. Number keys `[1]`, `[2]`, `[3]` select directly.
   - Mouse: Hovering over choice button updates `SelectedChoiceIndex`; clicking confirms it.
   - Confirm key (`[Enter]`, `[Space]`, `[E]`): triggers `SelectChoice(SelectedChoiceIndex)`.

7. **Procedural Avatar Rendering**:
   - Zero dependency on external PNGs that could go missing.
   - Procedural rendering routines for:
     - `Avatar_May`: Bé Mây with peach cheeks and straw nón lá.
     - `Avatar_ButChi`: Bút Chì with yellow body, pink eraser, and cyan sparkle.
     - `Avatar_BaAn`: Bà An with silver hair bun, warm grandmotherly eyes, and brown tunic.
     - `Avatar_ChuLong`: Chú Long / merchants with street vendor cap and apron.
     - `Avatar_BacTu`, `Avatar_CoLan`, `Avatar_ThaoMoc`: Market vendors.
     - `Avatar_BongXam`: Shadow silhouette with glowing violet/crimson eyes.
     - `Avatar_PoI`: Magnifying glass / star icon.

8. **Headless Testability Contract**:
   - `DialogueBox.Draw()` immediately returns if `!IsActive || !Raylib.IsWindowReady()`.
   - All properties (`IsActive`, `CurrentSpeaker`, `CurrentText`, `VisibleText`, `IsTypingComplete`, `SelectedChoiceIndex`, `Choices`) and methods (`Show()`, `Update()`, `FastForward()`, `SelectNextChoice()`, `ChooseOption()`, `Dismiss()`) are 100% testable headlessly with `MockInputService`.

---

## 3. Caveats

1. **Vietnamese Unicode Normalization Form C**:
   - If raw JSON text contains decomposed NFD characters (e.g. `o` + combining hook `\u0309` from certain macOS input tools), substring slicing during typewriter progression would slice between base vowel and combining accent, causing rendering glitches.
   - *Mitigation*: `DialogueBox` normalizes `CurrentText` via `FontManager.NormalizeText(text)` immediately upon `Show()`.

2. **Text Overflow in Dialogue Box**:
   - Very long paragraphs could exceed the 860px box width.
   - *Mitigation*: `DialogueBox` implements a lightweight word-wrapping helper that splits strings into lines of <= 65 characters on whitespace boundaries.

3. **Input Debounce / Hysteresis**:
   - In 60 FPS update loops, holding down `[E]` or `[Enter]` could instantly fast-forward typing and immediately trigger choice selection in consecutive frames.
   - *Mitigation*: Explicit input consumption flag `_inputConsumedThisFrame` ensures that advancing text requires a new key-press pulse (`IsConfirmJustPressed`).

4. **InteractionResult Compatibility**:
   - Existing tests call `BaAn.Interact(context)` or `merchant.Interact(context)` directly and assert on `result.DialogueSpeaker` and `result.DialogueText`.
   - *Mitigation*: Do not alter `IInteractable` or `InteractionResult`. Instead, let `HanoiExplorationScene` use `DialogueBox.Show()` to display both `DialogueNode` and `InteractionResult`, ensuring zero test breakages.

---

## 4. Conclusion & Drop-In Code Specification

To achieve full production-grade dialogue capabilities for Milestone 4, the implementer needs to apply the following 5 drop-in specifications.

### Part 1: `Data/dialogues_hanoi.json` (also placed at `Source/Data/dialogues_hanoi.json`)

```json
{
  "dialogues": [
    {
      "dialogueId": "Dialogue_BaAn_Intro",
      "startNodeId": "Node_01",
      "nodes": [
        {
          "id": "Node_01",
          "speaker": "Bà Cụ Bán Phở",
          "speakerAvatar": "Avatar_BaAn_Sad",
          "avatarPosition": "Right",
          "text": "Chào cháu, Bé Mây... Phố cổ mình giờ ảm đạm quá, các gam màu rực rỡ đều đã bị Bóng Xám mang đi mất. Bà muốn nấu một nồi phở truyền thống mà thiếu nguyên liệu.",
          "nextNodeId": "Node_02",
          "branches": []
        },
        {
          "id": "Node_02",
          "speaker": "Bé Mây",
          "speakerAvatar": "Avatar_May_Determined",
          "avatarPosition": "Left",
          "text": "Bà đừng lo ạ! Cháu và Bút Chì sẽ đi gom đủ nguyên liệu về cho bà ngay!",
          "branches": [
            {
              "choiceId": "Choice_Accept",
              "choiceText": "Cháu sẵn sàng rồi, thưa bà!",
              "nextNodeId": "Node_Accept"
            },
            {
              "choiceId": "Choice_AskRecipe",
              "choiceText": "Bà cần những nguyên liệu gì ạ?",
              "nextNodeId": "Node_AskRecipe"
            }
          ]
        },
        {
          "id": "Node_Accept",
          "speaker": "Bà Cụ Bán Phở",
          "speakerAvatar": "Avatar_BaAn_Smile",
          "avatarPosition": "Right",
          "text": "Cảm ơn cháu nhiều lắm! Hãy ghé các quầy hàng quanh Hàng Buồm và Lãn Ông nhé!",
          "nextNodeId": null,
          "actionTrigger": "Start_Quest_1"
        },
        {
          "id": "Node_AskRecipe",
          "speaker": "Bà Cụ Bán Phở",
          "speakerAvatar": "Avatar_BaAn_Smile",
          "avatarPosition": "Right",
          "text": "Bà cần bánh phở tươi, thịt bò, nước dùng hầm xương, hành lá và hoa hồi quế chi.",
          "nextNodeId": null,
          "actionTrigger": "Start_Quest_1"
        }
      ]
    },
    {
      "dialogueId": "Dialogue_BaAn_InProgress",
      "startNodeId": "Node_Prog_01",
      "nodes": [
        {
          "id": "Node_Prog_01",
          "speaker": "Bà Cụ Bán Phở",
          "speakerAvatar": "Avatar_BaAn_Smile",
          "avatarPosition": "Right",
          "text": "Cháu đã ghé đủ các quầy hàng chưa? Phố cổ Hà Nội tuy nhiều ngõ ngách nhưng bà con tiểu thương ai cũng nhiệt tình lắm!",
          "nextNodeId": null
        }
      ]
    },
    {
      "dialogueId": "Dialogue_BaAn_TurnIn",
      "startNodeId": "Node_TurnIn_01",
      "nodes": [
        {
          "id": "Node_TurnIn_01",
          "speaker": "Bà Cụ Bán Phở",
          "speakerAvatar": "Avatar_BaAn_Smile",
          "avatarPosition": "Right",
          "text": "Tuyệt vời quá bé Mây ơi! Đủ cả 5 nguyên liệu hảo hạng rồi: Bánh phở dẻo mềm, thịt bò tươi, nước dùng thơm ngát, hành hoa và thảo mộc Lãn Ông.",
          "nextNodeId": "Node_TurnIn_02"
        },
        {
          "id": "Node_TurnIn_02",
          "speaker": "Bà Cụ Bán Phở",
          "speakerAvatar": "Avatar_BaAn_Smile",
          "avatarPosition": "Right",
          "text": "Giờ bà cháu mình cùng bắt tay vào xếp bát phở hoàn chỉnh chuẩn vị Hà Nội nhé!",
          "nextNodeId": null,
          "actionTrigger": "Complete_Quest_1"
        }
      ]
    },
    {
      "dialogueId": "Dialogue_BaAn_PostPho",
      "startNodeId": "Node_Post_01",
      "nodes": [
        {
          "id": "Node_Post_01",
          "speaker": "Bà Cụ Bán Phở",
          "speakerAvatar": "Avatar_BaAn_Smile",
          "avatarPosition": "Right",
          "text": "Bát phở thơm ngon chuẩn vị Hà Nội của bà cháu mình đây rồi! Cảm ơn bé Mây nhiều lắm!",
          "nextNodeId": "Node_Post_02"
        },
        {
          "id": "Node_Post_02",
          "speaker": "Bà Cụ Bán Phở",
          "speakerAvatar": "Avatar_BaAn_Smile",
          "avatarPosition": "Right",
          "text": "Giờ cháu hãy qua chỗ cổng ra Cầu Long Biên xem sao nhé, nghe nói mấy gian hàng đang che mất lối đi rồi đấy!",
          "nextNodeId": null
        }
      ]
    },
    {
      "dialogueId": "Dialogue_Stall_BanhPho",
      "startNodeId": "Node_BP_01",
      "nodes": [
        {
          "id": "Node_BP_01",
          "speaker": "Bác Cả Hàng Buồm",
          "speakerAvatar": "Avatar_Merchant",
          "avatarPosition": "Right",
          "text": "Bánh phở tươi tráng mỏng sợi dai mềm của bác đây! Mang về cho Bà An nấu phở nhé bé Mây!",
          "nextNodeId": null,
          "actionTrigger": "Collect_BanhPho"
        }
      ]
    },
    {
      "dialogueId": "Dialogue_Stall_ThitBo",
      "startNodeId": "Node_TB_01",
      "nodes": [
        {
          "id": "Node_TB_01",
          "speaker": "Bác Tư",
          "speakerAvatar": "Avatar_BacTu",
          "avatarPosition": "Right",
          "text": "Thịt bò tươi thái mỏng bản hoa của Bác Tư đây! Nấu phở là phải có thịt tươi thế này mới ngọt nước!",
          "nextNodeId": null,
          "actionTrigger": "Collect_ThitBo"
        }
      ]
    },
    {
      "dialogueId": "Dialogue_Stall_NuocDung",
      "startNodeId": "Node_ND_01",
      "nodes": [
        {
          "id": "Node_ND_01",
          "speaker": "Chú Long Bếp Phở",
          "speakerAvatar": "Avatar_ChuLong",
          "avatarPosition": "Right",
          "text": "Nồi nước dùng ninh xương ống thơm lừng gia truyền đây! Chú đã chắt sẵn vào bình ấm, cẩn thận kẻo nóng nhé bé Mây!",
          "nextNodeId": null,
          "actionTrigger": "Collect_NuocDung"
        }
      ]
    },
    {
      "dialogueId": "Dialogue_Stall_HanhLa",
      "startNodeId": "Node_HL_01",
      "nodes": [
        {
          "id": "Node_HL_01",
          "speaker": "Cô Lan Gánh Rau",
          "speakerAvatar": "Avatar_CoLan",
          "avatarPosition": "Right",
          "text": "Hành hoa củ chẻ và rau mùi tươi mơn mởn của Cô Lan đây! Rắc lên bát phở là thơm ngát cả góc phố!",
          "nextNodeId": null,
          "actionTrigger": "Collect_HanhLa"
        }
      ]
    },
    {
      "dialogueId": "Dialogue_Stall_ThaoMoc",
      "startNodeId": "Node_TM_01",
      "nodes": [
        {
          "id": "Node_TM_01",
          "speaker": "Thầy Thuốc Lãn Ông",
          "speakerAvatar": "Avatar_ThaoMoc",
          "avatarPosition": "Right",
          "text": "Gói hoa hồi, quế chi, thảo quả sao vàng thơm lựng của tiệm thuốc bắc Lãn Ông đây! Linh hồn của nồi phở Hà Nội đấy cháu!",
          "nextNodeId": null,
          "actionTrigger": "Collect_ThaoMoc"
        }
      ]
    },
    {
      "dialogueId": "Dialogue_Gate_LongBien",
      "startNodeId": "Node_Gate_01",
      "nodes": [
        {
          "id": "Node_Gate_01",
          "speaker": "Bút Chì",
          "speakerAvatar": "Avatar_ButChi_Happy",
          "avatarPosition": "Left",
          "text": "Cổng dẫn lối lên Cầu Long Biên lịch sử bắc qua sông Hồng!",
          "branches": [
            {
              "choiceId": "Choice_Inspect",
              "choiceText": "Ngắm nhìn nhịp cầu thời gian",
              "nextNodeId": "Node_Gate_Inspect"
            },
            {
              "choiceId": "Choice_Cross",
              "choiceText": "Tiếp tục cuộc hành trình",
              "nextNodeId": "Node_Gate_Cross",
              "requiredQuestFlag": "Gate_LongBien_Unlocked"
            }
          ]
        },
        {
          "id": "Node_Gate_Inspect",
          "speaker": "Bút Chì",
          "speakerAvatar": "Avatar_ButChi_Thinking",
          "avatarPosition": "Left",
          "text": "Cầu Long Biên được xây dựng hơn một trăm năm trước, dải lụa thép chứng nhân lịch sử hào hùng của mảnh đất Thăng Long.",
          "nextNodeId": null
        },
        {
          "id": "Node_Gate_Cross",
          "speaker": "Bút Chì",
          "speakerAvatar": "Avatar_ButChi_Happy",
          "avatarPosition": "Left",
          "text": "Lối đi đã thông suốt! Chúng ta cùng đi qua Cầu Long Biên để ngắm nhìn sông Hồng và tiến vào Cố Đô Huế nào!",
          "nextNodeId": null,
          "actionTrigger": "Open_World_Map"
        }
      ]
    },
    {
      "dialogueId": "Dialogue_PoI_FlowerBicycle",
      "startNodeId": "Node_FB_01",
      "nodes": [
        {
          "id": "Node_FB_01",
          "speaker": "Bé Mây",
          "speakerAvatar": "Avatar_May_Happy",
          "avatarPosition": "Left",
          "text": "Gánh hoa sen Tây Hồ thơm ngát trên chiếc xe đạp cũ... Nét đẹp bình yên khó quên của Hà Nội!",
          "nextNodeId": null
        }
      ]
    },
    {
      "dialogueId": "Dialogue_PoI_VintageCyclo",
      "startNodeId": "Node_VC_01",
      "nodes": [
        {
          "id": "Node_VC_01",
          "speaker": "Chú Long Lái Xích Lô",
          "speakerAvatar": "Avatar_ChuLong",
          "avatarPosition": "Right",
          "text": "Chiếc xích lô này đã theo chú chở khách ngắm phố phường Hà Nội mấy chục năm qua rồi đấy bé Mây!",
          "nextNodeId": null
        }
      ]
    },
    {
      "dialogueId": "Dialogue_PoI_TeaTable",
      "startNodeId": "Node_TT_01",
      "nodes": [
        {
          "id": "Node_TT_01",
          "speaker": "Bút Chì",
          "speakerAvatar": "Avatar_ButChi_Thinking",
          "avatarPosition": "Left",
          "text": "Quán trà đá vỉa hè với chiếc bàn nhựa xanh, ấm chè tươi và đĩa kẹo lạc... nơi lưu giữ bao câu chuyện thường nhật của phố cổ.",
          "nextNodeId": null
        }
      ]
    }
  ]
}
```

---

### Part 2: Upgraded `Source/Core/Dialogue/DialogueService.cs`

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using GameProject.Core.Events;
using GameProject.Core.Models;

namespace GameProject.Core.Dialogue;

public sealed class DialogueBranch
{
    [JsonPropertyName("choiceId")]
    public string? ChoiceId { get; set; }

    [JsonPropertyName("choiceText")]
    public string ChoiceText { get; set; } = "";

    [JsonPropertyName("nextNodeId")]
    public string NextNodeId { get; set; } = "";

    [JsonPropertyName("requiredQuestFlag")]
    public string? RequiredQuestFlag { get; set; }
}

public sealed class DialogueNode
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("speaker")]
    public string Speaker { get; set; } = "";

    [JsonPropertyName("speakerAvatar")]
    public string SpeakerAvatar { get; set; } = "";

    [JsonPropertyName("avatarPosition")]
    public string AvatarPosition { get; set; } = "Left";

    [JsonPropertyName("text")]
    public string Text { get; set; } = "";

    [JsonPropertyName("sfxVoiceBlip")]
    public string? SfxVoiceBlip { get; set; }

    [JsonPropertyName("branches")]
    public List<DialogueBranch> Branches { get; set; } = new();

    [JsonPropertyName("nextNodeId")]
    public string? NextNodeId { get; set; }

    [JsonPropertyName("actionTrigger")]
    public string? ActionTrigger { get; set; }
}

public sealed class DialogueGraph
{
    [JsonPropertyName("dialogueId")]
    public string DialogueId { get; set; } = "";

    [JsonPropertyName("startNodeId")]
    public string StartNodeId { get; set; } = "";

    [JsonPropertyName("nodes")]
    public List<DialogueNode> Nodes { get; set; } = new();
}

public sealed class DialogueCollection
{
    [JsonPropertyName("dialogues")]
    public List<DialogueGraph> Dialogues { get; set; } = new();
}

public interface IDialogueService
{
    void LoadDialogueJson(string json);
    void LoadDialogueFile(string path);
    DialogueNode? StartDialogue(string dialogueId);
    DialogueNode? AdvanceToNext();
    DialogueNode? SelectChoice(int choiceIndex);
    DialogueNode? SelectChoice(string choiceId);
    void EndDialogue();
    IReadOnlyList<DialogueBranch> GetAvailableBranches(DialogueNode? node, QuestFlags? flags = null);
    DialogueNode? CurrentNode { get; }
    DialogueGraph? ActiveGraph { get; }
    bool IsDialogueActive { get; }
    event Action<string>? OnActionTriggered;
}

public sealed class DialogueService : IDialogueService
{
    private readonly Dictionary<string, DialogueGraph> _dialogues = new();
    private readonly IEventBus? _eventBus;
    private DialogueGraph? _activeGraph;
    private DialogueNode? _currentNode;

    public DialogueNode? CurrentNode => _currentNode;
    public DialogueGraph? ActiveGraph => _activeGraph;
    public bool IsDialogueActive => _currentNode != null;
    public IReadOnlyDictionary<string, DialogueGraph> LoadedGraphs => _dialogues;

    public event Action<string>? OnActionTriggered;

    public DialogueService(IEventBus? eventBus = null)
    {
        _eventBus = eventBus;
    }

    public void LoadDialogueFile(string path)
    {
        string[] candidates =
        [
            path,
            Path.Combine(AppContext.BaseDirectory, path),
            Path.Combine(AppContext.BaseDirectory, "Data", Path.GetFileName(path)),
            Path.Combine(AppContext.BaseDirectory, "Source", "Data", Path.GetFileName(path))
        ];

        foreach (string candidate in candidates)
        {
            if (File.Exists(candidate))
            {
                try
                {
                    string json = File.ReadAllText(candidate);
                    LoadDialogueJson(json);
                    return;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[DialogueService] Failed reading dialogue file '{candidate}': {ex.Message}");
                }
            }
        }
    }

    public void LoadDialogueJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return;

        try
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.ValueKind == JsonValueKind.Array)
            {
                var list = JsonSerializer.Deserialize<List<DialogueGraph>>(json, options);
                if (list != null)
                {
                    foreach (var g in list.Where(g => !string.IsNullOrEmpty(g.DialogueId)))
                    {
                        _dialogues[g.DialogueId] = g;
                    }
                }
            }
            else if (root.ValueKind == JsonValueKind.Object)
            {
                if (root.TryGetProperty("dialogues", out var dialoguesProp) && dialoguesProp.ValueKind == JsonValueKind.Array)
                {
                    var envelope = JsonSerializer.Deserialize<DialogueCollection>(json, options);
                    if (envelope?.Dialogues != null)
                    {
                        foreach (var g in envelope.Dialogues.Where(g => !string.IsNullOrEmpty(g.DialogueId)))
                        {
                            _dialogues[g.DialogueId] = g;
                        }
                    }
                }
                else
                {
                    var single = JsonSerializer.Deserialize<DialogueGraph>(json, options);
                    if (single != null && !string.IsNullOrEmpty(single.DialogueId))
                    {
                        _dialogues[single.DialogueId] = single;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DialogueService] Malformed JSON: {ex.Message}");
        }
    }

    public DialogueNode? StartDialogue(string dialogueId)
    {
        if (!_dialogues.TryGetValue(dialogueId, out var graph))
        {
            _activeGraph = null;
            _currentNode = null;
            return null;
        }

        _activeGraph = graph;
        _currentNode = graph.Nodes.FirstOrDefault(n => n.Id == graph.StartNodeId);

        if (_currentNode != null)
        {
            _eventBus?.Publish(new DialogueStartedEvent(_activeGraph.DialogueId, _currentNode.Speaker));
            _eventBus?.Publish(new DialogueLineAdvancedEvent(_activeGraph.DialogueId, _currentNode.Id, _currentNode.Speaker, _currentNode.Text));
        }

        TriggerActionIfAny(_currentNode);
        return _currentNode;
    }

    public DialogueNode? AdvanceToNext()
    {
        if (_activeGraph == null || _currentNode == null) return null;

        if (_currentNode.Branches != null && _currentNode.Branches.Count > 0)
        {
            return _currentNode;
        }

        if (string.IsNullOrEmpty(_currentNode.NextNodeId))
        {
            EndDialogue();
            return null;
        }

        var next = _activeGraph.Nodes.FirstOrDefault(n => n.Id == _currentNode.NextNodeId);
        _currentNode = next;

        if (_currentNode != null)
        {
            _eventBus?.Publish(new DialogueLineAdvancedEvent(_activeGraph.DialogueId, _currentNode.Id, _currentNode.Speaker, _currentNode.Text));
            TriggerActionIfAny(_currentNode);
        }
        else
        {
            EndDialogue();
        }

        return _currentNode;
    }

    public DialogueNode? SelectChoice(int choiceIndex)
    {
        if (_activeGraph == null || _currentNode == null) return null;
        if (_currentNode.Branches == null || choiceIndex < 0 || choiceIndex >= _currentNode.Branches.Count)
            return null;

        var choice = _currentNode.Branches[choiceIndex];
        _eventBus?.Publish(new DialogueChoiceSelectedEvent(_activeGraph.DialogueId, choice.ChoiceId ?? choice.NextNodeId, choice.NextNodeId));

        _currentNode = _activeGraph.Nodes.FirstOrDefault(n => n.Id == choice.NextNodeId);
        if (_currentNode != null)
        {
            _eventBus?.Publish(new DialogueLineAdvancedEvent(_activeGraph.DialogueId, _currentNode.Id, _currentNode.Speaker, _currentNode.Text));
            TriggerActionIfAny(_currentNode);
        }
        else
        {
            EndDialogue();
        }

        return _currentNode;
    }

    public DialogueNode? SelectChoice(string choiceId)
    {
        if (_activeGraph == null || _currentNode == null || _currentNode.Branches == null) return null;

        for (int i = 0; i < _currentNode.Branches.Count; i++)
        {
            var b = _currentNode.Branches[i];
            if (string.Equals(b.ChoiceId, choiceId, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(b.NextNodeId, choiceId, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(b.ChoiceText, choiceId, StringComparison.OrdinalIgnoreCase))
            {
                return SelectChoice(i);
            }
        }

        if (int.TryParse(choiceId, out int idx))
        {
            return SelectChoice(idx);
        }

        return null;
    }

    public void EndDialogue()
    {
        if (_activeGraph != null)
        {
            _eventBus?.Publish(new DialogueEndedEvent(_activeGraph.DialogueId));
        }
        _currentNode = null;
        _activeGraph = null;
    }

    public IReadOnlyList<DialogueBranch> GetAvailableBranches(DialogueNode? node, QuestFlags? flags = null)
    {
        if (node?.Branches == null) return Array.Empty<DialogueBranch>();
        if (flags == null) return node.Branches;

        return node.Branches.Where(b =>
            string.IsNullOrEmpty(b.RequiredQuestFlag) || flags.GetFlag(b.RequiredQuestFlag)
        ).ToList();
    }

    private void TriggerActionIfAny(DialogueNode? node)
    {
        if (node != null && !string.IsNullOrEmpty(node.ActionTrigger))
        {
            OnActionTriggered?.Invoke(node.ActionTrigger);
        }
    }
}
```

---

### Part 3: `Source/UI/DialogueBox.cs`

```csharp
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using GameProject.Core.Dialogue;
using GameProject.Core.Ports;
using GameProject.Graphics;
using Raylib_cs;

namespace GameProject.UI;

/// <summary>
/// Production Typewriter Dialogue UI component with procedural cartoon portraits,
/// Form C Vietnamese Unicode rendering, choice branch buttons, and keyboard/mouse navigation.
/// Fully testable in headless test runners.
/// </summary>
public sealed class DialogueBox
{
    public const float TypewriterSpeed = 38.0f; // characters per second

    private readonly IAudioService? _audioService;
    private DialogueNode? _currentNode;
    private IDialogueService? _dialogueService;
    private List<DialogueBranch> _currentBranches = new();

    public bool IsActive { get; private set; }
    public string CurrentSpeaker { get; private set; } = "";
    public string CurrentText { get; private set; } = "";
    public string CurrentAvatar { get; private set; } = "";
    public string AvatarPosition { get; private set; } = "Left";
    public float TypewriterProgress { get; private set; }
    public int SelectedChoiceIndex { get; set; }
    public bool RequestClose { get; private set; }

    public int VisibleCharCount => Math.Clamp((int)(TypewriterProgress * TypewriterSpeed), 0, CurrentText.Length);
    public bool IsTypingComplete => VisibleCharCount >= CurrentText.Length;
    public string VisibleText => CurrentText.Length == 0 ? "" : CurrentText.Substring(0, VisibleCharCount);
    public IReadOnlyList<DialogueBranch> CurrentBranches => _currentBranches;

    private int _lastSoundCharCount;
    private float _timer;

    public DialogueBox(IAudioService? audioService = null)
    {
        _audioService = audioService;
    }

    public void Show(DialogueNode node, IDialogueService? service = null, IReadOnlyList<DialogueBranch>? availableBranches = null)
    {
        ArgumentNullException.ThrowIfNull(node);
        _dialogueService = service;
        _currentNode = node;

        CurrentSpeaker = node.Speaker ?? "";
        CurrentText = FontManager.NormalizeText(node.Text ?? "");
        CurrentAvatar = node.SpeakerAvatar ?? "";
        AvatarPosition = string.IsNullOrEmpty(node.AvatarPosition) ? "Left" : node.AvatarPosition;

        _currentBranches = availableBranches != null
            ? new List<DialogueBranch>(availableBranches)
            : new List<DialogueBranch>(node.Branches ?? Enumerable.Empty<DialogueBranch>());

        SelectedChoiceIndex = 0;
        TypewriterProgress = 0f;
        _lastSoundCharCount = 0;
        _timer = 0f;
        IsActive = true;
        RequestClose = false;
    }

    public void Show(string speaker, string text, string avatar = "Avatar_May", string avatarPosition = "Left")
    {
        _dialogueService = null;
        _currentNode = null;
        _currentBranches.Clear();

        CurrentSpeaker = speaker ?? "";
        CurrentText = FontManager.NormalizeText(text ?? "");
        CurrentAvatar = avatar;
        AvatarPosition = avatarPosition;

        SelectedChoiceIndex = 0;
        TypewriterProgress = 0f;
        _lastSoundCharCount = 0;
        _timer = 0f;
        IsActive = true;
        RequestClose = false;
    }

    public void FastForward()
    {
        if (CurrentText.Length > 0)
        {
            TypewriterProgress = (CurrentText.Length + 1) / TypewriterSpeed;
        }
    }

    public void Update(float dt, IInputService? input = null)
    {
        if (!IsActive) return;

        _timer += dt;

        // 1. Advance typewriter animation
        if (!IsTypingComplete)
        {
            TypewriterProgress += dt;
            int currentChars = VisibleCharCount;
            if (currentChars - _lastSoundCharCount >= 3)
            {
                _lastSoundCharCount = currentChars;
                _audioService?.PlaySfx(SfxType.DialogueBlip, 0.25f);
            }
        }

        // 2. Gather user input
        bool confirmPressed = input?.IsConfirmJustPressed == true || input?.IsInteractJustPressed == true;
        bool cancelPressed = input?.IsCancelJustPressed == true;
        Vector2 pointerPos = input?.PointerPosition ?? Vector2.Zero;
        bool pointerClicked = input?.IsPointerJustPressed == true;

        if (Raylib.IsWindowReady())
        {
            confirmPressed = confirmPressed || Raylib.IsKeyPressed(KeyboardKey.Enter) || Raylib.IsKeyPressed(KeyboardKey.Space) || Raylib.IsKeyPressed(KeyboardKey.E);
            cancelPressed = cancelPressed || Raylib.IsKeyPressed(KeyboardKey.Escape);
            pointerPos = Raylib.GetMousePosition();
            pointerClicked = pointerClicked || Raylib.IsMouseButtonPressed(MouseButton.Left);

            if (_currentBranches.Count > 0 && IsTypingComplete)
            {
                if (Raylib.IsKeyPressed(KeyboardKey.W) || Raylib.IsKeyPressed(KeyboardKey.Up)) SelectPreviousChoice();
                if (Raylib.IsKeyPressed(KeyboardKey.S) || Raylib.IsKeyPressed(KeyboardKey.Down)) SelectNextChoice();

                if (Raylib.IsKeyPressed(KeyboardKey.One) && _currentBranches.Count >= 1) ChooseOption(0);
                if (Raylib.IsKeyPressed(KeyboardKey.Two) && _currentBranches.Count >= 2) ChooseOption(1);
                if (Raylib.IsKeyPressed(KeyboardKey.Three) && _currentBranches.Count >= 3) ChooseOption(2);
            }
        }

        // 3. Fast-forward typing or advance
        if (!IsTypingComplete)
        {
            if (confirmPressed || pointerClicked)
            {
                FastForward();
            }
            return;
        }

        // 4. Typing is complete: Handle branch selection or advance
        if (_currentBranches.Count > 0)
        {
            // Mouse hover & click detection on choices
            float boxW = 880f;
            float boxX = (1280f - boxW) * 0.5f;
            float choiceStartY = 540f - (_currentBranches.Count * 44f);

            for (int i = 0; i < _currentBranches.Count; i++)
            {
                Rectangle choiceRec = new(boxX + 160f, choiceStartY + i * 42f, boxW - 200f, 36f);
                if (Raylib.CheckCollisionPointRec(pointerPos, choiceRec))
                {
                    SelectedChoiceIndex = i;
                    if (pointerClicked)
                    {
                        ChooseOption(i);
                        return;
                    }
                }
            }

            if (confirmPressed)
            {
                ChooseOption(SelectedChoiceIndex);
            }
        }
        else
        {
            if (confirmPressed || pointerClicked)
            {
                AdvanceToNext();
            }
        }
    }

    public void SelectNextChoice()
    {
        if (_currentBranches.Count == 0) return;
        SelectedChoiceIndex = (SelectedChoiceIndex + 1) % _currentBranches.Count;
        _audioService?.PlaySfx(SfxType.ButtonClick, 0.2f);
    }

    public void SelectPreviousChoice()
    {
        if (_currentBranches.Count == 0) return;
        SelectedChoiceIndex = (SelectedChoiceIndex - 1 + _currentBranches.Count) % _currentBranches.Count;
        _audioService?.PlaySfx(SfxType.ButtonClick, 0.2f);
    }

    public void ChooseOption(int index)
    {
        if (index < 0 || index >= _currentBranches.Count) return;

        _audioService?.PlaySfx(SfxType.ChoiceSelected, 0.5f);
        if (_dialogueService != null)
        {
            var nextNode = _dialogueService.SelectChoice(index);
            if (nextNode != null)
            {
                Show(nextNode, _dialogueService);
            }
            else
            {
                Dismiss();
            }
        }
        else
        {
            Dismiss();
        }
    }

    public void AdvanceToNext()
    {
        if (_dialogueService != null)
        {
            var nextNode = _dialogueService.AdvanceToNext();
            if (nextNode != null)
            {
                Show(nextNode, _dialogueService);
            }
            else
            {
                Dismiss();
            }
        }
        else
        {
            Dismiss();
        }
    }

    public void Dismiss()
    {
        IsActive = false;
        RequestClose = true;
        _dialogueService?.EndDialogue();
    }

    public void Draw()
    {
        if (!IsActive || !Raylib.IsWindowReady()) return;

        float boxW = 880f;
        float boxH = 140f;
        float boxX = (1280f - boxW) * 0.5f;
        float boxY = 550f;

        // 1. Draw Choices above box if present and text complete
        if (_currentBranches.Count > 0 && IsTypingComplete)
        {
            float choiceStartY = boxY - (_currentBranches.Count * 44f) - 10f;
            for (int i = 0; i < _currentBranches.Count; i++)
            {
                bool isSelected = (i == SelectedChoiceIndex);
                Rectangle choiceRec = new(boxX + 160f, choiceStartY + i * 42f, boxW - 200f, 36f);

                Color fill = isSelected ? new Color(255, 238, 170, 250) : new Color(252, 250, 242, 240);
                Color border = isSelected ? HanoiPalette.CartoonOutline : new Color(170, 150, 130, 200);
                float borderThick = isSelected ? 2.5f : 1.5f;

                VectorRenderer.DrawRoundedRect(choiceRec, 0.35f, fill, border, borderThick, withShadow: isSelected);

                string prefix = isSelected ? $"▶ [{i + 1}] " : $"   [{i + 1}] ";
                string label = prefix + _currentBranches[i].ChoiceText;
                FontManager.Instance.DrawText(label, new Vector2(choiceRec.X + 12f, choiceRec.Y + 8f), 14f, HanoiPalette.CartoonOutline);
            }
        }

        // 2. Main Box Background
        Rectangle dialogRec = new(boxX, boxY, boxW, boxH);
        VectorRenderer.DrawRoundedRect(dialogRec, 0.18f, new Color(254, 252, 246, 252), HanoiPalette.CartoonOutline, 2.5f);

        // Inner decorative border
        Rectangle innerRec = new(boxX + 6f, boxY + 6f, boxW - 12f, boxH - 12f);
        Raylib.DrawRectangleRoundedLinesEx(innerRec, 0.16f, 6, 1.0f, Raylib.ColorAlpha(HanoiPalette.TerracottaRoof, 0.25f));

        // 3. Avatar Portrait
        float avatarCenterX = (AvatarPosition == "Right") ? (boxX + boxW - 65f) : (boxX + 65f);
        float avatarCenterY = boxY + boxH * 0.5f;
        DrawAvatar(CurrentAvatar, new Vector2(avatarCenterX, avatarCenterY), 42f);

        // 4. Speaker Name Badge
        if (!string.IsNullOrEmpty(CurrentSpeaker))
        {
            Vector2 nameSz = FontManager.Instance.MeasureText(CurrentSpeaker, 15f);
            float badgeX = (AvatarPosition == "Right") ? (boxX + 30f) : (boxX + 130f);
            Rectangle nameBadge = new(badgeX, boxY - 15f, nameSz.X + 28f, 30f);
            VectorRenderer.DrawRoundedRect(nameBadge, 0.35f, HanoiPalette.WallYellow, HanoiPalette.CartoonOutline, 2.0f, withShadow: false);
            FontManager.Instance.DrawText(CurrentSpeaker, new Vector2(nameBadge.X + 14f, nameBadge.Y + 6f), 15f, HanoiPalette.CartoonOutline);
        }

        // 5. Dialogue Typewriter Text
        float textX = (AvatarPosition == "Right") ? (boxX + 30f) : (boxX + 135f);
        float textW = boxW - 180f;
        DrawWrappedText(VisibleText, new Vector2(textX, boxY + 28f), textW, 16f, new Color(32, 28, 26, 255));

        // 6. Navigation Prompt in bottom-right corner
        string promptText = (_currentBranches.Count > 0 && IsTypingComplete)
            ? "[▲/▼ / 1-3] Chọn  [Enter] Xác nhận"
            : (IsTypingComplete ? "[Phím Trắng / Enter] Tiếp tục" : "[Phím Trắng] Xem nhanh");

        Vector2 promptSz = FontManager.Instance.MeasureText(promptText, 12f);
        FontManager.Instance.DrawText(promptText, new Vector2(boxX + boxW - promptSz.X - 22f, boxY + boxH - promptSz.Y - 10f), 12f, new Color(135, 115, 100, 255));
    }

    private static void DrawWrappedText(string text, Vector2 pos, float maxWidth, float fontSize, Color color)
    {
        if (string.IsNullOrEmpty(text)) return;

        string[] words = text.Split(' ');
        var line = new StringBuilder();
        float currentY = pos.Y;

        for (int i = 0; i < words.Length; i++)
        {
            string testLine = line.Length == 0 ? words[i] : line + " " + words[i];
            Vector2 size = FontManager.Instance.MeasureText(testLine, fontSize);
            if (size.X > maxWidth && line.Length > 0)
            {
                FontManager.Instance.DrawText(line.ToString(), new Vector2(pos.X, currentY), fontSize, color);
                line.Clear();
                line.Append(words[i]);
                currentY += fontSize + 6f;
            }
            else
            {
                line.Clear();
                line.Append(testLine);
            }
        }

        if (line.Length > 0)
        {
            FontManager.Instance.DrawText(line.ToString(), new Vector2(pos.X, currentY), fontSize, color);
        }
    }

    private static void DrawAvatar(string avatarKey, Vector2 center, float radius)
    {
        // Frame
        Raylib.DrawCircleV(center, radius + 3f, HanoiPalette.CartoonOutline);
        Raylib.DrawCircleV(center, radius, new Color(252, 245, 230, 255));

        switch (avatarKey)
        {
            case "Avatar_May":
            case "Avatar_May_Determined":
            case "Avatar_May_Happy":
                VectorRenderer.DrawBeMay(new Vector2(center.X, center.Y + 28f), FacingDirection.Down, 0f, false, scale: 0.95f);
                break;

            case "Avatar_ButChi":
            case "Avatar_ButChi_Thinking":
            case "Avatar_ButChi_Happy":
                VectorRenderer.DrawButChi(new Vector2(center.X, center.Y - 2f), 0f, scale: 1.2f);
                break;

            case "Avatar_BaAn":
            case "Avatar_BaAn_Sad":
            case "Avatar_BaAn_Smile":
                // Silver Hair Bun
                Raylib.DrawCircleV(new Vector2(center.X, center.Y - 16f), 12f, new Color(195, 195, 195, 255));
                Raylib.DrawCircleV(center, 18f, HanoiPalette.SkinPeach);
                Raylib.DrawCircleLines((int)center.X, (int)center.Y, 18f, HanoiPalette.CartoonOutline);
                // Eyes & Smile
                Raylib.DrawCircleV(new Vector2(center.X - 6f, center.Y - 2f), 2f, HanoiPalette.CartoonOutline);
                Raylib.DrawCircleV(new Vector2(center.X + 6f, center.Y - 2f), 2f, HanoiPalette.CartoonOutline);
                Raylib.DrawCircleSector(new Vector2(center.X, center.Y + 4f), 5f, 0f, 180f, 10, HanoiPalette.CartoonOutline);
                break;

            case "Avatar_ChuLong":
            case "Avatar_BacTu":
            case "Avatar_Merchant":
                // Friendly vendor with flat cap
                Raylib.DrawCircleV(center, 18f, HanoiPalette.SkinPeach);
                Rectangle cap = new(center.X - 18f, center.Y - 20f, 36f, 10f);
                Raylib.DrawRectangleRounded(cap, 0.4f, 4, new Color(85, 107, 47, 255));
                Raylib.DrawCircleV(new Vector2(center.X - 5f, center.Y - 2f), 2f, HanoiPalette.CartoonOutline);
                Raylib.DrawCircleV(new Vector2(center.X + 5f, center.Y - 2f), 2f, HanoiPalette.CartoonOutline);
                break;

            default:
                // Default emblem / magnifying glass
                Raylib.DrawCircleV(center, 14f, HanoiPalette.WallYellow);
                Raylib.DrawCircleLines((int)center.X, (int)center.Y, 14f, HanoiPalette.CartoonOutline);
                FontManager.Instance.DrawText("★", new Vector2(center.X - 6f, center.Y - 8f), 16f, HanoiPalette.CartoonOutline);
                break;
        }
    }
}
```

---

### Part 4: `HanoiExplorationScene.cs` Integration

1. Add properties:
   ```csharp
   public IDialogueService DialogueService { get; }
   public DialogueBox DialogueBox { get; }
   ```
2. In Constructor:
   ```csharp
   DialogueService = dialogueService ?? new DialogueService(EventBus);
   DialogueBox = new DialogueBox(AudioService);
   ```
3. In `Enter()`:
   ```csharp
   DialogueService.LoadDialogueFile("Data/dialogues_hanoi.json");
   DialogueService.OnActionTriggered += HandleDialogueAction;
   ```
4. In `Update(float deltaTime)`:
   - If `DialogueBox.IsActive`:
     ```csharp
     DialogueBox.Update(deltaTime, InputService);
     if (DialogueBox.RequestClose)
     {
         if (StateManager.CurrentState == GameState.Dialogue)
         {
             StateManager.PopState();
         }
     }
     return;
     ```
   - When player presses `[E]` near an interactable:
     - Check if corresponding dialogue exists in `DialogueService` (e.g. `"Dialogue_BaAn_Intro"`); if so:
       ```csharp
       var node = DialogueService.StartDialogue(dialogueId);
       if (node != null)
       {
           StateManager.PushState(GameState.Dialogue);
           DialogueBox.Show(node, DialogueService, DialogueService.GetAvailableBranches(node, QuestFlags));
           return;
       }
       ```
     - Fallback: `DialogueBox.Show(result.DialogueSpeaker, result.DialogueText)` for quick interactions.
5. In `Draw()`:
   - Replace old inline `DrawDialogueModal(_activeDialogue)` with:
     ```csharp
     if (DialogueBox.IsActive)
     {
         DialogueBox.Draw();
     }
     ```

---

### Part 5: New Test Coverage for `Tests/DialogueTests.cs`

Add the following headless verification test cases:

```csharp
[Fact]
public void LoadDialogueJson_MultiGraphArray_LoadsAllGraphs()
{
    const string multiJson = """
    [
      { "dialogueId": "G1", "startNodeId": "N1", "nodes": [{ "id": "N1", "speaker": "A", "text": "Hi" }] },
      { "dialogueId": "G2", "startNodeId": "N2", "nodes": [{ "id": "N2", "speaker": "B", "text": "Bye" }] }
    ]
    """;
    var service = new DialogueService();
    service.LoadDialogueJson(multiJson);

    Assert.NotNull(service.StartDialogue("G1"));
    Assert.NotNull(service.StartDialogue("G2"));
}

[Fact]
public void DialogueService_PublishesEventsToEventBus()
{
    var bus = new EventBus();
    var service = new DialogueService(bus);
    service.LoadDialogueJson(SampleDialogueJson);

    bool started = false;
    bool advanced = false;
    bus.Subscribe<DialogueStartedEvent>(_ => started = true);
    bus.Subscribe<DialogueLineAdvancedEvent>(_ => advanced = true);

    service.StartDialogue("Dialogue_BaAn_Intro");
    Assert.True(started);
    Assert.True(advanced);
}

[Fact]
public void DialogueBranch_FiltersByRequiredQuestFlag()
{
    var flags = new QuestFlags { Gate_LongBien_Unlocked = false };
    var node = new DialogueNode
    {
        Id = "TestNode",
        Branches = new List<DialogueBranch>
        {
            new() { ChoiceText = "Always open", NextNodeId = "N1" },
            new() { ChoiceText = "Only when gate open", NextNodeId = "N2", RequiredQuestFlag = "Gate_LongBien_Unlocked" }
        }
    };
    var service = new DialogueService();
    var available = service.GetAvailableBranches(node, flags);
    Assert.Single(available);
    Assert.Equal("Always open", available[0].ChoiceText);

    flags.Gate_LongBien_Unlocked = true;
    var unlocked = service.GetAvailableBranches(node, flags);
    Assert.Equal(2, unlocked.Count);
}

[Fact]
public void DialogueBox_TypewriterFastForward_AndHeadlessExecution()
{
    var box = new DialogueBox();
    box.Show("Bé Mây", "Chào phố cổ Hà Nội!");

    Assert.True(box.IsActive);
    Assert.False(box.IsTypingComplete);

    box.FastForward();
    Assert.True(box.IsTypingComplete);
    Assert.Equal("Chào phố cổ Hà Nội!", box.VisibleText);
}
```

---

## 5. Verification Method

To independently verify the implementation:

1. **Verify Compilation and Existing Test Suite**:
   ```bash
   dotnet test Tests/GameProject.Tests.csproj
   ```
   *Expected Result*: 410 existing tests pass with 0 failures in < 250 ms.

2. **Verify Dialogue Test Coverage**:
   ```bash
   dotnet test Tests/GameProject.Tests.csproj --filter FullyQualifiedName~DialogueTests
   ```
   *Expected Result*: All existing and new dialogue tests pass headlessly with 0 failures in < 50 ms.

3. **Verify Vietnamese UTF-8 Integrity**:
   ```bash
   dotnet test Tests/GameProject.Tests.csproj --filter FullyQualifiedName~Utf8EncodingTests
   ```
   *Expected Result*: Verifies round-trip fidelity and Form C normalization.

4. **Invalidation Conditions**:
   - Any test failure in existing 410 tests.
   - Any Raylib window initialization attempted during headless test runs (`Raylib.IsWindowReady()` check omitted).
   - Any Vietnamese diacritic string corruption during typewriter animation.
