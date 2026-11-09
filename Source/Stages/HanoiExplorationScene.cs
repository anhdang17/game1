using System;
using System.Collections.Generic;
using System.Numerics;
using GameProject.Audio;
using GameProject.Core.Dialogue;
using GameProject.Core.Events;
using GameProject.Core.Hints;
using GameProject.Core.Interactions;
using GameProject.Core.Models;
using GameProject.Core.Movement;
using GameProject.Core.Ports;
using GameProject.Core.Quests;
using GameProject.Core.Spatial;
using GameProject.Core.State;
using GameProject.Graphics;
using GameProject.UI;
using Raylib_cs;

namespace GameProject.Stages;

/// <summary>
/// Exploration stage recreating the Hanoi Old Quarter (Phố Hàng Buồm, Lãn Ông).
/// Integrates character kinematics, collision sliding, 2D camera tracking, dynamic Y-sorting,
/// interaction bubble prompts, market merchants, Quest 1 gathering loop, and post-processing bloom.
/// </summary>
public sealed class HanoiExplorationScene : IScene
{
    // Domain Services & State
    public SaveData SaveData { get; }
    public Inventory Inventory { get; }
    public QuestManager QuestManager { get; }
    public QuestFlags QuestFlags => SaveData.Level1State.QuestFlags;
    public GameStateManager StateManager { get; }
    public IInputService InputService { get; }
    public IAudioService AudioService { get; }
    public IEventBus EventBus { get; }
    public IDialogueService DialogueService { get; }
    public DialogueBox DialogueBox { get; }
    public IHintService HintService { get; }
    public ButChiGuideUI ButChiGuide { get; }

    // Spatial & Kinematics
    public CollisionWorld CollisionWorld { get; }
    public CharacterKinematics Kinematics { get; }
    public Camera2DTracker CameraTracker { get; }
    public InteractionManager InteractionManager { get; }

    // Visual & UI
    public ColorBloomEffect BloomEffect { get; }
    public InventoryBar InventoryBar { get; }
    public QuestHUD QuestHUD { get; }

    // World Entities
    public BaAnNpc BaAn { get; }
    public IReadOnlyList<PhoIngredientMerchant> Merchants { get; }
    public LongBienGateInteractable LongBienGate { get; } = new();

    // Scene & Transition State
    public bool RequestWorldMap { get; set; }
    private PhoAssemblyScene? _phoAssemblyModal;
    private StallPuzzleScene? _stallPuzzleModal;
    private AlbumViewerScene? _albumViewerModal;
    private readonly GrayShadowLetterModal _grayShadowLetterModal = new();

    // Scenery props
    public Vector2 FlowerBicyclePos { get; } = new(180f, 480f);
    public Vector2 VintageCycloPos { get; } = new(920f, 360f);
    public Vector2 TablePos1 { get; } = new(280f, 360f);
    public Vector2 TablePos2 { get; } = new(520f, 350f);

    private float _totalTime;
    private InteractionResult? _activeDialogue;

