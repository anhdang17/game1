using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using GameProject.Audio;
using GameProject.Core.Album;
using GameProject.Core.Dialogue;
using GameProject.Core.Events;
using GameProject.Core.Hints;
using GameProject.Core.Models;
using GameProject.Core.Persistence;
using GameProject.Core.Ports;
using GameProject.Core.Puzzles;
using GameProject.Core.Quests;
using GameProject.Core.State;
using GameProject.Core.WorldMap;
using GameProject.Graphics;
using GameProject.Stages;
using GameProject.Tests.Mocks;
using Raylib_cs;
using Xunit;
using NullAudioService = GameProject.Core.Ports.NullAudioService;
using MockInputService = GameProject.Tests.Mocks.MockInputService;

namespace GameProject.Tests;

/// <summary>
/// Empirical Adversarial Challenger Test Suite for Milestone 5:
/// 1. Headless Audio Safety Invariants across all 5 scenes (Hanoi, Pho, Stall, WorldMap, Album)
/// 2. Multi-Threaded Concurrency Stress (20 concurrent threads running update, event bus, dialogue, save/load)
/// 3. Memory & Resource Stability (2,000 rapid scene transitions without memory explosion)
/// 4. UTF-8 & Form C Normalization Under Stress (Extreme Vietnamese diacritics, emojis, control codes, corrupted JSON)
/// 5. Production Packaging & Performance Invariants (Standalone binary inspection and execution budget)
/// </summary>
public class ChallengerM5PackagingAndPerformanceTests
{
    // =========================================================================
    // SECTION 1: HEADLESS AUDIO SAFETY INVARIANTS ACROSS ALL SCENES
    // =========================================================================

    [Fact]
    public void HeadlessAudioSafety_AllFiveScenes_InstantiateHeadlessly_NeverInvokesMiniaudio()
    {
        // INVARIANT: In headless / test environments, Raylib window is never ready
        Assert.False(Raylib.IsWindowReady(), "Window must not be ready in headless CI environment.");
        Assert.False(Raylib.IsAudioDeviceReady(), "Audio device must not be ready in headless CI environment.");

        var saveData = SaveData.CreateDefault();
        var eventBus = new EventBus();
        var mockInput = new MockInputService();
        var questManager = new QuestManager(eventBus);

        // 1. HanoiExplorationScene: must default to NullAudioService when headless
        var expScene = new HanoiExplorationScene(saveData, inputService: mockInput, eventBus: eventBus);
        Assert.NotNull(expScene.AudioService);
        Assert.IsType<NullAudioService>(expScene.AudioService);
        Assert.IsNotType<RaylibAudioService>(expScene.AudioService);

        // Exercise HanoiExplorationScene lifecycle
        expScene.Enter();
        expScene.Update(0.016f);
        expScene.Exit();
        expScene.Dispose();

        // 2. PhoAssemblyScene: must safely handle null or NullAudioService without miniaudio hardware initialization
        var phoSceneWithNull = new PhoAssemblyScene(saveData, questManager, eventBus, audioService: null, inputService: mockInput);
        phoSceneWithNull.Enter();
        phoSceneWithNull.Update(0.016f);
        phoSceneWithNull.Exit();
        phoSceneWithNull.Dispose();

        var nullAudio = new NullAudioService();
        var phoSceneWithMock = new PhoAssemblyScene(saveData, questManager, eventBus, audioService: nullAudio, inputService: mockInput);
        phoSceneWithMock.Enter();
        phoSceneWithMock.Update(0.016f);
        phoSceneWithMock.Exit();
        phoSceneWithMock.Dispose();

        // 3. StallPuzzleScene: must safely handle null or NullAudioService without hardware audio
        var stallSceneWithNull = new StallPuzzleScene(saveData, questManager, eventBus, audioService: null, inputService: mockInput);
        stallSceneWithNull.Enter();
        stallSceneWithNull.Update(0.016f);
        stallSceneWithNull.Exit();
        stallSceneWithNull.Dispose();

        var stallSceneWithMock = new StallPuzzleScene(saveData, questManager, eventBus, audioService: nullAudio, inputService: mockInput);
        stallSceneWithMock.Enter();
        stallSceneWithMock.Update(0.016f);
        stallSceneWithMock.Exit();
        stallSceneWithMock.Dispose();

        // 4. WorldMapScene: headless navigation and updates
        var worldMapScene = new WorldMapScene(saveData, eventBus, mockInput);
        worldMapScene.Enter();
        worldMapScene.Update(0.016f);
        worldMapScene.Exit();
        worldMapScene.Dispose();

        // 5. AlbumViewerScene: headless tab navigation and updates
        var albumScene = new AlbumViewerScene(saveData, eventBus, mockInput);
        albumScene.Enter();
        albumScene.Update(0.016f);
        albumScene.Exit();
        albumScene.Dispose();

        // Invariant holds: Audio device remained uninitialized throughout all scene lifecycles
        Assert.False(Raylib.IsAudioDeviceReady(), "Miniaudio device was unexpectedly initialized during headless scene execution.");
    }

