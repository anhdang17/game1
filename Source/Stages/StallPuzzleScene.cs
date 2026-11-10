using System;
using System.Collections.Generic;
using System.Numerics;
using GameProject.Core.Events;
using GameProject.Core.Hints;
using GameProject.Core.Models;
using GameProject.Core.Ports;
using GameProject.Core.Puzzles;
using GameProject.Core.Quests;
using GameProject.Core.State;
using GameProject.Graphics;
using GameProject.UI;
using Raylib_cs;

namespace GameProject.Stages;

/// <summary>
/// Quest 3: Street Stall Sliding Puzzle — push carts and stalls on a 7x5 spatial grid to clear a continuous path
/// from street entrance (column 0, row 2) to the Long Biên bridge gate (column 6, row 2).
/// Features real-time BFS golden path ribbon, 100% regional color bloom wave, Lotus Stamp and Journal Page rewards,
/// and the Gray Shadow letter cutscene.
/// </summary>
public sealed class StallPuzzleScene : IScene
{
    private readonly SaveData _saveData;
    private readonly QuestManager _questManager;
    private readonly IEventBus _eventBus;
    private readonly IAudioService? _audioService;
    private readonly IInputService? _inputService;
    private readonly LevelProgressionService _progression;
    private readonly SpatialGridEngine _grid = new();
    private readonly GrayShadowLetterModal _letterModal = new();
    private readonly Dictionary<string, Vector2> _visualPositions = new();
    private readonly ParticleEmitter _particles = new();

    // UI state
    private string? _selectedObjectId;
    private bool _pathClear;
    private float _totalTime;
    private bool _solved;
    private float _solveTimer;
    private int _moveCount;
    private string? _hintMessage;
    private float _hintTimer;
    private float _bloomProgress;
    private Vector2? _dragStartPos;
    private string? _dragEntityId;

    public bool IsSolved => _solved;
    public int MoveCount => _moveCount;
    public bool RequestNextScene { get; private set; }
    public bool RequestExit { get; private set; }

    // Grid rendering constants
    private const float GridOriginX = 160f;
    private const float GridOriginY = 140f;
    private const float CellSize = 88f;

    // Colors for entity types (Obj_FruitStall corrected from Obj_PhoStall)
    private static readonly Dictionary<string, Color> EntityColors = new()
    {
        { "Obj_FlowerCart",    new Color(255, 182, 193, 255) }, // soft pink
        { "Obj_FruitStall",    new Color(255, 200, 80, 255) },  // golden orange-yellow
        { "Obj_Cyclo",         new Color(43,  76,  72,  255) }, // teal
        { "Obj_TeaStall",      new Color(180, 230, 180, 255) }, // mint green
        { "Obj_CeramicCrates", new Color(205, 149,  85, 255) }, // clay brown
    };

    private static readonly Dictionary<string, string> EntityLabels = new()
    {
        { "Obj_FlowerCart",    "Xe hoa" },
        { "Obj_FruitStall",    "Sạp hoa quả" },
        { "Obj_Cyclo",         "Xích lô" },
        { "Obj_TeaStall",      "Quầy chè" },
        { "Obj_CeramicCrates", "Thùng gốm" },
    };

    public StallPuzzleScene(
        SaveData saveData,
        QuestManager questManager,
        IEventBus eventBus,
        IAudioService? audioService = null,
        IInputService? inputService = null)
    {
        _saveData = saveData ?? throw new ArgumentNullException(nameof(saveData));
        _questManager = questManager ?? throw new ArgumentNullException(nameof(questManager));
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        _audioService = audioService;
        _inputService = inputService;
        _progression = new LevelProgressionService(_saveData, _eventBus);

        foreach (var entity in _grid.Entities)
        {
            _visualPositions[entity.Id] = new Vector2(entity.X, entity.Y);
        }
    }

    public void Enter()
    {
        _questManager.StartQuest(HanoiQuests.Quest3Id);
        _hintMessage = "Bút Chì: Hãy dọn các sạp hàng để mở lối thông từ cổng vào bên trái sang Cầu Long Biên bên phải!";
        _hintTimer = 6f;
        RequestNextScene = false;
        RequestExit = false;
        _dragStartPos = null;
        _dragEntityId = null;

        _visualPositions.Clear();
        foreach (var entity in _grid.Entities)
        {
            _visualPositions[entity.Id] = new Vector2(entity.X, entity.Y);
        }
        _particles.Clear();
    }