    public HanoiExplorationScene(
        SaveData? saveData = null,
        Inventory? inventory = null,
        QuestManager? questManager = null,
        GameStateManager? stateManager = null,
        IInputService? inputService = null,
        IAudioService? audioService = null,
        IEventBus? eventBus = null,
        IDialogueService? dialogueService = null,
        IHintService? hintService = null)
    {
        EventBus = eventBus ?? new EventBus();
        SaveData = saveData ?? SaveData.CreateDefault();
        Inventory = inventory ?? new Inventory(SaveData.Level1State.InventoryItems);

        QuestManager = questManager ?? new QuestManager(EventBus);
        if (questManager == null || QuestManager.GetQuest(HanoiQuests.Quest1Id) == null)
        {
            HanoiQuests.RegisterAll(QuestManager);
        }
        QuestManager.HydrateFromFlags(QuestFlags);

        StateManager = stateManager ?? new GameStateManager(EventBus, GameProject.Core.State.GameState.Exploration);
        InputService = inputService ?? new RaylibInputService();
        AudioService = audioService ?? (Raylib.IsWindowReady() ? new RaylibAudioService() : new NullAudioService());

        DialogueService = dialogueService ?? new DialogueService(EventBus);
        DialogueService.LoadDialogueFile("Data/dialogues_hanoi.json");
        DialogueService.OnActionTriggered += HandleDialogueAction;
        DialogueBox = new DialogueBox(AudioService);
        HintService = hintService ?? new HintManager();
        ButChiGuide = new ButChiGuideUI(HintService, AudioService, InputService);
        EventBus.Subscribe<ItemCollectedEvent>(_ => ButChiGuide.NotifyProgress());

        // 1. Setup Collision World & Map Bounds
        CollisionWorld = new CollisionWorld
        {
            MapBounds = new AABB(0, 0, 1600, 900)
        };

        // 2. Setup Kinematics
        Kinematics = new CharacterKinematics
        {
            Position = new Vector2((float)SaveData.Level1State.PlayerSpawnX, (float)SaveData.Level1State.PlayerSpawnY),
            MaxSpeed = 200.0f
        };

        // 3. Setup Camera Tracker
        CameraTracker = new Camera2DTracker
        {
            ViewportSize = new Vector2(1280, 720),
            MapBounds = CollisionWorld.MapBounds
        };
        CameraTracker.SnapToTarget(Kinematics.Position);

        // 4. Setup Interactables & Factory
        var (baAn, merchants) = HanoiInteractableFactory.CreateHanoiInteractables();
        BaAn = baAn;
        Merchants = merchants;

        InteractionManager = new InteractionManager();
        InteractionManager.Register(BaAn);
        InteractionManager.Register(LongBienGate);
        for (int i = 0; i < Merchants.Count; i++)
        {
            InteractionManager.Register(Merchants[i]);
        }

        // 5. Register Static Obstacle Colliders
        RegisterObstacles();

        // 6. Presentation Components
        BloomEffect = new ColorBloomEffect();
        InventoryBar = new InventoryBar(EventBus);
        QuestHUD = new QuestHUD(EventBus);

        // Sync items collected to SaveData
        EventBus.Subscribe<ItemCollectedEvent>(ev =>
        {
            if (!SaveData.Level1State.InventoryItems.Contains(ev.ItemId))
            {
                SaveData.Level1State.InventoryItems.Add(ev.ItemId);
            }
        });
    }

    private void RegisterObstacles()
    {
        CollisionWorld.ClearObstacles();

        // Level borders
        CollisionWorld.AddObstacle(new AABB(0, 0, 1600, 110)); // Top building wall line
        CollisionWorld.AddObstacle(new AABB(0, 850, 1600, 900)); // Bottom curb

        // Market Stalls static foot collision boxes
        CollisionWorld.AddObstacle(AABB.FromCenterAndExtents(BaAn.Position + new Vector2(0, -10), new Vector2(36, 14)));
        for (int i = 0; i < Merchants.Count; i++)
        {
            var m = Merchants[i];
            CollisionWorld.AddObstacle(AABB.FromCenterAndExtents(m.Position + new Vector2(0, -10), new Vector2(36, 14)));
        }

        // Scenery props colliders
        CollisionWorld.AddObstacle(AABB.FromCenterAndExtents(TablePos1 + new Vector2(0, -8), new Vector2(20, 10)));
        CollisionWorld.AddObstacle(AABB.FromCenterAndExtents(TablePos2 + new Vector2(0, -8), new Vector2(20, 10)));
        CollisionWorld.AddObstacle(AABB.FromCenterAndExtents(FlowerBicyclePos + new Vector2(0, -8), new Vector2(24, 10)));
        CollisionWorld.AddObstacle(AABB.FromCenterAndExtents(VintageCycloPos + new Vector2(0, -8), new Vector2(28, 12)));

        // Long Bien Bridge Gate barrier when locked
        if (!QuestFlags.Gate_LongBien_Unlocked)
        {
            CollisionWorld.AddObstacle(AABB.FromCenterAndExtents(new Vector2(1480f, 400f), new Vector2(30f, 300f)));
        }
    }