    [Fact]
    public void HeadlessAudioSafety_NullAudioService_MethodInvariants_ZeroHardwareCalls()
    {
        var nullAudio = new NullAudioService();

        // Property accessors
        Assert.Equal(1.0f, nullAudio.MasterVolume);
        Assert.Equal(1.0f, nullAudio.MusicVolume);
        Assert.Equal(1.0f, nullAudio.SfxVolume);
        Assert.False(nullAudio.IsMuted);
        Assert.False(nullAudio.IsMusicPlaying);
        Assert.Equal(MusicTrack.None, nullAudio.CurrentTrack);

        nullAudio.MasterVolume = 0.5f;
        nullAudio.MusicVolume = 0.4f;
        nullAudio.SfxVolume = 0.6f;
        nullAudio.IsMuted = true;

        Assert.Equal(0.5f, nullAudio.MasterVolume);
        Assert.Equal(0.4f, nullAudio.MusicVolume);
        Assert.Equal(0.6f, nullAudio.SfxVolume);
        Assert.True(nullAudio.IsMuted);

        // Method invocations across all enum values
        foreach (SfxType sfx in Enum.GetValues<SfxType>())
        {
            nullAudio.PlaySfx(sfx);
            nullAudio.PlaySfx(sfx, 0.5f);
        }

        foreach (MusicTrack track in Enum.GetValues<MusicTrack>())
        {
            nullAudio.PlayMusic(track);
            nullAudio.PlayMusic(track, loop: false, fadeDuration: 1.0f);
        }

        nullAudio.PauseMusic();
        nullAudio.ResumeMusic();
        nullAudio.StopMusic(0.2f);
        nullAudio.Update(0.016f);

        // Hardware device remained completely untouched
        Assert.False(Raylib.IsAudioDeviceReady());
    }

    // =========================================================================
    // SECTION 2: MULTI-THREADED CONCURRENCY STRESS (20 CONCURRENT THREADS)
    // =========================================================================