    public void Update(float dt)
    {
        _totalTime += dt;
        if (_hintTimer > 0f) _hintTimer -= dt;

        foreach (var entity in _grid.Entities)
        {
            Vector2 target = new(entity.X, entity.Y);
            if (!_visualPositions.TryGetValue(entity.Id, out Vector2 current))
            {
                _visualPositions[entity.Id] = target;
            }
            else
            {
                _visualPositions[entity.Id] = Vector2.Lerp(current, target, Math.Clamp(dt * 14f, 0f, 1f));
            }
        }

        _particles.Update(dt);

        if (_letterModal.IsActive)
        {
            _letterModal.Update(dt,
                isConfirmPressed: _inputService?.IsConfirmJustPressed ?? false,
                isSkipPressed: _inputService?.IsCancelJustPressed ?? false);
            if (_letterModal.RequestClose)
            {
                _saveData.Level1State.QuestFlags.GrayShadow_LetterRead = true;
                _eventBus.Publish(new CutsceneEndedEvent("GrayShadow_Letter"));
                _eventBus.Publish(new LetterOpenedEvent("Letter_GrayShadow_Hanoi", "Bóng Xám"));
                RequestNextScene = true;
            }
            return;
        }
        else if (_letterModal.RequestClose && !_saveData.Level1State.QuestFlags.GrayShadow_LetterRead)
        {
            _saveData.Level1State.QuestFlags.GrayShadow_LetterRead = true;
            _eventBus.Publish(new CutsceneEndedEvent("GrayShadow_Letter"));
            _eventBus.Publish(new LetterOpenedEvent("Letter_GrayShadow_Hanoi", "Bóng Xám"));
            RequestNextScene = true;
            return;
        }

        if (_solved)
        {
            _solveTimer += dt;
            _bloomProgress = Math.Clamp(_solveTimer / 2.5f, 0f, 1f);

            if (_solveTimer >= 3.0f && !_letterModal.IsActive && !_saveData.Level1State.QuestFlags.GrayShadow_LetterRead)
            {
                _letterModal.Show();
            }
            else if (_solveTimer >= 4.0f && !RequestNextScene)
            {
                RequestNextScene = true;
            }
            return;
        }

        _pathClear = _grid.IsPathClear();
        if (_pathClear && !_solved)
        {
            TriggerSolve();
            return;
        }

        HandleInput();
    }

    public bool Push(string objectId, Direction direction)
    {
        var entity = GetEntityById(objectId);
        int fromX = entity?.X ?? 0, fromY = entity?.Y ?? 0;
        bool moved = _grid.PushObject(objectId, direction);
        if (moved)
        {
            _moveCount++;
            var updated = GetEntityById(objectId);
            _eventBus.Publish(new StallPushedEvent(objectId, fromX, fromY,
                updated?.X ?? fromX, updated?.Y ?? fromY, true));
            _audioService?.PlaySfx(SfxType.StepStreet, 0.4f);

            float puffX = GridOriginX + (fromX + 0.5f) * CellSize;
            float puffY = GridOriginY + (fromY + 0.5f) * CellSize;
            _particles.EmitCloudPuff(new Vector2(puffX, puffY), 4);

            _pathClear = _grid.IsPathClear();
            if (_pathClear && !_solved)
            {
                TriggerSolve();
            }
        }
        else
        {
            _eventBus.Publish(new StallPushedEvent(objectId, fromX, fromY, fromX, fromY, false));
        }
        return moved;
    }

    public void ResetGrid()
    {
        _grid.ResetGrid();
        _selectedObjectId = null;
        _moveCount = 0;
        _visualPositions.Clear();
        foreach (var entity in _grid.Entities)
        {
            _visualPositions[entity.Id] = new Vector2(entity.X, entity.Y);
        }
        _particles.Clear();
        _eventBus.Publish(new StallPuzzleResetEvent());
        _hintMessage = "Đã làm lại! Thử tìm đường khác nhé.";
        _hintTimer = 3f;
        _audioService?.PlaySfx(SfxType.ButtonClick);
    }

    public void TriggerHint()
    {
        var hintMgr = new HintManager();
        _hintMessage = hintMgr.GetStallPuzzleHint(_grid.IsPathClear(), _moveCount);
        _hintTimer = 6f;
        _audioService?.PlaySfx(SfxType.DialogueBlip);
    }

    private void CycleSelectedEntity()
    {
        var movable = _grid.Entities.Where(e => !e.IsStaticWall).ToList();
        if (movable.Count == 0) return;

        int currentIdx = movable.FindIndex(e => e.Id == _selectedObjectId);
        int nextIdx = (currentIdx + 1) % movable.Count;
        _selectedObjectId = movable[nextIdx].Id;
        _audioService?.PlaySfx(SfxType.ButtonClick);
    }