    public void Enter()
    {
        FontManager.Instance.Initialize();
        BloomEffect.Initialize(1280, 720);

        // Start Hanoi theme if audio ready
        AudioService.PlayMusic(MusicTrack.HanoiStreetsTheme);

        DialogueService.LoadDialogueFile("Data/dialogues_hanoi.json");
        DialogueService.OnActionTriggered -= HandleDialogueAction;
        DialogueService.OnActionTriggered += HandleDialogueAction;
        ButChiGuide.ResetIdleTimer();

        // Sync initial quest flags
        if (QuestFlags.Q3_Completed || QuestFlags.Gate_LongBien_Unlocked)
        {
            BloomEffect.SetColorPercent(100f);
            RegisterObstacles();
        }
        else if (QuestFlags.Q2_Completed)
        {
            BloomEffect.SetColorPercent(33.3f);
        }
        else if (QuestFlags.Q1_Completed)
        {
            BloomEffect.SetColorPercent(0f);
        }
        else
        {
            BloomEffect.SetColorPercent(0f); // Start monochrome
        }
    }

    private void HandleDialogueAction(string action)
    {
        switch (action)
        {
            case "Start_Quest_1":
                if (!QuestFlags.Q1_Started)
                {
                    QuestManager.StartQuest(HanoiQuests.Quest1Id);
                    QuestFlags.Q1_Started = true;
                }
                break;
            case "Complete_Quest_1":
                if (QuestFlags.Q1_Started && !QuestFlags.Q1_Completed)
                {
                    QuestManager.CompleteQuest(HanoiQuests.Quest1Id);
                    QuestFlags.Q1_Completed = true;
                }
                break;
            case "Collect_BanhPho":
                Inventory.AddItem(ItemIds.BanhPho);
                QuestFlags.Q1_BanhPho_Collected = true;
                break;
            case "Collect_ThitBo":
                Inventory.AddItem(ItemIds.ThitBo);
                QuestFlags.Q1_ThitBo_Collected = true;
                break;
            case "Collect_NuocDung":
                Inventory.AddItem(ItemIds.NuocDung);
                QuestFlags.Q1_NuocDung_Collected = true;
                break;
            case "Collect_HanhLa":
                Inventory.AddItem(ItemIds.HanhLa);
                QuestFlags.Q1_HanhLa_Collected = true;
                break;
            case "Collect_ThaoMoc":
                Inventory.AddItem(ItemIds.ThaoMoc);
                QuestFlags.Q1_ThaoMoc_Collected = true;
                break;
            case "Open_World_Map":
                RequestWorldMap = true;
                break;
        }
    }

