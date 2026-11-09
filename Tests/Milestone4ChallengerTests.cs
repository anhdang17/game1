using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Text;
using System.Text.Json;
using GameProject.Core.Dialogue;
using GameProject.Core.Events;
using GameProject.Core.Hints;
using GameProject.Core.Models;
using GameProject.Core.Ports;
using GameProject.Core.Quests;
using GameProject.Core.State;
using GameProject.Stages;
using GameProject.UI;
using Xunit;

namespace GameProject.Tests;

public class Milestone4ChallengerTests
{
    // =========================================================================
    // SECTION 1: EMPIRICAL DISCOVERY / BUG CONFIRMATION TESTS
    // =========================================================================

    [Fact]
    public void BugCheck_ExplorationScene_DoesNotUpdateDialogueBoxTypewriter()
    {
        // ARRANGE: Set up HanoiExplorationScene headlessly
        var bus = new EventBus();
        var qm = new QuestManager(bus);
        HanoiQuests.RegisterAll(qm);

        var saveData = SaveData.CreateDefault();
        var input = new MockInputService();
        var sm = new GameStateManager(bus, GameState.Exploration);

        var scene = new HanoiExplorationScene(
            saveData: saveData,
            questManager: qm,
            stateManager: sm,
            inputService: input,
            audioService: new NullAudioService(),
            eventBus: bus
        );

        scene.Enter();

        // 1. Position player near Bà An and trigger dialogue via interaction [E]
        scene.Kinematics.Position = scene.BaAn.Position + new Vector2(0, 25f);
        input.IsInteractJustPressed = true;
        scene.Update(0.016f);
        input.IsInteractJustPressed = false;

        // Dialogue should now be open
        Assert.True(scene.DialogueBox.IsActive, "DialogueBox should be active after interacting with Bà An");
        Assert.Equal("Bà An", scene.DialogueBox.CurrentSpeaker);
        Assert.NotEmpty(scene.DialogueBox.CurrentText);

        // 2. Advance time by 1.0 second without pressing dismiss keys
        scene.Update(1.0f);

        // EMPIRICAL CHECK:
        // In 1.0s at 38 chars/sec, TypewriterProgress should be >= 1.0 and VisibleCharCount should be >= 38.
        // If DialogueBox.Update() is never called in HanoiExplorationScene.Update(),
        // TypewriterProgress will remain 0 and VisibleCharCount will remain 0 (blank dialogue!).
        Assert.True(scene.DialogueBox.TypewriterProgress > 0f,
            $"Expected DialogueBox.TypewriterProgress > 0 after 1.0s, but was {scene.DialogueBox.TypewriterProgress}. DialogueBox.Update is not being called by HanoiExplorationScene!");
        Assert.True(scene.DialogueBox.VisibleCharCount > 0,
            $"Expected VisibleCharCount > 0, but was {scene.DialogueBox.VisibleCharCount}. Typewriter text is empty on screen!");
    }

    [Fact]
    public void BugCheck_DialogueBox_ChooseOption_WithFilteredBranches_SelectsWrongBranchIndex()
    {
        // ARRANGE: Node with 2 branches: Branch 0 requires a flag (not unlocked), Branch 1 is always available
        var service = new DialogueService();
        var node = new DialogueNode
        {
            Id = "Node_Question",
            Speaker = "NPC",
            Text = "Chọn hướng đi",
            Branches = new List<DialogueBranch>
            {
                new() { ChoiceId = "Choice_Secret", ChoiceText = "Đường bí mật", NextNodeId = "Node_Secret", RequiredQuestFlag = "Secret_Unlocked" },
                new() { ChoiceId = "Choice_Normal", ChoiceText = "Đường bình thường", NextNodeId = "Node_Normal" }
            }
        };

        var graph = new DialogueGraph
        {
            DialogueId = "Graph_BranchTest",
            StartNodeId = "Node_Question",
            Nodes = new List<DialogueNode>
            {
                node,
                new() { Id = "Node_Secret", Speaker = "NPC", Text = "Vào phòng bí mật!" },
                new() { Id = "Node_Normal", Speaker = "NPC", Text = "Đi đường bình thường." }
            }
        };

        service.LoadDialogueJson(JsonSerializer.Serialize(graph));
        service.StartDialogue("Graph_BranchTest");

        // Filter branches: Secret_Unlocked is false, so only "Choice_Normal" is available
        var flags = new QuestFlags(); // Secret_Unlocked not set
        var availableBranches = service.GetAvailableBranches(service.CurrentNode, flags);

        Assert.Single(availableBranches);
        Assert.Equal("Choice_Normal", availableBranches[0].ChoiceId);

        // Pass available branches to DialogueBox
        var box = new DialogueBox();
        box.Show(service.CurrentNode!, service, availableBranches);
        box.FastForward();

        // The UI presents 1 option: Index 0 on screen is "Đường bình thường" (Choice_Normal)
        // User selects option 0:
        box.ChooseOption(0);

        // EMPIRICAL CHECK:
        // DialogueService should transition to Node_Normal ("Choice_Normal").
        // If DialogueBox calls service.SelectChoice(index) with raw UI index 0,
        // DialogueService accesses node.Branches[0] which is "Choice_Secret"!
        Assert.Equal("Node_Normal", service.CurrentNode?.Id);
    }