    private void HandleInput()
    {
        if (_inputService != null && _inputService.IsCancelJustPressed)
        {
            RequestExit = true;
            return;
        }

        if (!Raylib.IsWindowReady()) return;

        if (Raylib.IsKeyPressed(KeyboardKey.Escape))
        {
            RequestExit = true;
            return;
        }

        Vector2 mouse = Raylib.GetMousePosition();

        // 1. UI Buttons in sidebar (Reset, Hint, Exit)
        Rectangle btnReset = new(1050f, 490f, 92f, 36f);
        Rectangle btnHint = new(1154f, 490f, 92f, 36f);
        Rectangle btnExit = new(1050f, 536f, 196f, 36f);

        if (Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            if (Raylib.CheckCollisionPointRec(mouse, btnReset))
            {
                ResetGrid();
                return;
            }
            if (Raylib.CheckCollisionPointRec(mouse, btnHint))
            {
                TriggerHint();
                return;
            }
            if (Raylib.CheckCollisionPointRec(mouse, btnExit))
            {
                RequestExit = true;
                return;
            }
        }

        // 2. Click on selected entity's push arrow buttons
        if (_selectedObjectId != null)
        {
            var selEntity = GetEntityById(_selectedObjectId);
            if (selEntity != null)
            {
                Vector2 visualPos = _visualPositions.TryGetValue(selEntity.Id, out var vp) ? vp : new Vector2(selEntity.X, selEntity.Y);
                float x = GridOriginX + visualPos.X * CellSize;
                float y = GridOriginY + visualPos.Y * CellSize;
                float w = selEntity.Width * CellSize;
                float h = selEntity.Height * CellSize;
                float cx = x + w / 2f, cy = y + h / 2f;

                Rectangle upArrowRec = new(cx - 20f, cy - h / 2f - 32f, 40f, 28f);
                Rectangle downArrowRec = new(cx - 20f, cy + h / 2f + 4f, 40f, 28f);
                Rectangle leftArrowRec = new(cx - w / 2f - 32f, cy - 20f, 28f, 40f);
                Rectangle rightArrowRec = new(cx + w / 2f + 4f, cy - 20f, 28f, 40f);

                if (Raylib.IsMouseButtonPressed(MouseButton.Left))
                {
                    if (selEntity.CanPushVertically && Raylib.CheckCollisionPointRec(mouse, upArrowRec))
                    {
                        Push(_selectedObjectId, Direction.Up);
                        return;
                    }
                    if (selEntity.CanPushVertically && Raylib.CheckCollisionPointRec(mouse, downArrowRec))
                    {
                        Push(_selectedObjectId, Direction.Down);
                        return;
                    }
                    if (selEntity.CanPushHorizontally && Raylib.CheckCollisionPointRec(mouse, leftArrowRec))
                    {
                        Push(_selectedObjectId, Direction.Left);
                        return;
                    }
                    if (selEntity.CanPushHorizontally && Raylib.CheckCollisionPointRec(mouse, rightArrowRec))
                    {
                        Push(_selectedObjectId, Direction.Right);
                        return;
                    }
                }
            }
        }

        // 3. Left Click on Entity or Adjacent Empty Cell
        if (Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            string? clickedId = GetEntityAtMouse(mouse);
            if (clickedId != null)
            {
                _selectedObjectId = (_selectedObjectId == clickedId) ? null : clickedId;
                _dragStartPos = mouse;
                _dragEntityId = _selectedObjectId;
                _audioService?.PlaySfx(SfxType.ButtonClick);
                return;
            }

            // If an entity was selected, check if user clicked on an adjacent empty tile in the grid
            if (_selectedObjectId != null)
            {
                var selEntity = GetEntityById(_selectedObjectId);
                if (selEntity != null && mouse.X >= GridOriginX && mouse.Y >= GridOriginY)
                {
                    int clickedCol = (int)((mouse.X - GridOriginX) / CellSize);
                    int clickedRow = (int)((mouse.Y - GridOriginY) / CellSize);

                    if (clickedCol >= 0 && clickedCol < _grid.Width && clickedRow >= 0 && clickedRow < _grid.Height)
                    {
                        // Check adjacency to selected entity
                        if (selEntity.CanPushVertically && clickedCol >= selEntity.X && clickedCol < selEntity.X + selEntity.Width && clickedRow == selEntity.Y - 1)
                        {
                            Push(_selectedObjectId, Direction.Up);
                            return;
                        }
                        if (selEntity.CanPushVertically && clickedCol >= selEntity.X && clickedCol < selEntity.X + selEntity.Width && clickedRow == selEntity.Y + selEntity.Height)
                        {
                            Push(_selectedObjectId, Direction.Down);
                            return;
                        }
                        if (selEntity.CanPushHorizontally && clickedRow >= selEntity.Y && clickedRow < selEntity.Y + selEntity.Height && clickedCol == selEntity.X - 1)
                        {
                            Push(_selectedObjectId, Direction.Left);
                            return;
                        }
                        if (selEntity.CanPushHorizontally && clickedRow >= selEntity.Y && clickedRow < selEntity.Y + selEntity.Height && clickedCol == selEntity.X + selEntity.Width)
                        {
                            Push(_selectedObjectId, Direction.Right);
                            return;
                        }
                    }
                }

                _selectedObjectId = null;
            }
        }

        // 4. Mouse Drag gesture
        if (Raylib.IsMouseButtonDown(MouseButton.Left) && _dragStartPos.HasValue && !string.IsNullOrEmpty(_dragEntityId))
        {
            Vector2 dragDelta = mouse - _dragStartPos.Value;
            if (dragDelta.Length() >= 24f)
            {
                Direction? dragDir = null;
                if (MathF.Abs(dragDelta.X) > MathF.Abs(dragDelta.Y))
                {
                    dragDir = dragDelta.X > 0 ? Direction.Right : Direction.Left;
                }
                else
                {
                    dragDir = dragDelta.Y > 0 ? Direction.Down : Direction.Up;
                }

                if (dragDir.HasValue)
                {
                    Push(_dragEntityId, dragDir.Value);
                }
                _dragStartPos = null;
                _dragEntityId = null;
            }
        }
        else if (Raylib.IsMouseButtonReleased(MouseButton.Left))
        {
            _dragStartPos = null;
            _dragEntityId = null;
        }

        // 5. Keyboard Navigation & Selection (Tab, 1..5)
        if (Raylib.IsKeyPressed(KeyboardKey.Tab))
        {
            CycleSelectedEntity();
        }

        var movableList = _grid.Entities.Where(e => !e.IsStaticWall).ToList();
        for (int i = 0; i < Math.Min(movableList.Count, 5); i++)
        {
            if (Raylib.IsKeyPressed(KeyboardKey.One + i))
            {
                _selectedObjectId = movableList[i].Id;
                _audioService?.PlaySfx(SfxType.ButtonClick);
            }
        }

        // 6. Push selected object with WASD/arrows
        if (_selectedObjectId != null)
        {
            Direction? dir = null;
            if (Raylib.IsKeyPressed(KeyboardKey.Up)    || Raylib.IsKeyPressed(KeyboardKey.W)) dir = Direction.Up;
            if (Raylib.IsKeyPressed(KeyboardKey.Down)  || Raylib.IsKeyPressed(KeyboardKey.S)) dir = Direction.Down;
            if (Raylib.IsKeyPressed(KeyboardKey.Left)  || Raylib.IsKeyPressed(KeyboardKey.A)) dir = Direction.Left;
            if (Raylib.IsKeyPressed(KeyboardKey.Right) || Raylib.IsKeyPressed(KeyboardKey.D)) dir = Direction.Right;

            if (dir.HasValue)
            {
                Push(_selectedObjectId, dir.Value);
            }
        }
        else
        {
            // Auto-select when player presses movement keys without selection
            if (Raylib.IsKeyPressed(KeyboardKey.Up) || Raylib.IsKeyPressed(KeyboardKey.Down) ||
                Raylib.IsKeyPressed(KeyboardKey.Left) || Raylib.IsKeyPressed(KeyboardKey.Right) ||
                Raylib.IsKeyPressed(KeyboardKey.W) || Raylib.IsKeyPressed(KeyboardKey.S) ||
                Raylib.IsKeyPressed(KeyboardKey.A) || Raylib.IsKeyPressed(KeyboardKey.D) ||
                Raylib.IsKeyPressed(KeyboardKey.Space) || Raylib.IsKeyPressed(KeyboardKey.Enter))
            {
                CycleSelectedEntity();
            }
        }

        if (Raylib.IsKeyPressed(KeyboardKey.R))
        {
            ResetGrid();
        }

        if ((_inputService != null && _inputService.IsHintJustPressed) || (Raylib.IsWindowReady() && Raylib.IsKeyPressed(KeyboardKey.H)))
        {
            TriggerHint();
        }
    }