    public void Update(float deltaTime)
    {
        if (deltaTime <= 0f) return;
        _totalTime += deltaTime;

        // 1. Process Input Service
        InputService.Update(deltaTime);

        // 2. Mini-Game, Album & Cutscene Modal Overlays
        if (StateManager.CurrentState == GameState.Album)
        {
            _albumViewerModal ??= new AlbumViewerScene(SaveData, EventBus, InputService);
            _albumViewerModal.Update(deltaTime);
            if (_albumViewerModal.RequestClose)
            {
                StateManager.PopState();
            }
            return;
        }

        if (InputService.IsAlbumJustPressed && StateManager.CanOpenAlbum)
        {
            _albumViewerModal ??= new AlbumViewerScene(SaveData, EventBus, InputService);
            _albumViewerModal.Enter();
            StateManager.PushState(GameState.Album);
            return;
        }

        if (StateManager.CurrentState == GameState.MiniGamePho && _phoAssemblyModal != null)
        {
            _phoAssemblyModal.Update(deltaTime);
            if (_phoAssemblyModal.RequestExit || _phoAssemblyModal.RequestNextScene)
            {
                if (_phoAssemblyModal.IsSolved)
                {
                    BloomEffect.TriggerWave(BaAn.Position, 33.3f, 2.0f);
                    AudioService.PlaySfx(SfxType.ColorBloomWave);
                }
                StateManager.PopState();
            }
            return;
        }

        if (StateManager.CurrentState == GameState.MiniGameStalls && _stallPuzzleModal != null)
        {
            _stallPuzzleModal.Update(deltaTime);
            if (_stallPuzzleModal.RequestNextScene)
            {
                StateManager.PopState();
                BloomEffect.TriggerWave(new Vector2(1480f, 480f), 100f, 2.5f);
                AudioService.PlaySfx(SfxType.ColorBloomWave);
                AudioService.PlaySfx(SfxType.GateUnlocked);
                RegisterObstacles();
            }
            return;
        }

        if (StateManager.CurrentState == GameState.Cutscene)
        {
            _grayShadowLetterModal.Update(deltaTime,
                isConfirmPressed: InputService.IsConfirmJustPressed,
                isSkipPressed: InputService.IsCancelJustPressed);
            if (_grayShadowLetterModal.RequestClose)
            {
                SaveData.Level1State.QuestFlags.GrayShadow_LetterRead = true;
                EventBus.Publish(new CutsceneEndedEvent("GrayShadow_Letter"));
                EventBus.Publish(new LetterOpenedEvent("Letter_GrayShadow_Hanoi", "Bóng Xám"));
                StateManager.PopState();
            }
            return;
        }

        // Companion Guide Update
        if (InputService.MovementDirection != Vector2.Zero)
        {
            ButChiGuide.ResetIdleTimer();
        }
        string activeQuest = SaveData.Level1State.ActiveQuestId ?? (QuestManager.GetQuestsByState(QuestState.InProgress).FirstOrDefault()?.Id ?? QuestIds.Hanoi01);
        ButChiGuide.Update(deltaTime, activeQuest, SaveData.Level1State);

        // 3. Build Interaction Context
        var context = new InteractionContext(
            PlayerPosition: Kinematics.Position,
            Inventory: Inventory,
            QuestManager: QuestManager,
            QuestFlags: QuestFlags,
            EventBus: EventBus,
            AudioService: AudioService
        );

        // 4. Handle Active Dialogue Dismissal / Progression
        if (DialogueBox.IsActive || _activeDialogue != null)
        {
            DialogueBox.Update(deltaTime, InputService);

            bool inputDismiss = InputService.IsInteractJustPressed || InputService.IsConfirmJustPressed || InputService.IsCancelJustPressed;
            bool choiceWaiting = DialogueBox.CurrentBranches.Count > 0 && !DialogueBox.RequestClose;

            if (DialogueBox.RequestClose || !DialogueBox.IsActive || (inputDismiss && !choiceWaiting))
            {
                AudioService.PlaySfx(SfxType.ButtonClick);
                string speaker = _activeDialogue?.DialogueSpeaker ?? DialogueBox.CurrentSpeaker;
                var currentTarget = InteractionManager.CurrentTarget;
                _activeDialogue = null;
                DialogueBox.Dismiss();

                // Launch Pho Assembly after completing Q1 dialogue with Ba An
                if ((speaker == BaAn.DisplayName || speaker == "Bà An") &&
                    QuestFlags.Q1_Completed && !QuestFlags.Q2_Completed)
                {
                    _phoAssemblyModal ??= new PhoAssemblyScene(SaveData, QuestManager, EventBus, AudioService, InputService);
                    _phoAssemblyModal.Enter();
                    StateManager.PushState(GameState.MiniGamePho);
                }
                else if (currentTarget is LongBienGateInteractable)
                {
                    if (QuestFlags.Q2_Completed && !QuestFlags.Q3_Completed)
                    {
                        _stallPuzzleModal ??= new StallPuzzleScene(SaveData, QuestManager, EventBus, AudioService, InputService);
                        _stallPuzzleModal.Enter();
                        StateManager.PushState(GameState.MiniGameStalls);
                    }
                    else if (QuestFlags.Gate_LongBien_Unlocked)
                    {
                        if (!QuestFlags.GrayShadow_LetterRead)
                        {
                            _grayShadowLetterModal.Show();
                            StateManager.PushState(GameState.Cutscene);
                        }
                        else
                        {
                            RequestWorldMap = true;
                        }
                    }
                }
            }
            return;
        }

        // 5. Update Movement & Kinematics when not in modal dialogue
        Kinematics.Update(InputService.MovementDirection, CollisionWorld, deltaTime);

        if (Kinematics.CheckFootstepTrigger())
        {
            AudioService.PlaySfx(SfxType.StepStreet, 0.4f);
        }

        // 6. Update Interaction Manager
        InteractionManager.Update(deltaTime, Kinematics.Position, context);

        if (InputService.IsInteractJustPressed)
        {
            var target = InteractionManager.CurrentTarget;
            var result = InteractionManager.TryInteract(context);
            if (result != null)
            {
                _activeDialogue = result;
                string? graphId = GetDialogueGraphIdForTarget(target);
                if (graphId != null && DialogueService.LoadedGraphs.ContainsKey(graphId))
                {
                    DialogueService.StartDialogue(graphId);
                }
                DialogueBox.Show(result.DialogueSpeaker, result.DialogueText, GetAvatarForSpeaker(result.DialogueSpeaker));

                // Trigger color wave if quest completed
                if (result.QuestCompleted)
                {
                    BloomEffect.TriggerWave(BaAn.Position, 33.3f, 2.0f);
                }
            }
        }

        // 6. Update Camera Tracker
        Vector2 cameraFocus = Kinematics.Position + new Vector2(0, -20);
        CameraTracker.Update(cameraFocus, deltaTime);

        // 7. Update UI HUD
        InventoryBar.Update(deltaTime, Inventory, InputService.PointerPosition, 1280);
        QuestHUD.Update(deltaTime, QuestManager, QuestFlags);

        // 8. Update Bloom Shaders
        BloomEffect.Update(deltaTime);
    }