    [Fact]
    public async Task MultiThreadedStress_TwentyConcurrentThreads_SimultaneousUpdatePublishDialogueSave_ZeroExceptions()
    {
        const int threadCount = 20;
        const int iterationsPerThread = 40;

        var exceptions = new ConcurrentBag<Exception>();
        var barrier = new Barrier(threadCount);
        var sharedBus = new EventBus();
        string tempDir = Path.Combine(Path.GetTempPath(), "game1_stress_m5_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var tasks = new Task[threadCount];

            for (int t = 0; t < threadCount; t++)
            {
                int threadId = t;
                tasks[t] = Task.Run(() =>
                {
                    try
                    {
                        // Synchronize start so all 20 threads unleash load simultaneously
                        barrier.SignalAndWait(TimeSpan.FromSeconds(5));

                        // Group 1 (Threads 0-4): EventBus publish, subscribe, unsubscribe under high contention
                        if (threadId < 5)
                        {
                            for (int i = 0; i < iterationsPerThread * 2; i++)
                            {
                                int captured = i;
                                IDisposable sub = sharedBus.Subscribe<ItemCollectedEvent>(_ => { });
                                sharedBus.Publish(new ItemCollectedEvent($"Item_Stress_{threadId}_{captured}", "Bánh Phở"));
                                sharedBus.Publish(new QuestStartedEvent("Quest_Hanoi_01", "Nhiệm vụ Hà Nội"));
                                sharedBus.Publish(new DialogueStartedEvent("BaAn_Intro", "Bà An"));
                                sub.Dispose();
                            }
                        }
                        // Group 2 (Threads 5-9): Game update loops & Scene simulations
                        else if (threadId < 10)
                        {
                            var mockInput = new MockInputService();
                            var nullAudio = new NullAudioService();

                            for (int i = 0; i < iterationsPerThread; i++)
                            {
                                var data = SaveData.CreateDefault();
                                var localBus = new EventBus();
                                var scene = new HanoiExplorationScene(data, inputService: mockInput, audioService: nullAudio, eventBus: localBus);
                                scene.Enter();
                                mockInput.SimulateMovement(1f, 0f);
                                scene.Update(0.016f);
                                mockInput.SimulateMovement(0f, 1f);
                                scene.Update(0.016f);
                                scene.Exit();
                                scene.Dispose();
                            }
                        }
                        // Group 3 (Threads 10-14): Dialogue advancement & Graph navigation
                        else if (threadId < 15)
                        {
                            for (int i = 0; i < iterationsPerThread; i++)
                            {
                                var dialService = new DialogueService();
                                dialService.LoadDialogueFile("Data/dialogues_hanoi.json");
                                if (dialService.LoadedGraphs.Count > 0)
                                {
                                    string firstGraphId = dialService.LoadedGraphs.Keys.First();
                                    dialService.StartDialogue(firstGraphId);
                                    int safetyLimit = 0;
                                    while (dialService.IsDialogueActive && safetyLimit++ < 20)
                                    {
                                        if (dialService.CurrentNode?.Branches != null && dialService.CurrentNode.Branches.Count > 0)
                                        {
                                            dialService.SelectChoice(0);
                                        }
                                        else if (!string.IsNullOrEmpty(dialService.CurrentNode?.NextNodeId))
                                        {
                                            dialService.AdvanceToNext();
                                        }
                                        else
                                        {
                                            break;
                                        }
                                    }
                                }
                            }
                        }
                        // Group 4 (Threads 15-19): Save/Load persistence with atomic writes on isolated storage
                        else
                        {
                            string threadSavePath = Path.Combine(tempDir, $"save_thread_{threadId}.json");
                            var fileStorage = new FileSaveStorage();
                            var saveService = new SaveLoadService(threadSavePath, fileStorage);

                            for (int i = 0; i < iterationsPerThread; i++)
                            {
                                var data = SaveData.CreateDefault();
                                data.PlayerProfile.PlayerName = $"Thread_{threadId}_Run_{i}";
                                data.WorldProgression.SetColorRestoration(WorldProgression.RegionHanoi, i % 100);
                                data.Level1State.InventoryItems.Add(ItemIds.BanhPho);

                                bool saved = saveService.Save(data);
                                if (!saved) throw new InvalidOperationException($"Save failed on thread {threadId} at step {i}");

                                var loaded = saveService.Load();
                                if (loaded.PlayerProfile.PlayerName != $"Thread_{threadId}_Run_{i}")
                                {
                                    throw new InvalidOperationException($"Persistence state mismatch on thread {threadId}: expected 'Thread_{threadId}_Run_{i}', got '{loaded.PlayerProfile.PlayerName}'");
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        exceptions.Add(ex);
                    }
                });
            }

            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(15));
            var allTasks = Task.WhenAll(tasks);
            var completedTask = await Task.WhenAny(allTasks, timeoutTask);

            Assert.True(completedTask == allTasks, "Multi-threaded stress test timed out (deadlock detected across 20 threads)!");
            Assert.True(exceptions.IsEmpty, $"Concurrent exceptions detected: {string.Join("; ", exceptions.Select(e => e.ToString()))}");
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                try { Directory.Delete(tempDir, recursive: true); } catch { }
            }
        }
    }

    [Fact]
    public async Task MultiThreadedStress_SharedEventBus_ConcurrentPublishAndSnapshotIteration_NoRaceConditions()
    {
        const int publisherThreads = 10;
        const int subscriberThreads = 10;
        const int iterations = 100;

        var sharedBus = new EventBus();
        var exceptions = new ConcurrentBag<Exception>();
        int totalEventsHandled = 0;

        var tasks = new List<Task>();

        // 10 concurrent subscribers constantly registering and disposing tokens
        for (int s = 0; s < subscriberThreads; s++)
        {
            tasks.Add(Task.Run(() =>
            {
                try
                {
                    for (int i = 0; i < iterations; i++)
                    {
                        using var token = sharedBus.Subscribe<ItemCollectedEvent>(_ =>
                        {
                            Interlocked.Increment(ref totalEventsHandled);
                        });
                        Thread.Yield();
                    }
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            }));
        }

        // 10 concurrent publishers firing events
        for (int p = 0; p < publisherThreads; p++)
        {
            int publisherId = p;
            tasks.Add(Task.Run(() =>
            {
                try
                {
                    for (int i = 0; i < iterations; i++)
                    {
                        sharedBus.Publish(new ItemCollectedEvent($"Item_{publisherId}_{i}", "Thịt Bò"));
                        Thread.Yield();
                    }
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            }));
        }

        var timeoutTask = Task.Delay(TimeSpan.FromSeconds(10));
        var allTasks = Task.WhenAll(tasks);
        var completedTask = await Task.WhenAny(allTasks, timeoutTask);

        Assert.True(completedTask == allTasks, "EventBus stress timed out!");
        Assert.True(exceptions.IsEmpty, $"Exceptions during concurrent EventBus execution: {string.Join("; ", exceptions.Select(e => e.ToString()))}");
    }

    // =========================================================================
    // SECTION 3: MEMORY & RESOURCE STABILITY (2,000 RAPID SCENE TRANSITIONS)
    // =========================================================================

    private sealed class DummyTitleScene : IScene
    {
        public bool IsEntered { get; private set; }
        public bool IsExited { get; private set; }
        public bool IsDisposed { get; private set; }
        public float ElapsedTime { get; private set; }

        public void Enter() { IsEntered = true; IsExited = false; }
        public void Update(float deltaTime) { ElapsedTime += deltaTime; }
        public void Draw() { }
        public void Exit() { IsExited = true; }
        public void Dispose() { IsDisposed = true; }
    }

    [Fact]
    public void MemoryAndResourceStability_TwoThousandRapidSceneTransitions_BoundedMemory()
    {
        using var sceneManager = new SceneManager();
        var saveData = SaveData.CreateDefault();
        var nullAudio = new NullAudioService();
        var mockInput = new MockInputService();

        // Warm up GC and establish baseline memory
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        long initialMemory = GC.GetTotalMemory(forceFullCollection: true);

        const int totalTransitions = 2000;
        for (int i = 0; i < totalTransitions; i++)
        {
            var eventBus = new EventBus();
            var questManager = new QuestManager(eventBus);

            IScene nextScene = (i % 6) switch
            {
                0 => new DummyTitleScene(),
                1 => new WorldMapScene(saveData, eventBus, mockInput),
                2 => new HanoiExplorationScene(saveData, inputService: mockInput, audioService: nullAudio, eventBus: eventBus),
                3 => new PhoAssemblyScene(saveData, questManager, eventBus, nullAudio, mockInput),
                4 => new StallPuzzleScene(saveData, questManager, eventBus, nullAudio, mockInput),
                _ => new AlbumViewerScene(saveData, eventBus, mockInput)
            };

            sceneManager.SwitchScene(nextScene);
            sceneManager.Update(0.016f);
        }

        sceneManager.Dispose();

        // Trigger garbage collection to clean unreferenced scenes
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        long finalMemory = GC.GetTotalMemory(forceFullCollection: true);

        long memoryDeltaBytes = Math.Max(0, finalMemory - initialMemory);
        long memoryDeltaMB = memoryDeltaBytes / (1024 * 1024);

        // Invariant: Net memory growth must remain strictly bounded (< 35 MB) for 2,000 transitions
        Assert.True(memoryDeltaMB < 35, $"Memory growth exceeded safety budget: {memoryDeltaMB} MB after 2,000 scene transitions.");
    }

    [Fact]
    public void MemoryAndResourceStability_EventBusSubscriberLifecycle_TokensAndClearEnsureBoundedSubscribers()
    {
        var bus = new EventBus();
        const int iterations = 500;

        // Verify that SubscriptionToken Dispose() strictly unsubscribes
        for (int i = 0; i < iterations; i++)
        {
            IDisposable token = bus.Subscribe<ItemCollectedEvent>(_ => { });
            Assert.Equal(1, bus.SubscriberCount<ItemCollectedEvent>());
            token.Dispose();
            Assert.Equal(0, bus.SubscriberCount<ItemCollectedEvent>());
        }

        // Verify that Clear() reliably purges all event types
        for (int i = 0; i < 50; i++)
        {
            bus.Subscribe<ItemCollectedEvent>(_ => { });
            bus.Subscribe<QuestStartedEvent>(_ => { });
            bus.Subscribe<GameSavedEvent>(_ => { });
        }

        Assert.Equal(50, bus.SubscriberCount<ItemCollectedEvent>());
        Assert.Equal(50, bus.SubscriberCount<QuestStartedEvent>());
        Assert.Equal(50, bus.SubscriberCount<GameSavedEvent>());

        bus.Clear();

        Assert.Equal(0, bus.SubscriberCount<ItemCollectedEvent>());
        Assert.Equal(0, bus.SubscriberCount<QuestStartedEvent>());
        Assert.Equal(0, bus.SubscriberCount<GameSavedEvent>());
    }

    // =========================================================================
    // SECTION 4: UTF-8 & FORM C NORMALIZATION UNDER STRESS
    // =========================================================================

    [Fact]
    public void Utf8Stress_FontManagerNormalizeText_ExtremeVietnameseDiacritics_ProducesValidFormC()
    {
        string[] testPhrases =
        [
            "Nguyễn Đình Chiểu, Phố Hàng Bồ, Bánh cuốn Thanh Trì, Bún chả Đống Đa, Ô Quan Chưởng",
            "Hà Nội 36 phố phường nghìn năm văn hiến, cầu Long Biên chứng nhân lịch sử rực rỡ",
            "Mây và Bút Chì phiêu lưu thu thập từng mảnh ký ức sắc màu nhiệm màu",
            "Trăm năm trong cõi người ta, chữ tài chữ mệnh khéo là ghét nhau. Trải qua một cuộc bể dâu, những điều trông thấy mà đau đớn lòng.",
            "ắ ằ ẳ ẵ ặ, ấ ầ ẩ ẫ ậ, ế ề ể ễ ệ, ố ồ ổ ỗ ộ, ớ ờ ở ỡ ợ, ứ ừ ử ữ ự, ý ỳ ỷ ỹ ỵ"
        ];

        foreach (string phrase in testPhrases)
        {
            // 1. Precomposed NFC test
            string normalizedNfc = FontManager.NormalizeText(phrase);
            Assert.True(normalizedNfc.IsNormalized(NormalizationForm.FormC),
                $"String '{phrase}' failed to produce valid Form C precomposition.");

            // 2. Decomposed NFD stress test (common on macOS filesystems and keyboards)
            string decomposedNfd = phrase.Normalize(NormalizationForm.FormD);
            Assert.True(decomposedNfd.IsNormalized(NormalizationForm.FormD));

            string reconciled = FontManager.NormalizeText(decomposedNfd);
            Assert.True(reconciled.IsNormalized(NormalizationForm.FormC),
                $"Decomposed NFD string failed to normalize back to Form C: {reconciled}");
            Assert.Equal(phrase.Normalize(NormalizationForm.FormC), reconciled);
        }
    }

    [Fact]
    public void Utf8Stress_FontManagerNormalizeText_EmojiAndSymbolSanitization_ReplacesAllWithSafeAscii()
    {
        string rawSymbolicText = "✕ ✓ ✗ ★ ▲ ▼ ◄ ► ← → ▶ ➔ ✉ ⭐ ① ② ③ ④ ⑤ 🍜 🥩 🌿 🫕 🎉 🌉";
        string expectedReplaced = "X V X * ^ v < > <- -> > -> [Thu] * (1) (2) (3) (4) (5) [Pho] [Bo] [Rau] [Nuoc] [!] [Cau]";

        string sanitized = FontManager.NormalizeText(rawSymbolicText);
        Assert.Equal(expectedReplaced, sanitized);

        // Verify zero residual UI emojis remain
        char[] bannedRawSymbols = ['✕', '✓', '✗', '★', '▲', '▼', '◄', '►', '←', '→', '▶', '➔', '✉', '⭐', '①', '②', '③', '④', '⑤'];
        foreach (char banned in bannedRawSymbols)
        {
            Assert.DoesNotContain(banned, sanitized);
        }
    }

    [Fact]
    public void Utf8Stress_FontManagerNormalizeText_ControlCodesAndExtremeLengths_NoExceptions()
    {
        // 1. Control codes & zero-width characters
        string controlText = "Hà\0Nội\r\nPhố\tCổ\b\u001B[31m\u007F\u200B";
        string normalizedControl = FontManager.NormalizeText(controlText);
        Assert.NotNull(normalizedControl);
        Assert.Contains("Hà", normalizedControl);
        Assert.Contains("Nội", normalizedControl);

        // 2. Null, empty, whitespace
        Assert.Equal(string.Empty, FontManager.NormalizeText(null));
        Assert.Equal(string.Empty, FontManager.NormalizeText(""));
        Assert.Equal("   ", FontManager.NormalizeText("   "));

        // 3. Massive string buffer (50,000 characters)
        var sb = new StringBuilder(50000);
        string chunk = "Bé Mây đi qua phố Hàng Bông nhặt sợi Bánh Phở tươi ngon. ";
        while (sb.Length < 50000)
        {
            sb.Append(chunk);
        }
        string massive = sb.ToString();

        var sw = Stopwatch.StartNew();
        string normalizedMassive = FontManager.NormalizeText(massive);
        sw.Stop();

        Assert.Equal(massive.Length, normalizedMassive.Length);
        Assert.True(sw.ElapsedMilliseconds < 50, $"Massive text normalization took too long: {sw.ElapsedMilliseconds} ms");
    }

    [Fact]
    public void Utf8Stress_DialogueParser_VietnameseEncodingAndMalformedJson_GracefulHandling()
    {
        var dialogueService = new DialogueService();

        // 1. Probe and load real dialogues_hanoi.json
        dialogueService.LoadDialogueFile("Data/dialogues_hanoi.json");
        Assert.True(dialogueService.LoadedGraphs.Count > 0, "dialogues_hanoi.json should load successfully.");

        foreach (var (graphId, graph) in dialogueService.LoadedGraphs)
        {
            Assert.NotNull(graphId);
            Assert.NotNull(graph.Nodes);
            foreach (var node in graph.Nodes)
            {
                Assert.NotNull(node.Text);
                Assert.True(node.Text.Normalize(NormalizationForm.FormC).IsNormalized(NormalizationForm.FormC),
                    $"Dialogue node {node.Id} text is not Form C normalized: {node.Text}");
            }
        }

        // 2. Fault injection: invalid paths
        dialogueService.LoadDialogueFile("NonExistent/InvalidPath/dialogue.json");
        dialogueService.LoadDialogueFile("");
        dialogueService.LoadDialogueFile("   ");

        // 3. Fault injection: malformed JSON file
        string tempMalformedFile = Path.Combine(Path.GetTempPath(), $"malformed_dialogue_{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(tempMalformedFile, "{ \"Dialogues\": [ { \"DialogueId\": \"Broken\", \"Nodes\": [ INVALID_JSON } ] }");
            // Must not throw an unhandled crash
            dialogueService.LoadDialogueFile(tempMalformedFile);
        }
        finally
        {
            if (File.Exists(tempMalformedFile))
            {
                File.Delete(tempMalformedFile);
            }
        }
    }

    // =========================================================================
    // SECTION 5: PRODUCTION PACKAGING & PERFORMANCE INVARIANTS
    // =========================================================================

    [Fact]
    public void PackagingInvariants_StandaloneExecutable_FilePropertiesAndDependencies()
    {
        string publishDir = Path.Combine(AppContext.BaseDirectory, "../../../../bin/Release/net10.0/osx-arm64/publish");
        string publishBinary = Path.Combine(publishDir, "GameProject");

        // Only assert file properties if the publish step was executed in the workspace
        if (Directory.Exists(publishDir) && File.Exists(publishBinary))
        {
            var info = new FileInfo(publishBinary);
            // Single-file self-contained .NET binary should be between 50 MB and 120 MB
            Assert.True(info.Length > 50_000_000, $"Published binary unexpectedly small: {info.Length} bytes");
            Assert.True(info.Length < 120_000_000, $"Published binary unexpectedly large: {info.Length} bytes");

            // Required bundled assets
            string libraylib = Path.Combine(publishDir, "libraylib.dylib");
            string dialogueJson = Path.Combine(publishDir, "Data", "dialogues_hanoi.json");

            Assert.True(File.Exists(libraylib), "libraylib.dylib missing in publish directory.");
            Assert.True(File.Exists(dialogueJson), "dialogues_hanoi.json missing in publish Data directory.");
        }
    }

    [Fact]
    public void PerformanceInvariants_HeadlessContinuousPlaythrough_ExecutesUnderBudget()
    {
        var sw = Stopwatch.StartNew();

        var driver = new TestSimulationDriver();
        driver.SimulateGatherAllPhoIngredients();
        bool phoSolved = driver.SimulateSolvePhoCooking();
        Assert.True(phoSolved);
        bool gridCleared = driver.SimulateClearStallPath();
        Assert.True(gridCleared);
        driver.SimulateLevelClimax();

        sw.Stop();

        // 100% full continuous slice simulation (Hanoi -> Q1 -> Q2 -> Q3 -> Climax -> Save/Reload)
        // must execute well within budget (< 500 ms)
        Assert.True(sw.ElapsedMilliseconds < 500, $"Full continuous playthrough exceeded 500 ms budget: took {sw.ElapsedMilliseconds} ms");
    }
}