    private GridEntity? GetEntityById(string id)
    {
        foreach (var e in _grid.Entities)
            if (e.Id == id) return e;
        return null;
    }

    private string? GetEntityAtMouse(Vector2 mouse)
    {
        foreach (var entity in _grid.Entities)
        {
            if (entity.IsStaticWall) continue;
            Vector2 visualPos = _visualPositions.TryGetValue(entity.Id, out var vp) ? vp : new Vector2(entity.X, entity.Y);
            float x1 = GridOriginX + visualPos.X * CellSize;
            float y1 = GridOriginY + visualPos.Y * CellSize;
            float w = entity.Width * CellSize;
            float h = entity.Height * CellSize;
            Rectangle rect = new(x1 + 2f, y1 + 2f, w - 4f, h - 4f);
            if (Raylib.CheckCollisionPointRec(mouse, rect))
                return entity.Id;
        }
        return null;
    }

    private void TriggerSolve()
    {
        _solved = true;
        _solveTimer = 0f;
        _saveData.Level1State.QuestFlags.Gate_LongBien_Unlocked = true;
        _saveData.Level1State.QuestFlags.Q3_Completed = true;
        _questManager.RecordObjectiveProgress(HanoiQuests.Quest3Id, "clear_long_bien_path");
        _questManager.CompleteQuest(HanoiQuests.Quest3Id);
        _progression.CompleteLevel1();
        _eventBus.Publish(new StallPuzzleSolvedEvent(_moveCount));

        // Confetti celebration when puzzle is solved
        Vector2 gateCenter = new(GridOriginX + (_grid.Width - 0.5f) * CellSize, GridOriginY + 2.5f * CellSize);
        Vector2 boardCenter = new(GridOriginX + _grid.Width * 0.5f * CellSize, GridOriginY + _grid.Height * 0.5f * CellSize);
        _particles.EmitConfetti(gateCenter, 30);
        _particles.EmitConfetti(boardCenter, 30);
        _particles.EmitSparkles(gateCenter, HanoiPalette.WallYellow, 12);
        _particles.EmitSparkles(boardCenter, HanoiPalette.LotusPink, 12);

        _audioService?.PlaySfx(SfxType.GateUnlocked);
        _audioService?.PlaySfx(SfxType.ColorBloomWave);
    }