    private sealed class RenderableEntity : IYSortable
    {
        public string EntityId { get; init; } = "";
        public float FootY { get; init; }
        public int SubLayerPriority { get; init; }
        public Action<float> DrawAction { get; init; } = _ => { };
    }

    public void Draw()
    {
        // Begin full-screen render pass through color restoration bloom shader
        BloomEffect.BeginRender();

        Camera2D rayCamera = new()
        {
            Target = CameraTracker.CurrentPosition,
            Offset = new Vector2(1280f * 0.5f, 720f * 0.5f),
            Rotation = 0f,
            Zoom = CameraTracker.Zoom
        };

        Raylib.BeginMode2D(rayCamera);

        // --- 1. Draw Street Environment & Buildings ---
        DrawStreetEnvironment();

        // --- 2. Dynamic Y-Sorting Entity Queue ---
        var yQueue = new YSortQueue<RenderableEntity>();

        // Player components
        Vector2 playerPos = Kinematics.Position;
        yQueue.Add(new RenderableEntity
        {
            EntityId = "Player_Shadow",
            FootY = playerPos.Y,
            SubLayerPriority = -1,
            DrawAction = _ => Raylib.DrawEllipse((int)playerPos.X, (int)playerPos.Y, 16f, 8f, Raylib.ColorAlpha(Color.Black, 0.28f))
        });
        yQueue.Add(new RenderableEntity
        {
            EntityId = "Player_BeMay",
            FootY = playerPos.Y,
            SubLayerPriority = 0,
            DrawAction = t => VectorRenderer.DrawBeMay(playerPos, Kinematics.Facing, t, Kinematics.IsMoving)
        });
        yQueue.Add(new RenderableEntity
        {
            EntityId = "Player_Companion_ButChi",
            FootY = playerPos.Y,
            SubLayerPriority = 1,
            DrawAction = t => VectorRenderer.DrawButChi(playerPos + new Vector2(24f, -34f), t)
        });

        // Bà An NPC & Stall
        yQueue.Add(new RenderableEntity
        {
            EntityId = BaAn.Id,
            FootY = BaAn.Position.Y,
            SubLayerPriority = 0,
            DrawAction = _ => VectorRenderer.DrawMarketStall(BaAn.Position, "Quán Phở Bà An", HanoiPalette.TerracottaRoof, HanoiPalette.WallYellow)
        });

        // 5 Market Merchants
        for (int i = 0; i < Merchants.Count; i++)
        {
            var m = Merchants[i];
            Color c1 = (i % 2 == 0) ? HanoiPalette.ShutterGreen : HanoiPalette.TerracottaRoof;
            Color c2 = (i % 2 == 0) ? HanoiPalette.WallYellowLight : HanoiPalette.WallYellow;
            yQueue.Add(new RenderableEntity
            {
                EntityId = m.Id,
                FootY = m.Position.Y,
                SubLayerPriority = 0,
                DrawAction = _ => VectorRenderer.DrawMarketStall(m.Position, m.DisplayName, c1, c2)
            });
        }

        // Street props
        yQueue.Add(new RenderableEntity
        {
            EntityId = "Prop_Bicycle",
            FootY = FlowerBicyclePos.Y,
            SubLayerPriority = 0,
            DrawAction = t => VectorRenderer.DrawFlowerBicycle(FlowerBicyclePos, t)
        });
        yQueue.Add(new RenderableEntity
        {
            EntityId = "Prop_Cyclo",
            FootY = VintageCycloPos.Y,
            SubLayerPriority = 0,
            DrawAction = _ => VectorRenderer.DrawVintageCyclo(VintageCycloPos)
        });
        yQueue.Add(new RenderableEntity
        {
            EntityId = "Prop_Table1",
            FootY = TablePos1.Y,
            SubLayerPriority = 0,
            DrawAction = _ => VectorRenderer.DrawBluePlasticTableAndStools(TablePos1)
        });
        yQueue.Add(new RenderableEntity
        {
            EntityId = "Prop_Table2",
            FootY = TablePos2.Y,
            SubLayerPriority = 0,
            DrawAction = _ => VectorRenderer.DrawBluePlasticTableAndStools(TablePos2)
        });
        yQueue.Add(new RenderableEntity
        {
            EntityId = LongBienGate.Id,
            FootY = LongBienGate.Position.Y,
            SubLayerPriority = 0,
            DrawAction = t => DrawLongBienGateEntity(LongBienGate.Position, t)
        });

        // Sort entities deterministically and render
        yQueue.Sort();
        for (int i = 0; i < yQueue.Count; i++)
        {
            yQueue.Entities[i].DrawAction(_totalTime);
        }

        // --- 3. Interaction Prompt Bubble (Drawn above target in world space) ---
        var target = InteractionManager.CurrentTarget;
        if (target != null && InteractionManager.BubbleAlpha > 0.01f)
        {
            var ctx = new InteractionContext(
                Kinematics.Position,
                Inventory,
                QuestManager,
                QuestFlags,
                EventBus,
                AudioService
            );
            string promptText = target.GetPromptText(ctx);
            string actionText = promptText.Replace("[E] ", "").Trim();
            InteractionBubbleUI.Draw(target.InteractionCenter, target.VisualHeight, actionText, InteractionManager.BubbleAlpha, InteractionManager.BubbleBobTimer);
        }

        Raylib.EndMode2D();

        // End color restoration framebuffer pass and draw to screen
        BloomEffect.EndRenderAndDraw();

        // --- 4. Screen-Space UI Passes ---
        InventoryBar.Draw();
        QuestHUD.Draw(QuestFlags, 1280);
        ButChiGuide.Draw(1280, 720);

        // --- 5. Modal Screen Overlays ---
        if (StateManager.CurrentState == GameState.Album && _albumViewerModal != null)
        {
            _albumViewerModal.Draw();
        }
        else if (StateManager.CurrentState == GameState.MiniGamePho && _phoAssemblyModal != null)
        {
            _phoAssemblyModal.Draw();
        }
        else if (StateManager.CurrentState == GameState.MiniGameStalls && _stallPuzzleModal != null)
        {
            _stallPuzzleModal.Draw();
        }
        else if (StateManager.CurrentState == GameState.Cutscene)
        {
            _grayShadowLetterModal.Draw();
        }
        else if (DialogueBox.IsActive)
        {
            DialogueBox.Draw();
        }
        else if (_activeDialogue != null)
        {
            DrawDialogueModal(_activeDialogue);
        }
    }