    // =========================================================================
    // SECTION 2: MULTI-GRAPH JSON INTEGRITY & STRESS TESTS
    // =========================================================================

    [Fact]
    public void HanoiDialogueJson_All13Graphs_HaveValidStartNodesAndLinks()
    {
        var service = new DialogueService();
        service.LoadDialogueFile("Data/dialogues_hanoi.json");

        Assert.True(service.LoadedGraphs.Count >= 13, $"Expected >= 13 graphs loaded, found {service.LoadedGraphs.Count}");

        foreach (var (graphId, graph) in service.LoadedGraphs)
        {
            Assert.False(string.IsNullOrWhiteSpace(graph.DialogueId), $"Graph has empty DialogueId");
            Assert.False(string.IsNullOrWhiteSpace(graph.StartNodeId), $"Graph {graphId} has empty StartNodeId");

            var nodeMap = new Dictionary<string, DialogueNode>();
            foreach (var n in graph.Nodes)
            {
                Assert.False(string.IsNullOrWhiteSpace(n.Id), $"Graph {graphId} contains node with empty Id");
                Assert.False(nodeMap.ContainsKey(n.Id), $"Graph {graphId} has duplicate node Id '{n.Id}'");
                nodeMap[n.Id] = n;
            }

            Assert.True(nodeMap.ContainsKey(graph.StartNodeId),
                $"Graph {graphId} StartNodeId '{graph.StartNodeId}' does not exist in its nodes list");

            // Verify nextNodeId links
            foreach (var n in graph.Nodes)
            {
                if (!string.IsNullOrEmpty(n.NextNodeId))
                {
                    Assert.True(nodeMap.ContainsKey(n.NextNodeId),
                        $"Graph {graphId} node '{n.Id}' points to non-existent NextNodeId '{n.NextNodeId}'");
                }

                if (n.Branches != null)
                {
                    foreach (var b in n.Branches)
                    {
                        Assert.False(string.IsNullOrEmpty(b.NextNodeId),
                            $"Graph {graphId} node '{n.Id}' has branch with empty NextNodeId");
                        Assert.True(nodeMap.ContainsKey(b.NextNodeId),
                            $"Graph {graphId} node '{n.Id}' branch points to non-existent NextNodeId '{b.NextNodeId}'");
                    }
                }
            }
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("{}")]
    [InlineData("[]")]
    [InlineData("{\"dialogues\": []}")]
    [InlineData("{\"dialogues\": null}")]
    [InlineData("{\"invalid\": 123}")]
    [InlineData("NOT_EVEN_JSON:::")]
    public void LoadDialogueJson_AdversarialInputs_NeverThrow(string input)
    {
        var service = new DialogueService();
        var ex = Record.Exception(() => service.LoadDialogueJson(input));
        Assert.Null(ex);
    }

    [Fact]
    public void LoadDialogueFile_NonExistentFile_HandledGracefully()
    {
        var service = new DialogueService();
        var ex = Record.Exception(() => service.LoadDialogueFile("NonExistent/Path/To/File.json"));
        Assert.Null(ex);
        Assert.Empty(service.LoadedGraphs);
    }

    [Fact]
    public void DialogueService_SelectChoice_InvalidChoiceId_ReturnsNullGracefully()
    {
        var service = new DialogueService();
        const string json = """
        {
            "dialogueId": "TestChoice",
            "startNodeId": "N1",
            "nodes": [
                {
                    "id": "N1",
                    "speaker": "Speaker",
                    "text": "Hello",
                    "branches": [
                        { "choiceId": "C1", "choiceText": "Opt 1", "nextNodeId": "N2" }
                    ]
                },
                { "id": "N2", "speaker": "Speaker", "text": "World" }
            ]
        }
        """;
        service.LoadDialogueJson(json);
        service.StartDialogue("TestChoice");

        var resultInvalidString = service.SelectChoice("NonExistentChoiceId");
        Assert.Null(resultInvalidString);

        var resultNegativeIndex = service.SelectChoice(-1);
        Assert.Null(resultNegativeIndex);

        var resultOutOfRangeIndex = service.SelectChoice(99);
        Assert.Null(resultOutOfRangeIndex);
    }

    [Fact]
    public void DialogueService_AdvanceToNext_WithBranches_DoesNotAdvanceAutomatically()
    {
        var service = new DialogueService();
        const string json = """
        {
            "dialogueId": "TestBranches",
            "startNodeId": "N1",
            "nodes": [
                {
                    "id": "N1",
                    "speaker": "Speaker",
                    "text": "Choose",
                    "branches": [
                        { "choiceId": "C1", "choiceText": "Opt 1", "nextNodeId": "N2" }
                    ]
                },
                { "id": "N2", "speaker": "Speaker", "text": "Done" }
            ]
        }
        """;
        service.LoadDialogueJson(json);
        service.StartDialogue("TestBranches");

        // Calling AdvanceToNext on a node with branches should stay on N1
        var node = service.AdvanceToNext();
        Assert.NotNull(node);
        Assert.Equal("N1", node.Id);
    }

    // =========================================================================
    // SECTION 3: TYPEWRITER PROGRESSION & VIETNAMESE NORMALIZATION
    // =========================================================================

    [Fact]
    public void DialogueBox_TypewriterProgression_Matches38CharsPerSecRate()
    {
        var box = new DialogueBox();
        box.Show("Bé Mây", "12345678901234567890123456789012345678"); // 38 characters

        Assert.Equal(0, box.VisibleCharCount);
        Assert.False(box.IsTypingComplete);

        // Advance 0.5s -> should show ~19 characters (0.5 * 38 = 19)
        box.Update(0.5f);
        Assert.Equal(19, box.VisibleCharCount);
        Assert.False(box.IsTypingComplete);

        // Advance another 0.5s -> total 1.0s -> should show 38 characters
        box.Update(0.5f);
        Assert.Equal(38, box.VisibleCharCount);
        Assert.True(box.IsTypingComplete);
    }

    [Fact]
    public void DialogueBox_FastForward_InstantlyCompletesAnyLengthText()
    {
        var box = new DialogueBox();
        string longText = new string('A', 500);
        box.Show("Bé Mây", longText);

        Assert.False(box.IsTypingComplete);
        box.FastForward();
        Assert.True(box.IsTypingComplete);
        Assert.Equal(500, box.VisibleCharCount);
        Assert.Equal(longText, box.VisibleText);
    }

    [Fact]
    public void DialogueBox_EmptyText_HandlesGracefullyWithoutDivByZero()
    {
        var box = new DialogueBox();
        box.Show("Bé Mây", "");

        Assert.True(box.IsTypingComplete);
        Assert.Equal(0, box.VisibleCharCount);
        Assert.Equal("", box.VisibleText);

        var ex = Record.Exception(() => box.FastForward());
        Assert.Null(ex);
        Assert.True(box.IsTypingComplete);
    }

    // =========================================================================
    // SECTION 4: BÚT CHÌ HINT ENGINE & DEBOUNCE TESTS
    // =========================================================================

    [Fact]
    public void HintEngine_All5MissingIngredients_ProduceActionableSpecificGuidance()
    {
        var hintManager = new HintManager();

        // 1. Missing Thảo Mộc (priority 1)
        var s1 = Level1State.CreateDefault();
        s1.ActiveQuestId = QuestIds.Hanoi01;
        s1.QuestFlags.Q1_Started = true;
        s1.QuestFlags.Q1_ThaoMoc_Collected = false;
        Assert.Contains("Lãn Ông", hintManager.GetCurrentHint(QuestIds.Hanoi01, s1));

        // 2. Missing Hành Lá
        var s2 = Level1State.CreateDefault();
        s2.ActiveQuestId = QuestIds.Hanoi01;
        s2.QuestFlags.Q1_Started = true;
        s2.QuestFlags.Q1_ThaoMoc_Collected = true;
        s2.QuestFlags.Q1_HanhLa_Collected = false;
        Assert.Contains("hành", hintManager.GetCurrentHint(QuestIds.Hanoi01, s2).ToLower());

        // 3. Missing Nước Dùng
        var s3 = Level1State.CreateDefault();
        s3.ActiveQuestId = QuestIds.Hanoi01;
        s3.QuestFlags.Q1_Started = true;
        s3.QuestFlags.Q1_ThaoMoc_Collected = true;
        s3.QuestFlags.Q1_HanhLa_Collected = true;
        s3.QuestFlags.Q1_NuocDung_Collected = false;
        Assert.Contains("nước dùng", hintManager.GetCurrentHint(QuestIds.Hanoi01, s3).ToLower());

        // 4. Missing Thịt Bò
        var s4 = Level1State.CreateDefault();
        s4.ActiveQuestId = QuestIds.Hanoi01;
        s4.QuestFlags.Q1_Started = true;
        s4.QuestFlags.Q1_ThaoMoc_Collected = true;
        s4.QuestFlags.Q1_HanhLa_Collected = true;
        s4.QuestFlags.Q1_NuocDung_Collected = true;
        s4.QuestFlags.Q1_ThitBo_Collected = false;
        Assert.Contains("thịt bò", hintManager.GetCurrentHint(QuestIds.Hanoi01, s4).ToLower());

        // 5. Missing Bánh Phở
        var s5 = Level1State.CreateDefault();
        s5.ActiveQuestId = QuestIds.Hanoi01;
        s5.QuestFlags.Q1_Started = true;
        s5.QuestFlags.Q1_ThaoMoc_Collected = true;
        s5.QuestFlags.Q1_HanhLa_Collected = true;
        s5.QuestFlags.Q1_NuocDung_Collected = true;
        s5.QuestFlags.Q1_ThitBo_Collected = true;
        s5.QuestFlags.Q1_BanhPho_Collected = false;
        Assert.Contains("bánh phở", hintManager.GetCurrentHint(QuestIds.Hanoi01, s5).ToLower());
    }

    [Fact]
    public void ButChiGuideUI_DebounceCooldown_BlocksRapidSpamming()
    {
        var hintManager = new HintManager();
        var mockInput = new MockInputService();
        var guide = new ButChiGuideUI(hintManager, inputService: mockInput);
        var state = Level1State.CreateDefault();

        // 1. First trigger via [H] pulse
        mockInput.IsHintJustPressed = true;
        guide.Update(0.016f, QuestIds.Hanoi01, state);
        mockInput.IsHintJustPressed = false;

        Assert.True(guide.IsHintActive);
        string firstHint = guide.CurrentHintText;

        // Change state to test if rapid re-press triggers immediate new evaluation
        state.QuestFlags.Q1_ThaoMoc_Collected = true;

        // Try triggering again at 0.10s (< 0.35s debounce cooldown)
        guide.Update(0.10f, QuestIds.Hanoi01, state);
        mockInput.IsHintJustPressed = true;
        guide.Update(0.016f, QuestIds.Hanoi01, state);
        mockInput.IsHintJustPressed = false;

        // Should still be showing first hint because debounce prevented re-trigger
        Assert.Equal(firstHint, guide.CurrentHintText);

        // Advance past debounce cooldown (> 0.35s total)
        guide.Update(0.30f, QuestIds.Hanoi01, state);

        // Now trigger again
        mockInput.IsHintJustPressed = true;
        guide.Update(0.016f, QuestIds.Hanoi01, state);
        mockInput.IsHintJustPressed = false;

        // Should have updated to second hint (Hành Lá)
        Assert.NotEqual(firstHint, guide.CurrentHintText);
        Assert.Contains("hành", guide.CurrentHintText.ToLower());
    }

    [Fact]
    public void ButChiGuideUI_IdleTimer_DoesNotAccumulateWhileHintIsActive()
    {
        var hintManager = new HintManager();
        var guide = new ButChiGuideUI(hintManager);
        var state = Level1State.CreateDefault();

        // Trigger a hint (duration is 6.0s)
        guide.TriggerHint(QuestIds.Hanoi01, state);
        Assert.True(guide.IsHintActive);
        Assert.Equal(0f, guide.IdleSeconds);

        // Advance 5.0s (still within 6.0s duration)
        guide.Update(5.0f, QuestIds.Hanoi01, state);
        Assert.True(guide.IsHintActive);
        Assert.Equal(0f, guide.IdleSeconds); // Idle timer MUST NOT accumulate while reading hint

        // Advance another 2.0s (hint expires)
        guide.Update(2.0f, QuestIds.Hanoi01, state);
        Assert.False(guide.IsHintActive);
        // Idle timer should now begin accumulating
        guide.Update(10.0f, QuestIds.Hanoi01, state);
        Assert.True(guide.IdleSeconds > 0f);
    }

    [Fact]
    public void ButChiGuideUI_FailedAttemptsThreshold_TriggersAlertAtExactCount()
    {
        var hintManager = new HintManager();
        var guide = new ButChiGuideUI(hintManager);

        guide.NotifyFailure();
        Assert.False(guide.IsIdleAlertActive);

        guide.NotifyFailure();
        Assert.False(guide.IsIdleAlertActive);

        guide.NotifyFailure();
        Assert.True(guide.IsIdleAlertActive, "Alert should activate on exactly 3 failures");

        // Progress clears failure alert
        guide.NotifyProgress();
        Assert.False(guide.IsIdleAlertActive);
        Assert.Equal(0, guide.FailedAttemptsCount);
    }

    // =========================================================================
    // SECTION 5: ADVERSARIAL STRESS HARNESSES (MILESTONE 4 ITERATION 2)
    // =========================================================================

    [Fact]
    public void AdversarialStress_DialogueBox_RapidInputSpamming_MaintainsStateIntegrity()
    {
        // ARRANGE: Dialogue node with 3 branches and moderate text
        var service = new DialogueService();
        var graph = new DialogueGraph
        {
            DialogueId = "SpamTestGraph",
            StartNodeId = "N1",
            Nodes = new List<DialogueNode>
            {
                new()
                {
                    Id = "N1",
                    Speaker = "Bút Chì",
                    Text = "Phố cổ Hà Nội có ba mươi sáu phố phường, mỗi phố lưu giữ một nét nghề truyền thống độc đáo qua ngàn năm văn hiến.",
                    Branches = new List<DialogueBranch>
                    {
                        new() { ChoiceId = "C1", ChoiceText = "Phố Hàng Bạc", NextNodeId = "N2" },
                        new() { ChoiceId = "C2", ChoiceText = "Phố Hàng Gai", NextNodeId = "N3" },
                        new() { ChoiceId = "C3", ChoiceText = "Phố Hàng Chiếu", NextNodeId = "N4" }
                    }
                },
                new() { Id = "N2", Speaker = "Bút Chì", Text = "Nghề chạm bạc tinh xảo." },
                new() { Id = "N3", Speaker = "Bút Chì", Text = "Nghề tơ lụa mềm mại." },
                new() { Id = "N4", Speaker = "Bút Chì", Text = "Nghề dệt chiếu cói bền chắc." }
            }
        };

        service.LoadDialogueJson(JsonSerializer.Serialize(graph));
        service.StartDialogue("SpamTestGraph");

        var box = new DialogueBox();
        box.Show(service.CurrentNode!, service);

        var mockInput = new MockInputService();
        var rng = new Random(42);
        float[] deltaSamples = { 0f, 0.0001f, 0.005f, 0.016f, 0.033f, 0.1f };

        // ACT: Run 600 frames of chaotic input updates
        for (int frame = 0; frame < 600; frame++)
        {
            float dt = deltaSamples[rng.Next(deltaSamples.Length)];
            int actionType = rng.Next(8);

            mockInput.IsConfirmJustPressed = (actionType == 0 || actionType == 4);
            mockInput.IsInteractJustPressed = (actionType == 1 || actionType == 4);
            mockInput.IsCancelJustPressed = (actionType == 2);
            mockInput.IsPointerJustPressed = (actionType == 3);
            mockInput.PointerPosition = new Vector2(rng.Next(0, 1280), rng.Next(0, 720));

            if (actionType == 5) box.SelectNextChoice();
            if (actionType == 6) box.SelectPreviousChoice();

            // Execute frame update
            var ex = Record.Exception(() => box.Update(dt, mockInput));
            Assert.Null(ex);

            // EMPIRICAL INVARIANTS:
            Assert.True(box.TypewriterProgress >= 0f, $"TypewriterProgress must be non-negative: {box.TypewriterProgress}");
            Assert.InRange(box.VisibleCharCount, 0, box.CurrentText.Length);
            Assert.NotNull(box.VisibleText);
            Assert.True(box.VisibleText.Length <= box.CurrentText.Length);

            if (box.CurrentBranches.Count > 0)
            {
                Assert.InRange(box.SelectedChoiceIndex, 0, box.CurrentBranches.Count - 1);
            }

            // If dialogue closed via cancel/complete, restart to continue stress testing
            if (!box.IsActive)
            {
                service.StartDialogue("SpamTestGraph");
                box.Show(service.CurrentNode!, service);
            }
        }
    }

    [Fact]
    public void AdversarialStress_ExplorationScene_InputSpammingDuringDialogue_MaintainsStateIntegrity()
    {
        // ARRANGE: Setup headless HanoiExplorationScene
        var bus = new EventBus();
        var qm = new QuestManager(bus);
        HanoiQuests.RegisterAll(qm);

        var saveData = SaveData.CreateDefault();
        var input = new MockInputService();
        var sm = new GameStateManager(bus, GameState.Exploration);

        var scene = new HanoiExplorationScene(
            saveData: saveData,
            questManager: qm,
            stateManager: sm,
            inputService: input,
            audioService: new NullAudioService(),
            eventBus: bus
        );

        scene.Enter();

        // Position player near Bà An
        scene.Kinematics.Position = scene.BaAn.Position + new Vector2(0, 25f);
        var rng = new Random(1337);

        // ACT: 300 cycles of chaotic player updates (interact, confirm, cancel, move)
        for (int i = 0; i < 300; i++)
        {
            float dt = (float)(0.008 + rng.NextDouble() * 0.025);
            input.IsInteractJustPressed = (rng.Next(3) == 0);
            input.IsConfirmJustPressed = (rng.Next(4) == 0);
            input.IsCancelJustPressed = (rng.Next(6) == 0);
            input.MovementDirection = (rng.Next(2) == 0)
                ? Vector2.Zero
                : Vector2.Normalize(new Vector2((float)rng.NextDouble() - 0.5f, (float)rng.NextDouble() - 0.5f));

            var ex = Record.Exception(() => scene.Update(dt));
            Assert.Null(ex);

            // Invariants: Player coordinates must not explode, state manager must remain valid
            Assert.False(float.IsNaN(scene.Kinematics.Position.X));
            Assert.False(float.IsNaN(scene.Kinematics.Position.Y));
            Assert.False(float.IsInfinity(scene.Kinematics.Position.X));
            Assert.False(float.IsInfinity(scene.Kinematics.Position.Y));
            Assert.True(Enum.IsDefined(sm.CurrentState));
        }
    }

    [Fact]
    public void AdversarialOracle_DialogueService_MultiOptionSequentialFiltering_AllCombinationsHitExactNodes()
    {
        // ARRANGE: Node with 4 branches:
        // Branch 0: Requires Q1_Completed
        // Branch 1: Requires Q2_Completed
        // Branch 2: Unconditional (Default)
        // Branch 3: Requires Gate_LongBien_Unlocked
        var service = new DialogueService();
        var node = new DialogueNode
        {
            Id = "Node_Root",
            Speaker = "Trưởng Thôn",
            Text = "Ngươi muốn đi con đường nào?",
            Branches = new List<DialogueBranch>
            {
                new() { ChoiceId = "Choice_Q1", ChoiceText = "Đường Bí Mật", NextNodeId = "Node_Q1", RequiredQuestFlag = nameof(QuestFlags.Q1_Completed) },
                new() { ChoiceId = "Choice_Q2", ChoiceText = "Đường Tiểu Thương", NextNodeId = "Node_Q2", RequiredQuestFlag = nameof(QuestFlags.Q2_Completed) },
                new() { ChoiceId = "Choice_Default", ChoiceText = "Đường Đại Lộ", NextNodeId = "Node_Default" },
                new() { ChoiceId = "Choice_Gate", ChoiceText = "Đường Tông Sư", NextNodeId = "Node_Gate", RequiredQuestFlag = nameof(QuestFlags.Gate_LongBien_Unlocked) }
            }
        };

        var graph = new DialogueGraph
        {
            DialogueId = "Graph_MultiFilterOracle",
            StartNodeId = "Node_Root",
            Nodes = new List<DialogueNode>
            {
                node,
                new() { Id = "Node_Q1", Speaker = "Trưởng Thôn", Text = "Vào mật đạo." },
                new() { Id = "Node_Q2", Speaker = "Trưởng Thôn", Text = "Vào chợ sầm uất." },
                new() { Id = "Node_Default", Speaker = "Trưởng Thôn", Text = "Đi đường cái quan." },
                new() { Id = "Node_Gate", Speaker = "Trưởng Thôn", Text = "Lên núi bái sư." }
            }
        };

        service.LoadDialogueJson(JsonSerializer.Serialize(graph));

        // Test all 2^3 = 8 combinations of (Q1_Completed, Q2_Completed, Gate_LongBien_Unlocked)
        for (int mask = 0; mask < 8; mask++)
        {
            bool hasQ1 = (mask & 1) != 0;
            bool hasQ2 = (mask & 2) != 0;
            bool hasGate = (mask & 4) != 0;

            var flags = new QuestFlags();
            if (hasQ1) flags.SetFlag(nameof(QuestFlags.Q1_Completed), true);
            if (hasQ2) flags.SetFlag(nameof(QuestFlags.Q2_Completed), true);
            if (hasGate) flags.SetFlag(nameof(QuestFlags.Gate_LongBien_Unlocked), true);

            // Determine expected available branches
            var availableBranches = service.GetAvailableBranches(node, flags);
            int expectedCount = 1 + (hasQ1 ? 1 : 0) + (hasQ2 ? 1 : 0) + (hasGate ? 1 : 0);
            Assert.Equal(expectedCount, availableBranches.Count);

            // EMPIRICAL ORACLE: For every UI choice index presented on screen,
            // selecting that index MUST navigate to the corresponding branch's NextNodeId.
            for (int uiIndex = 0; uiIndex < availableBranches.Count; uiIndex++)
            {
                service.StartDialogue("Graph_MultiFilterOracle");
                var box = new DialogueBox();
                box.Show(service.CurrentNode!, service, availableBranches);
                box.FastForward();

                var expectedBranch = availableBranches[uiIndex];
                box.ChooseOption(uiIndex);

                Assert.Equal(expectedBranch.NextNodeId, service.CurrentNode?.Id);
            }
        }
    }

    [Fact]
    public void AdversarialEdgeCase_DialogueService_AllBranchesLocked_GracefulBehavior()
    {
        // ARRANGE: Node where all branches require unmet flags
        var service = new DialogueService();
        var node = new DialogueNode
        {
            Id = "Node_Locked",
            Speaker = "Người Gác Cổng",
            Text = "Ngươi không có bất kỳ lệnh bài nào!",
            Branches = new List<DialogueBranch>
            {
                new() { ChoiceId = "C1", ChoiceText = "Lệnh bài đỏ", NextNodeId = "N_Red", RequiredQuestFlag = "Flag_Red" },
                new() { ChoiceId = "C2", ChoiceText = "Lệnh bài lam", NextNodeId = "N_Blue", RequiredQuestFlag = "Flag_Blue" }
            }
        };

        var graph = new DialogueGraph
        {
            DialogueId = "Graph_Locked",
            StartNodeId = "Node_Locked",
            Nodes = new List<DialogueNode>
            {
                node,
                new() { Id = "N_Red", Speaker = "Người Gác Cổng", Text = "Mở cửa đỏ." },
                new() { Id = "N_Blue", Speaker = "Người Gác Cổng", Text = "Mở cửa lam." }
            }
        };

        service.LoadDialogueJson(JsonSerializer.Serialize(graph));
        service.StartDialogue("Graph_Locked");

        var flags = new QuestFlags(); // No flags set
        var availableBranches = service.GetAvailableBranches(service.CurrentNode, flags);
        Assert.Empty(availableBranches);

        var box = new DialogueBox();
        box.Show(service.CurrentNode!, service, availableBranches);
        box.FastForward();

        Assert.Empty(box.CurrentBranches);
        Assert.True(box.IsTypingComplete);

        // Calling ChooseOption when no branches are available must not throw
        var exChoose = Record.Exception(() => box.ChooseOption(0));
        Assert.Null(exChoose);

        // Calling Dismiss cleanly closes the dialogue
        box.Dismiss();
        Assert.False(box.IsActive);
        Assert.True(box.RequestClose);
    }

    [Fact]
    public void AdversarialStress_DialogueBox_LongVietnameseFormCText_NormalizationAndTypewriterSlicing()
    {
        // 1. Construct long Vietnamese text with all diacritic marks, composed and decomposed
        const string vietnameseSourceText =
            "Hà Nội nghìn năm văn hiến rực rỡ sắc màu qua từng góc phố cổ kính. " +
            "Bé Mây cùng Bút Chì rong ruổi qua phố Hàng Bạc, Hàng Gai, Hàng Chiếu, " +
            "tìm kiếm từng nguyên liệu thảo mộc Lãn Ông thanh tao, hành hoa tươi xanh, " +
            "thịt bò mềm ngọt, bánh phở trắng tinh dẻo thơm và nồi nước dùng ninh xương đậm đà. " +
            "Ánh nắng vàng ươm chiếu qua tán bàng xanh mướt, tiếng leng keng tàu điện xưa " +
            "hòa cùng nhịp thở sôi động của ba mươi sáu phố phường. " +
            "Từng giọt nước dùng thanh trong nghi ngút khói đem lại niềm vui ấm áp, " +
            "giúp khôi phục lại sắc màu ký ức cho bức tranh quê hương tươi đẹp và rạng rỡ!";

        // Convert explicitly to NFD (Form D decomposed Unicode)
        string decomposedNfdText = vietnameseSourceText.Normalize(NormalizationForm.FormD);
        Assert.False(decomposedNfdText.IsNormalized(NormalizationForm.FormC));

        var box = new DialogueBox();
        box.Show("Bút Chì", decomposedNfdText);

        // EMPIRICAL CHECK: Show() must immediately normalize CurrentText to Form C
        Assert.True(box.CurrentText.IsNormalized(NormalizationForm.FormC));
        Assert.Equal(vietnameseSourceText.Normalize(NormalizationForm.FormC), box.CurrentText);

        // Step through typewriter character by character across 50 frames
        for (int f = 0; f < 50; f++)
        {
            box.Update(0.05f); // 0.05s * 38 chars/sec = ~1.9 chars/frame
            Assert.True(box.VisibleText.IsNormalized(NormalizationForm.FormC));
            Assert.StartsWith(box.VisibleText, box.CurrentText);
            Assert.Equal(box.VisibleCharCount, box.VisibleText.Length);
        }

        // Fast forward instantly finishes
        box.FastForward();
        Assert.True(box.IsTypingComplete);
        Assert.Equal(box.CurrentText.Length, box.VisibleCharCount);
        Assert.Equal(box.CurrentText, box.VisibleText);
    }

    [Fact]
    public void AdversarialStress_DialogueBox_SurrogatePairsAndEmojis_TypewriterSafety()
    {
        // Text containing surrogate pair emojis (🪷 lotus, 🍜 ramen/pho, ✏️ pencil, 🇻🇳 flag)
        const string emojiText = "Bé Mây nhặt được bông sen 🪷 và bát phở thơm 🍜 cùng chiếc bút chì ✏️ trên đất Hà Nội 🇻🇳!";

        var box = new DialogueBox();
        box.Show("Bé Mây", emojiText);

        // Advance gradually
        for (int i = 0; i < 40; i++)
        {
            var ex = Record.Exception(() => box.Update(0.04f));
            Assert.Null(ex);
            Assert.InRange(box.VisibleCharCount, 0, box.CurrentText.Length);
            Assert.True(box.VisibleText.Length <= box.CurrentText.Length);
        }

        box.FastForward();
        Assert.Equal(box.CurrentText, box.VisibleText);
    }

    [Fact]
    public void AdversarialStress_ExplorationScene_BaAnDialogue_ImmediatePhoTransition_StateIntegrity()
    {
        // ARRANGE: HanoiExplorationScene with Q1 completed and Q2 pending
        var bus = new EventBus();
        var qm = new QuestManager(bus);
        HanoiQuests.RegisterAll(qm);

        var saveData = SaveData.CreateDefault();
        saveData.Level1State.QuestFlags.Q1_Started = true;
        saveData.Level1State.QuestFlags.Q1_Completed = true;
        saveData.Level1State.QuestFlags.Q2_Completed = false;

        var input = new MockInputService();
        var sm = new GameStateManager(bus, GameState.Exploration);

        var scene = new HanoiExplorationScene(
            saveData: saveData,
            questManager: qm,
            stateManager: sm,
            inputService: input,
            audioService: new NullAudioService(),
            eventBus: bus
        );

        scene.Enter();

        // Position player right at Bà An
        scene.Kinematics.Position = scene.BaAn.Position + new Vector2(0, 25f);

        // Frame 1: Trigger dialogue via interact [E]
        input.IsInteractJustPressed = true;
        scene.Update(0.016f);
        input.IsInteractJustPressed = false;

        Assert.True(scene.DialogueBox.IsActive);
        Assert.Equal("Bà An", scene.DialogueBox.CurrentSpeaker);

        // Frame 2: Immediately dismiss dialogue via Confirm [Enter]
        input.IsConfirmJustPressed = true;
        scene.Update(0.016f);
        input.IsConfirmJustPressed = false;

        // EMPIRICAL CHECK:
        // DialogueBox is dismissed and state transitions immediately to MiniGamePho
        Assert.False(scene.DialogueBox.IsActive);
        Assert.Equal(GameState.MiniGamePho, sm.CurrentState);

        // Advance 30 frames in MiniGamePho: Scene must update seamlessly without crashing
        for (int i = 0; i < 30; i++)
        {
            var ex = Record.Exception(() => scene.Update(0.016f));
            Assert.Null(ex);
            Assert.Equal(GameState.MiniGamePho, sm.CurrentState);
        }
    }

    [Fact]
    public void AdversarialStress_ExplorationScene_LongBienGateDialogue_ImmediateStallPuzzleTransition_StateIntegrity()
    {
        // ARRANGE: Scene with Q2 completed and Q3 pending (stalls puzzle required)
        var bus = new EventBus();
        var qm = new QuestManager(bus);
        HanoiQuests.RegisterAll(qm);

        var saveData = SaveData.CreateDefault();
        saveData.Level1State.QuestFlags.Q1_Completed = true;
        saveData.Level1State.QuestFlags.Q2_Completed = true;
        saveData.Level1State.QuestFlags.Q3_Completed = false;
        saveData.Level1State.QuestFlags.Gate_LongBien_Unlocked = false;

        var input = new MockInputService();
        var sm = new GameStateManager(bus, GameState.Exploration);

        var scene = new HanoiExplorationScene(
            saveData: saveData,
            questManager: qm,
            stateManager: sm,
            inputService: input,
            audioService: new NullAudioService(),
            eventBus: bus
        );

        scene.Enter();

        // Position player at Long Bien Gate
        scene.Kinematics.Position = scene.LongBienGate.Position + new Vector2(-20f, 0);

        // Frame 1: Interact with Gate
        input.IsInteractJustPressed = true;
        scene.Update(0.016f);
        input.IsInteractJustPressed = false;

        Assert.True(scene.DialogueBox.IsActive);
        Assert.Contains("sạp hàng", scene.DialogueBox.CurrentText.ToLower());

        // Frame 2: Immediately dismiss dialogue via Confirm
        input.IsConfirmJustPressed = true;
        scene.Update(0.016f);
        input.IsConfirmJustPressed = false;

        // EMPIRICAL CHECK:
        // DialogueBox is dismissed and state transitions to MiniGameStalls
        Assert.False(scene.DialogueBox.IsActive);
        Assert.Equal(GameState.MiniGameStalls, sm.CurrentState);

        // Advance 30 frames in MiniGameStalls
        for (int i = 0; i < 30; i++)
        {
            var ex = Record.Exception(() => scene.Update(0.016f));
            Assert.Null(ex);
            Assert.Equal(GameState.MiniGameStalls, sm.CurrentState);
        }
    }

    [Fact]
    public void AdversarialStress_ExplorationScene_LongBienGate_Unlocked_CutsceneAndWorldMapTransitions()
    {
        // ARRANGE: Gate unlocked, but Gray Shadow letter not yet read
        var bus = new EventBus();
        var qm = new QuestManager(bus);
        HanoiQuests.RegisterAll(qm);

        var saveData = SaveData.CreateDefault();
        saveData.Level1State.QuestFlags.Q1_Completed = true;
        saveData.Level1State.QuestFlags.Q2_Completed = true;
        saveData.Level1State.QuestFlags.Q3_Completed = true;
        saveData.Level1State.QuestFlags.Gate_LongBien_Unlocked = true;
        saveData.Level1State.QuestFlags.GrayShadow_LetterRead = false;

        var input = new MockInputService();
        var sm = new GameStateManager(bus, GameState.Exploration);

        var scene = new HanoiExplorationScene(
            saveData: saveData,
            questManager: qm,
            stateManager: sm,
            inputService: input,
            audioService: new NullAudioService(),
            eventBus: bus
        );

        scene.Enter();
        scene.Kinematics.Position = scene.LongBienGate.Position + new Vector2(-20f, 0);

        // Frame 1: Interact with Gate
        input.IsInteractJustPressed = true;
        scene.Update(0.016f);
        input.IsInteractJustPressed = false;

        // Frame 2: Dismiss gate dialogue
        input.IsConfirmJustPressed = true;
        scene.Update(0.016f);
        input.IsConfirmJustPressed = false;

        // Transitions to Cutscene (Letter modal)
        Assert.Equal(GameState.Cutscene, sm.CurrentState);

        // Frame 3a: First confirm fast-forwards letter typewriter
        input.IsConfirmJustPressed = true;
        scene.Update(0.016f);
        input.IsConfirmJustPressed = false;

        // Frame 3b: Second confirm dismisses letter modal
        input.IsConfirmJustPressed = true;
        scene.Update(0.016f);
        input.IsConfirmJustPressed = false;

        Assert.True(saveData.Level1State.QuestFlags.GrayShadow_LetterRead);
        Assert.Equal(GameState.Exploration, sm.CurrentState);

        // Frame 3: Re-interact with Gate after letter read -> World Map requested
        input.IsInteractJustPressed = true;
        scene.Update(0.016f);
        input.IsInteractJustPressed = false;

        input.IsConfirmJustPressed = true;
        scene.Update(0.016f);
        input.IsConfirmJustPressed = false;

        Assert.True(scene.RequestWorldMap, "RequestWorldMap should be true when exiting through unlocked gate after letter read");
    }

    [Fact]
    public void AdversarialStress_HintEngine_ExtremeInputsAndRapidQuerySpam()
    {
        var hintManager = new HintManager();
        var state = Level1State.CreateDefault();

        // 1. Extreme/invalid quest IDs should return safe default guidance
        string h1 = hintManager.GetCurrentHint("NonExistent_Quest_999", state);
        Assert.False(string.IsNullOrWhiteSpace(h1));

        string h2 = hintManager.GetCurrentHint("", state);
        Assert.False(string.IsNullOrWhiteSpace(h2));

        // 2. ButChiGuideUI handling negative, zero, and massive delta times
        var guide = new ButChiGuideUI(hintManager);

        var exNeg = Record.Exception(() => guide.Update(-10.0f, QuestIds.Hanoi01, state));
        Assert.Null(exNeg);

        var exZero = Record.Exception(() => guide.Update(0.0f, QuestIds.Hanoi01, state));
        Assert.Null(exZero);

        var exHuge = Record.Exception(() => guide.Update(100000.0f, QuestIds.Hanoi01, state));
        Assert.Null(exHuge);

        // 3. Rapid oscillation between failure and progress
        for (int i = 0; i < 200; i++)
        {
            guide.NotifyFailure();
            guide.NotifyFailure();
            guide.NotifyFailure();
            Assert.True(guide.IsIdleAlertActive);

            guide.NotifyProgress();
            Assert.False(guide.IsIdleAlertActive);
            Assert.Equal(0, guide.FailedAttemptsCount);
        }
    }
}