    public void Draw()
    {
        if (!Raylib.IsWindowReady()) return;

        // Background: Hanoi street scene
        Color bgBase = HanoiPalette.ApplyDesaturation(HanoiPalette.StoneStreet, _solved ? 50f + _bloomProgress * 50f : 25f);
        Raylib.ClearBackground(bgBase);

        DrawHeader();
        DrawGridBackground();
        DrawEntities();
        DrawPathIndicator();
        DrawSidebar();
        DrawHintMessage();

        _particles.Draw();

        if (_solved) DrawSolveOverlay();

        if (_letterModal.IsActive)
        {
            _letterModal.Draw();
        }
    }

    private void DrawHeader()
    {
        string title = "Dọn Lối Ra Cầu Long Biên";
        Vector2 sz = FontManager.Instance.MeasureText(title, 26f);
        FontManager.Instance.DrawTextOutline(title, new Vector2(640f - sz.X / 2f, 20f), 26f,
            HanoiPalette.WallYellow, HanoiPalette.CartoonOutline, 2f);

        string sub = "Click/kéo sạp · Bấm nút mũi tên hoặc WASD · [Tab/1-5] Chọn sạp · [R] Làm lại · [H] Gợi ý · [ESC] Thoát";
        Vector2 subSz = FontManager.Instance.MeasureText(sub, 11f);
        FontManager.Instance.DrawText(sub, new Vector2(640f - subSz.X / 2f, 56f), 11f,
            new Color(90, 70, 50, 230));
    }