    private static string GetAvatarForSpeaker(string speaker)
    {
        if (speaker.Contains("Bà")) return "Avatar_BaAn";
        if (speaker.Contains("Mây")) return "Avatar_May";
        if (speaker.Contains("Bút Chì")) return "Avatar_ButChi";
        if (speaker.Contains("Bác Tư")) return "Avatar_BacTu";
        if (speaker.Contains("Chú Long")) return "Avatar_ChuLong";
        if (speaker.Contains("Cô Lan")) return "Avatar_CoLan";
        if (speaker.Contains("Lãn Ông")) return "Avatar_ThaoMoc";
        return "Avatar_Merchant";
    }

    private string? GetDialogueGraphIdForTarget(IInteractable? target)
    {
        if (target is BaAnNpc)
        {
            if (QuestFlags.Q1_Completed && !QuestFlags.Q2_Completed) return "Dialogue_BaAn_TurnIn";
            if (QuestFlags.Q2_Completed) return "Dialogue_BaAn_PostPho";
            if (QuestFlags.Q1_Started) return "Dialogue_BaAn_InProgress";
            return "Dialogue_BaAn_Intro";
        }
        if (target is PhoIngredientMerchant stall)
        {
            return stall.ItemId switch
            {
                ItemIds.BanhPho => "Dialogue_Stall_BanhPho",
                ItemIds.ThitBo => "Dialogue_Stall_ThitBo",
                ItemIds.NuocDung => "Dialogue_Stall_NuocDung",
                ItemIds.HanhLa => "Dialogue_Stall_HanhLa",
                ItemIds.ThaoMoc => "Dialogue_Stall_ThaoMoc",
                _ => null
            };
        }
        if (target is LongBienGateInteractable)
        {
            return "Dialogue_Gate_LongBien";
        }
        return null;
    }