    private void DrawGridBackground()
    {
        int cols = _grid.Width, rows = _grid.Height;

        // Draw cells
        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < cols; col++)
            {
                float x = GridOriginX + col * CellSize;
                float y = GridOriginY + row * CellSize;
                Rectangle cell = new(x, y, CellSize, CellSize);

                bool isPathRow = row == 2;
                Color cellColor = isPathRow ? new Color(195, 185, 168, 255) : new Color(170, 161, 148, 255);

                bool isEntrance = col == 0 && row == 2;
                bool isExit = col == cols - 1 && row == 2;
                if (isEntrance) cellColor = new Color(130, 210, 140, 255);
                if (isExit) cellColor = HanoiPalette.ApplyDesaturation(new Color(255, 200, 50, 255), _solved ? 100f : 40f);

                Raylib.DrawRectangleRec(cell, cellColor);
                Raylib.DrawRectangleLinesEx(cell, 1f, HanoiPalette.StoneMortar);

                if (isEntrance)
                {
                    string eLabel = "Vào";
                    FontManager.Instance.DrawText(eLabel, new Vector2(x + 10f, y + CellSize / 2f - 7f), 11f,
                        new Color(30, 100, 50, 255));
                }
                if (isExit)
                {
                    string xLabel = _solved ? "Đích" : "Cầu";
                    FontManager.Instance.DrawText(xLabel, new Vector2(x + 10f, y + CellSize / 2f - 7f), 11f,
                        new Color(140, 90, 20, 255));
                }
            }
        }

        // Draw real-time BFS path glowing tile highlights
        var path = _grid.FindPath();
        if (path != null)
        {
            float pulse = 0.35f + 0.15f * MathF.Sin(_totalTime * 4.0f);
            foreach (var (px, py) in path)
            {
                float cx = GridOriginX + px * CellSize;
                float cy = GridOriginY + py * CellSize;
                Rectangle glowRect = new(cx + 1f, cy + 1f, CellSize - 2f, CellSize - 2f);
                Raylib.DrawRectangleRec(glowRect, Raylib.ColorAlpha(new Color(255, 230, 80, 255), pulse));
            }
        }

        // Draw Long Biên bridge silhouette on right edge
        Color bridgeColor = HanoiPalette.ApplyDesaturation(HanoiPalette.RustedIron, _solved ? _bloomProgress * 80f : 20f);
        float bx = GridOriginX + cols * CellSize + 10f;
        float by = GridOriginY;
        for (int i = 0; i < 3; i++)
        {
            Raylib.DrawLineEx(new Vector2(bx + i * 24f, by + 100f), new Vector2(bx + i * 24f + 12f, by + 20f), 4f, bridgeColor);
            Raylib.DrawLineEx(new Vector2(bx + i * 24f + 12f, by + 20f), new Vector2(bx + i * 24f + 24f, by + 100f), 4f, bridgeColor);
        }
        Raylib.DrawLineEx(new Vector2(bx, by + 100f), new Vector2(bx + 72f, by + 100f), 4f, bridgeColor);
    }

    private void DrawEntities()
    {
        foreach (var entity in _grid.Entities)
        {
            if (entity.IsStaticWall) continue;

            Vector2 visualPos = _visualPositions.TryGetValue(entity.Id, out var vp) ? vp : new Vector2(entity.X, entity.Y);
            float x = GridOriginX + visualPos.X * CellSize;
            float y = GridOriginY + visualPos.Y * CellSize;
            float w = entity.Width * CellSize;
            float h = entity.Height * CellSize;

            bool isSelected = entity.Id == _selectedObjectId;
            Rectangle rect = new(x + 3f, y + 3f, w - 6f, h - 6f);

            EntityColors.TryGetValue(entity.Id, out Color entityColor);
            if (entityColor == default) entityColor = new Color(200, 200, 200, 255);

            if (isSelected)
            {
                Rectangle glowRect = new(rect.X - 4f, rect.Y - 4f, rect.Width + 8f, rect.Height + 8f);
                VectorRenderer.DrawRoundedRect(glowRect, 0.25f,
                    Raylib.ColorAlpha(Color.Yellow, 0.4f), Color.Yellow, 3f, withShadow: false);
            }

            VectorRenderer.DrawRoundedRect(rect, 0.3f, entityColor, HanoiPalette.CartoonOutline,
                isSelected ? 2.5f : 1.5f);

            if (entity.Id == "Obj_FruitStall")
            {
                VectorRenderer.DrawCoLanVeggieStall(new Vector2(x + w * 0.5f, y + h - 6f), "", _totalTime, scale: 0.62f, withShadow: false);
            }

            // Label
            EntityLabels.TryGetValue(entity.Id, out string? label);
            label ??= entity.Name;
            Vector2 lsz = FontManager.Instance.MeasureText(label, 11f);
            if (entity.Id == "Obj_FruitStall")
            {
                FontManager.Instance.DrawTextOutline(label,
                    new Vector2(x + w / 2f - lsz.X / 2f, y + 6f), 10f, HanoiPalette.CartoonOutline, Color.White, 1.2f);
            }
            else
            {
                FontManager.Instance.DrawText(label,
                    new Vector2(x + w / 2f - lsz.X / 2f, y + h / 2f - 7f), 11f, HanoiPalette.CartoonOutline);
            }

            // Direction badge on corner
            string dirBadge = entity.CanPushHorizontally && entity.CanPushVertically ? "✥" : (entity.CanPushVertically ? "↕" : "↔");
            FontManager.Instance.DrawText(dirBadge, new Vector2(x + 5f, y + 4f), 10f, new Color(80, 60, 40, 180));

            // Push arrow buttons if selected
            if (isSelected)
            {
                float cx = x + w / 2f, cy = y + h / 2f;
                if (entity.CanPushVertically)
                {
                    Rectangle upBtn = new(cx - 18f, cy - h / 2f - 30f, 36f, 26f);
                    VectorRenderer.DrawRoundedRect(upBtn, 0.4f, new Color(255, 230, 80, 240), HanoiPalette.CartoonOutline, 1.5f, withShadow: false);
                    Raylib.DrawTriangle(new Vector2(cx, upBtn.Y + 4f), new Vector2(cx - 7f, upBtn.Y + 19f), new Vector2(cx + 7f, upBtn.Y + 19f), HanoiPalette.CartoonOutline);

                    Rectangle downBtn = new(cx - 18f, cy + h / 2f + 4f, 36f, 26f);
                    VectorRenderer.DrawRoundedRect(downBtn, 0.4f, new Color(255, 230, 80, 240), HanoiPalette.CartoonOutline, 1.5f, withShadow: false);
                    Raylib.DrawTriangle(new Vector2(cx, downBtn.Y + 22f), new Vector2(cx + 7f, downBtn.Y + 7f), new Vector2(cx - 7f, downBtn.Y + 7f), HanoiPalette.CartoonOutline);
                }
                if (entity.CanPushHorizontally)
                {
                    Rectangle leftBtn = new(cx - w / 2f - 30f, cy - 18f, 26f, 36f);
                    VectorRenderer.DrawRoundedRect(leftBtn, 0.4f, new Color(255, 230, 80, 240), HanoiPalette.CartoonOutline, 1.5f, withShadow: false);
                    Raylib.DrawTriangle(new Vector2(leftBtn.X + 4f, cy), new Vector2(leftBtn.X + 19f, cy - 7f), new Vector2(leftBtn.X + 19f, cy + 7f), HanoiPalette.CartoonOutline);

                    Rectangle rightBtn = new(cx + w / 2f + 4f, cy - 18f, 26f, 36f);
                    VectorRenderer.DrawRoundedRect(rightBtn, 0.4f, new Color(255, 230, 80, 240), HanoiPalette.CartoonOutline, 1.5f, withShadow: false);
                    Raylib.DrawTriangle(new Vector2(rightBtn.X + 22f, cy), new Vector2(rightBtn.X + 7f, cy + 7f), new Vector2(rightBtn.X + 7f, cy - 7f), HanoiPalette.CartoonOutline);
                }
            }
        }
    }

    private void DrawPathIndicator()
    {
        var path = _grid.FindPath();
        if (path != null && path.Count > 1)
        {
            float pulse = 0.35f + 0.25f * MathF.Sin(_totalTime * 4.5f);
            Color outerGoldAura = Raylib.ColorAlpha(new Color(255, 215, 0, 255), pulse);
            Color innerBrightGold = new(255, 235, 120, 255);

            // 1. Outer pulse aura ribbon (width 10px, glowing gold)
            for (int i = 0; i < path.Count - 1; i++)
            {
                Vector2 p1 = new(GridOriginX + (path[i].x + 0.5f) * CellSize, GridOriginY + (path[i].y + 0.5f) * CellSize);
                Vector2 p2 = new(GridOriginX + (path[i + 1].x + 0.5f) * CellSize, GridOriginY + (path[i + 1].y + 0.5f) * CellSize);
                Raylib.DrawLineEx(p1, p2, 10f, outerGoldAura);
                Raylib.DrawCircleV(p1, 5f, outerGoldAura);
            }
            Vector2 lastP = new(GridOriginX + (path[^1].x + 0.5f) * CellSize, GridOriginY + (path[^1].y + 0.5f) * CellSize);
            Raylib.DrawCircleV(lastP, 5f, outerGoldAura);

            // 2. Inner bright ribbon (width 4px, Color(255, 235, 120))
            for (int i = 0; i < path.Count - 1; i++)
            {
                Vector2 p1 = new(GridOriginX + (path[i].x + 0.5f) * CellSize, GridOriginY + (path[i].y + 0.5f) * CellSize);
                Vector2 p2 = new(GridOriginX + (path[i + 1].x + 0.5f) * CellSize, GridOriginY + (path[i + 1].y + 0.5f) * CellSize);
                Raylib.DrawLineEx(p1, p2, 4f, innerBrightGold);
                Raylib.DrawCircleV(p1, 2f, innerBrightGold);
            }
            Raylib.DrawCircleV(lastP, 2f, innerBrightGold);

            // 3. Flowing dash sparkles along the path towards Long Bien gate
            int segCount = path.Count - 1;
            int sparkCount = Math.Max(3, segCount * 2);
            for (int s = 0; s < sparkCount; s++)
            {
                float phase = (_totalTime * 2.5f + (float)s / sparkCount * segCount) % segCount;
                int segIdx = (int)phase;
                if (segIdx >= 0 && segIdx < segCount)
                {
                    float t = phase - segIdx;
                    Vector2 sp1 = new(GridOriginX + (path[segIdx].x + 0.5f) * CellSize, GridOriginY + (path[segIdx].y + 0.5f) * CellSize);
                    Vector2 sp2 = new(GridOriginX + (path[segIdx + 1].x + 0.5f) * CellSize, GridOriginY + (path[segIdx + 1].y + 0.5f) * CellSize);
                    Vector2 sparkPos = Vector2.Lerp(sp1, sp2, t);
                    Raylib.DrawCircleV(sparkPos, 3.5f, Color.White);
                    Raylib.DrawCircleV(sparkPos, 6.0f, Raylib.ColorAlpha(new Color(255, 250, 180, 255), 0.65f));
                }
            }
        }

        string pathText = _pathClear ? "Đường thông! Cổng Cầu Long Biên đã mở" : "Đường bị chặn";
        Color pathColor = _pathClear ? new Color(46, 160, 67, 255) : new Color(200, 60, 40, 255);
        Vector2 pSz = FontManager.Instance.MeasureText(pathText, 15f);
        float px = GridOriginX + _grid.Width * CellSize / 2f - pSz.X / 2f + 10f;
        float py = GridOriginY + _grid.Height * CellSize + 12f;
        if (_pathClear)
        {
            VectorRenderer.DrawCheckmark(new Vector2(px - 14f, py + 8f), 12f, pathColor);
        }
        else
        {
            VectorRenderer.DrawCloseCross(new Vector2(px - 14f, py + 8f), 10f, pathColor);
        }
        FontManager.Instance.DrawTextOutline(pathText, new Vector2(px, py), 15f, pathColor, Color.White, 1.5f);
    }

    private void DrawSidebar()
    {
        // Move counter
        Rectangle sideBox = new(1050f, 140f, 196f, 100f);
        VectorRenderer.DrawRoundedRect(sideBox, 0.25f, new Color(255, 252, 240, 230),
            HanoiPalette.CartoonOutline, 1.5f);
        FontManager.Instance.DrawText("Số bước đã đẩy:", new Vector2(1065f, 155f), 12f, new Color(80, 60, 40, 255));
        string moveStr = _moveCount.ToString();
        Vector2 mSz = FontManager.Instance.MeasureText(moveStr, 36f);
        FontManager.Instance.DrawTextOutline(moveStr, new Vector2(1148f - mSz.X / 2f, 170f), 36f,
            HanoiPalette.WallYellow, HanoiPalette.CartoonOutline, 2f);

        // Legend
        Rectangle legendBox = new(1050f, 255f, 196f, 215f);
        VectorRenderer.DrawRoundedRect(legendBox, 0.25f, new Color(255, 252, 240, 220),
            HanoiPalette.CartoonOutline, 1.5f);
        FontManager.Instance.DrawText("Chú thích:", new Vector2(1065f, 268f), 12f, new Color(80, 60, 40, 255));

        int li = 0;
        foreach (var (id, color) in EntityColors)
        {
            float lY = 290f + li * 34f;
            Raylib.DrawRectangleRounded(new Rectangle(1065f, lY, 20f, 16f), 0.3f, 4, color);
            EntityLabels.TryGetValue(id, out string? lbl);
            FontManager.Instance.DrawText(lbl ?? id, new Vector2(1092f, lY), 11f, new Color(60, 45, 30, 255));
            li++;
        }

        // Action Buttons: Reset, Hint, Exit
        Rectangle btnReset = new(1050f, 485f, 92f, 36f);
        VectorRenderer.DrawRoundedRect(btnReset, 0.35f, new Color(255, 215, 64, 255), HanoiPalette.CartoonOutline, 1.5f);
        FontManager.Instance.DrawText("[R] Làm lại", new Vector2(btnReset.X + 14f, btnReset.Y + 11f), 12f, HanoiPalette.CartoonOutline);

        Rectangle btnHint = new(1154f, 485f, 92f, 36f);
        VectorRenderer.DrawRoundedRect(btnHint, 0.35f, new Color(130, 210, 240, 255), HanoiPalette.CartoonOutline, 1.5f);
        FontManager.Instance.DrawText("[H] Gợi ý", new Vector2(btnHint.X + 18f, btnHint.Y + 11f), 12f, HanoiPalette.CartoonOutline);

        Rectangle btnExit = new(1050f, 530f, 196f, 36f);
        VectorRenderer.DrawRoundedRect(btnExit, 0.35f, new Color(240, 225, 215, 255), HanoiPalette.CartoonOutline, 1.5f);
        FontManager.Instance.DrawText("[ESC] Thoát ra phố", new Vector2(btnExit.X + 46f, btnExit.Y + 11f), 12f, new Color(80, 50, 40, 255));
    }

    private void DrawHintMessage()
    {
        if (string.IsNullOrEmpty(_hintMessage) || _hintTimer <= 0f) return;
        float alpha = Math.Clamp(_hintTimer, 0f, 1f);
        Vector2 sz = FontManager.Instance.MeasureText(_hintMessage, 13f);
        float bw = Math.Min(sz.X + 32f, 900f);
        Rectangle box = new(640f - bw / 2f, 672f, bw, 36f);
        VectorRenderer.DrawRoundedRect(box, 0.35f, Raylib.ColorAlpha(new Color(50, 100, 180, 255), alpha * 0.9f),
            Color.White, 1.5f, withShadow: false);
        FontManager.Instance.DrawText(_hintMessage, new Vector2(box.X + 16f, box.Y + 10f), 13f,
            Raylib.ColorAlpha(Color.White, alpha));
    }

    private void DrawSolveOverlay()
    {
        // Color bloom wave
        Raylib.DrawRectangle(0, 0, 1280, 720,
            Raylib.ColorAlpha(new Color(255, 230, 100, 255), _bloomProgress * 0.35f));

        if (_solveTimer > 0.4f)
        {
            string msg1 = "Cổng Cầu Long Biên đã mở!";
            string msg2 = $"Hà Nội đã hồi sinh 100% sắc màu! ({_moveCount} bước)";
            string msg3 = "Mây và Bút Chì nhận được Trang Sổ Khởi Đầu và Tem Hoa Sen!";

            Vector2 s1 = FontManager.Instance.MeasureText(msg1, 30f);
            FontManager.Instance.DrawTextOutline(msg1, new Vector2(640f - s1.X / 2f, 210f), 30f,
                HanoiPalette.WallYellow, HanoiPalette.CartoonOutline, 2.5f);

            Vector2 s2 = FontManager.Instance.MeasureText(msg2, 18f);
            FontManager.Instance.DrawText(msg2, new Vector2(640f - s2.X / 2f, 258f), 18f, new Color(60, 50, 35, 255));

            if (_solveTimer > 1.0f)
            {
                Vector2 s3 = FontManager.Instance.MeasureText(msg3, 15f);
                FontManager.Instance.DrawText(msg3, new Vector2(640f - s3.X / 2f, 295f), 15f, new Color(80, 60, 40, 220));

                // Lotus stamp medallion
                VectorRenderer.DrawCartoonCircle(new Vector2(520f, 400f), 55f,
                    HanoiPalette.ApplyDesaturation(HanoiPalette.LotusPink, _bloomProgress * 100f),
                    HanoiPalette.CartoonOutline, 2f);
                string ls = "Tem Hoa Sen";
                Vector2 lsz = FontManager.Instance.MeasureText(ls, 12f);
                FontManager.Instance.DrawText(ls, new Vector2(520f - lsz.X / 2f, 394f), 12f, Color.White);

                // Memory Journal page medallion
                Rectangle jRect = new(690f, 350f, 100f, 100f);
                VectorRenderer.DrawRoundedRect(jRect, 0.15f,
                    new Color(255, 248, 220, 255), HanoiPalette.CartoonOutline, 2f);
                string jp = "Trang Sổ\nKhởi Đầu";
                FontManager.Instance.DrawText(jp, new Vector2(705f, 385f), 13f, HanoiPalette.CartoonOutline);
            }
        }
    }

    public void Exit() { }
    public void Dispose()
    {
        VectorRenderer.UnloadStallTextures();
    }
}