    private void DrawLongBienGateEntity(Vector2 pos, float animTime)
    {
        if (!QuestFlags.Gate_LongBien_Unlocked)
        {
            // Barricade: Wooden sawhorse barrier
            Rectangle sawhorse = new(pos.X - 30f, pos.Y - 18f, 60f, 22f);
            VectorRenderer.DrawRoundedRect(sawhorse, 0.2f, new Color(175, 120, 70, 255), HanoiPalette.CartoonOutline, 2f, withShadow: false);
            // Caution stripes
            for (int s = -20; s <= 20; s += 14)
            {
                Raylib.DrawLineEx(new Vector2(pos.X + s, pos.Y - 14f), new Vector2(pos.X + s + 6f, pos.Y + 2f), 3f, new Color(255, 220, 60, 255));
            }
            // Vector Padlock icon
            Rectangle lockBody = new(pos.X - 7f, pos.Y - 32f, 14f, 12f);
            VectorRenderer.DrawRoundedRect(lockBody, 0.2f, new Color(255, 215, 64, 255), HanoiPalette.CartoonOutline, 1.2f, withShadow: false);
            Raylib.DrawCircleLines((int)pos.X, (int)pos.Y - 33, 4.5f, HanoiPalette.CartoonOutline);
        }
        else
        {
            // Unlocked: Glowing cyan/gold portal aura with animated expanding ripple rings
            float pulse = 0.5f + 0.5f * MathF.Sin(animTime * 3.5f);
            Raylib.DrawCircleV(pos, 26f + pulse * 6f, Raylib.ColorAlpha(new Color(100, 220, 255, 255), 0.35f));
            Raylib.DrawCircleLines((int)pos.X, (int)pos.Y, 32f + pulse * 8f, new Color(255, 230, 100, 200));

            // Floating lotus sparks
            for (int p = 0; p < 4; p++)
            {
                float a = p * (MathF.PI * 2f / 4f) + animTime * 1.5f;
                Vector2 sp = pos + new Vector2(MathF.Cos(a) * 22f, MathF.Sin(a) * 14f - 10f);
                Raylib.DrawCircleV(sp, 2.5f, new Color(255, 182, 193, 230));
            }

            FontManager.Instance.DrawTextOutline("Cổng Cầu Long Biên", new Vector2(pos.X - 55f, pos.Y - 48f), 12f,
                HanoiPalette.WallYellow, HanoiPalette.CartoonOutline, 1.5f);
        }
    }

    private void DrawStreetEnvironment()
    {
        // Ground stone cobblestones
        Raylib.ClearBackground(HanoiPalette.StoneStreet);

        // Grid lines for cobblestone pavement
        for (int x = 0; x < 1600; x += 64)
        {
            Raylib.DrawLine(x, 110, x, 850, HanoiPalette.StoneMortar);
        }
        for (int y = 110; y < 850; y += 48)
        {
            Raylib.DrawLine(0, y, 1600, y, HanoiPalette.StoneMortar);
        }

        // Top curbs & colonial facades
        Rectangle facade = new(0, 0, 1600, 110);
        Raylib.DrawRectangleRec(facade, HanoiPalette.WallYellow);
        Raylib.DrawRectangleLinesEx(facade, 2f, HanoiPalette.CartoonOutline);

        // Roof overhang
        Rectangle roof = new(0, 0, 1600, 36);
        Raylib.DrawRectangleRec(roof, HanoiPalette.TerracottaRoof);
        Raylib.DrawLineEx(new Vector2(0, 36), new Vector2(1600, 36), 3f, HanoiPalette.TerracottaDark);

        // Green shutters on upper facade
        for (int sx = 80; sx < 1550; sx += 180)
        {
            Rectangle shutter = new(sx, 48, 48, 52);
            VectorRenderer.DrawRoundedRect(shutter, 0.2f, HanoiPalette.ShutterGreen, HanoiPalette.CartoonOutline, 1.5f, withShadow: false);
            Raylib.DrawLineEx(new Vector2(sx + 6, 62), new Vector2(sx + 42, 62), 1.5f, HanoiPalette.ShutterGreenDark);
            Raylib.DrawLineEx(new Vector2(sx + 6, 74), new Vector2(sx + 42, 74), 1.5f, HanoiPalette.ShutterGreenDark);
            Raylib.DrawLineEx(new Vector2(sx + 6, 86), new Vector2(sx + 42, 86), 1.5f, HanoiPalette.ShutterGreenDark);
        }

        // Bottom sidewalk curb
        Rectangle bottomCurb = new(0, 850, 1600, 50);
        Raylib.DrawRectangleRec(bottomCurb, HanoiPalette.StoneCurb);
        Raylib.DrawLineEx(new Vector2(0, 850), new Vector2(1600, 850), 3f, HanoiPalette.CartoonOutline);

        // Distant Long Biên Bridge steel trusses silhouette on right edge
        Vector2[] girderPoints =
        [
            new(1350, 110), new(1420, 40), new(1490, 110),
            new(1490, 110), new(1560, 40), new(1600, 110)
        ];
        for (int i = 0; i < girderPoints.Length - 1; i += 3)
        {
            Raylib.DrawLineEx(girderPoints[i], girderPoints[i + 1], 4f, HanoiPalette.RustedIron);
            Raylib.DrawLineEx(girderPoints[i + 1], girderPoints[i + 2], 4f, HanoiPalette.RustedIron);
            Raylib.DrawLineEx(girderPoints[i], girderPoints[i + 2], 3f, HanoiPalette.RustedIronPatina);
        }
    }

    private static void DrawDialogueModal(InteractionResult result)
    {
        float boxW = 820f;
        float boxH = 124f;
        float boxX = (1280f - boxW) * 0.5f;
        float boxY = 566f;

        Rectangle dialogRec = new(boxX, boxY, boxW, boxH);
        VectorRenderer.DrawRoundedRect(dialogRec, 0.25f, new Color(254, 252, 246, 250), HanoiPalette.CartoonOutline, 2.0f);

        // Speaker Name Badge
        Vector2 nameSz = FontManager.Instance.MeasureText(result.DialogueSpeaker, 14f);
        Rectangle nameBadge = new(boxX + 20f, boxY - 14f, nameSz.X + 24f, 28f);
        VectorRenderer.DrawRoundedRect(nameBadge, 0.35f, HanoiPalette.WallYellow, HanoiPalette.CartoonOutline, 1.5f, withShadow: false);
        FontManager.Instance.DrawText(result.DialogueSpeaker, new Vector2(nameBadge.X + 12f, nameBadge.Y + 6f), 14f, HanoiPalette.CartoonOutline);

        // Dialogue text
        FontManager.Instance.DrawText(result.DialogueText, new Vector2(boxX + 24f, boxY + 24f), 15f, new Color(35, 30, 28, 255));

        // Advance prompt
        string advanceHint = "[Phím Trắng / Enter] Tiếp tục";
        Vector2 hintSz = FontManager.Instance.MeasureText(advanceHint, 12f);
        FontManager.Instance.DrawText(advanceHint, new Vector2(boxX + boxW - hintSz.X - 20f, boxY + boxH - hintSz.Y - 10f), 12f, new Color(130, 110, 95, 255));
    }

    public void Exit()
    {
        AudioService.StopMusic();
    }

    public void Dispose()
    {
        BloomEffect.Dispose();
    }
}
