using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using GameProject.Core.Events;
using GameProject.Core.Hints;
using GameProject.Core.Models;
using GameProject.Core.Ports;
using GameProject.Core.Puzzles;
using GameProject.Core.Quests;
using GameProject.Graphics;
using Raylib_cs;

namespace GameProject.Stages;

/// <summary>
/// Quest 3: "Thả Đèn Hoa Đăng trên Sông Hương" spatial river puzzle scene & modal overlay.
/// Push dragon boats, flower rafts, and fisher sampans on a 7x5 water grid to clear a continuous river channel
/// from the Imperial Citadel pier (0, 2) to the sacred waters of Chùa Thiên Mụ (6, 2).
/// Features real-time BFS golden river reflection ribbon, floating water lanterns, 100% regional color bloom wave,
/// and unlocks the journey to Hội An.
/// </summary>
public sealed class HoaDangRiverScene : IScene
{
    private readonly SaveData _saveData;
    private readonly QuestManager _questManager;
    private readonly IEventBus? _eventBus;
    private readonly IAudioService? _audioService;
    private readonly IInputService? _inputService;
    private readonly LevelProgressionService _progression;
    private readonly HoaDangRiverEngine _grid = new();
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
    public string? HintMessage => _hintMessage;
    public HoaDangRiverEngine Engine => _grid;
    public IReadOnlyList<GridEntity> Entities => _grid.Entities;

    // Grid rendering constants
    private const float GridOriginX = 160f;
    private const float GridOriginY = 140f;
    private const float CellSize = 88f;

    // Cultural Entity Labels
    private static readonly Dictionary<string, string> EntityLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        { "Obj_FlowerRaft",        "Bè hoa sen" },
        { "Obj_FlowerRaft_1",      "Bè hoa sen" },
        { "Obj_FisherSampan",      "Thuyền chài" },
        { "Obj_FruitBoat",         "Thuyền chài" },
        { "Obj_FruitBoat_1",       "Thuyền chài" },
        { "Obj_DragonBoat",        "Thuyền rồng" },
        { "Obj_DragonBoat_1",      "Thuyền rồng" },
        { "Obj_WaterLilyPatch",    "Bãi hoa sen" },
        { "Obj_LotusCluster",      "Bãi hoa sen" },
        { "Obj_LotusCluster_1",    "Bãi hoa sen" },
        { "Obj_LanternPlatform",   "Giàn hoa đăng" },
        { "Obj_LanternPlatform_1", "Giàn hoa đăng" }
    };

    public HoaDangRiverScene(
        SaveData saveData,
        QuestManager questManager,
        IEventBus? eventBus = null,
        IAudioService? audioService = null,
        IInputService? inputService = null)
    {
        _saveData = saveData ?? throw new ArgumentNullException(nameof(saveData));
        _questManager = questManager ?? throw new ArgumentNullException(nameof(questManager));
        _eventBus = eventBus;
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
        _questManager.StartQuest(HueQuests.Quest3Id);
        _hintMessage = "Bút Chì: Hãy dọn các thuyền rồng và bè hoa trên Sông Hương để mở luồng nước thông cho hoa đăng xuôi dòng nhé Mây!";
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

        if (_solved)
        {
            _solveTimer += dt;
            _bloomProgress = Math.Clamp(_solveTimer / 2.5f, 0f, 1f);

            if (_solveTimer >= 3.5f && !RequestNextScene)
            {
                RequestNextScene = true;
                RequestExit = true;
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
            _eventBus?.Publish(new StallPushedEvent(objectId, fromX, fromY,
                updated?.X ?? fromX, updated?.Y ?? fromY, true));
            _audioService?.PlaySfx(SfxType.StepStreet, 0.4f);

            float puffX = GridOriginX + (fromX + 0.5f) * CellSize;
            float puffY = GridOriginY + (fromY + 0.5f) * CellSize;
            _particles.EmitSparkles(new Vector2(puffX, puffY), new Color(100, 200, 255, 200), 5);

            _pathClear = _grid.IsPathClear();
            if (_pathClear && !_solved)
            {
                TriggerSolve();
            }
        }
        else
        {
            _eventBus?.Publish(new StallPushedEvent(objectId, fromX, fromY, fromX, fromY, false));
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
        _eventBus?.Publish(new StallPuzzleResetEvent());
        _hintMessage = "Đã sắp xếp lại dòng sông! Hãy thử tính toán đường đi khác nhé.";
        _hintTimer = 3f;
        _audioService?.PlaySfx(SfxType.ButtonClick);
    }

    public void TriggerHint()
    {
        if (_grid.IsPathClear())
        {
            _hintMessage = "Bút Chì: Luồng sông đã thông suốt! Hoa đăng đã có thể xuôi dòng.";
        }
        else
        {
            _hintMessage = "Bút Chì: Thuyền chài ở cột 2 có thể lùi sang trái, tạo chỗ cho bè hoa sen trượt xuống!";
        }
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

        // UI Buttons in sidebar
        Rectangle btnReset = new(1050f, 485f, 92f, 36f);
        Rectangle btnHint = new(1154f, 485f, 92f, 36f);
        Rectangle btnExit = new(1050f, 530f, 196f, 36f);

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

        // Push arrow buttons for selected entity
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

                Rectangle upArrowRec = new(cx - 18f, cy - h / 2f - 30f, 36f, 26f);
                Rectangle downArrowRec = new(cx - 18f, cy + h / 2f + 4f, 36f, 26f);
                Rectangle leftArrowRec = new(cx - w / 2f - 30f, cy - 18f, 26f, 36f);
                Rectangle rightArrowRec = new(cx + w / 2f + 4f, cy - 18f, 26f, 36f);

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

        // Left Click on Entity or Adjacent Empty Cell
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

            if (_selectedObjectId != null)
            {
                var selEntity = GetEntityById(_selectedObjectId);
                if (selEntity != null && mouse.X >= GridOriginX && mouse.Y >= GridOriginY)
                {
                    int clickedCol = (int)((mouse.X - GridOriginX) / CellSize);
                    int clickedRow = (int)((mouse.Y - GridOriginY) / CellSize);

                    if (clickedCol >= 0 && clickedCol < _grid.Width && clickedRow >= 0 && clickedRow < _grid.Height)
                    {
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

        // Drag gesture
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

        // Keyboard navigation
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

        if (_selectedObjectId != null)
        {
            Direction? dir = null;
            if (Raylib.IsKeyPressed(KeyboardKey.Up) || Raylib.IsKeyPressed(KeyboardKey.W)) dir = Direction.Up;
            if (Raylib.IsKeyPressed(KeyboardKey.Down) || Raylib.IsKeyPressed(KeyboardKey.S)) dir = Direction.Down;
            if (Raylib.IsKeyPressed(KeyboardKey.Left) || Raylib.IsKeyPressed(KeyboardKey.A)) dir = Direction.Left;
            if (Raylib.IsKeyPressed(KeyboardKey.Right) || Raylib.IsKeyPressed(KeyboardKey.D)) dir = Direction.Right;

            if (dir.HasValue)
            {
                Push(_selectedObjectId, dir.Value);
            }
        }
        else
        {
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

        if ((_inputService != null && _inputService.IsHintJustPressed) || Raylib.IsKeyPressed(KeyboardKey.H))
        {
            TriggerHint();
        }
    }

    private GridEntity? GetEntityById(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        foreach (var e in _grid.Entities)
            if (HoaDangRiverEngine.MatchesId(e.Id, id)) return e;
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
        _saveData.QuestFlags.Hue_Q3_PuzzleSolved = true;
        _saveData.QuestFlags.Hue_Q3_Completed = true;
        _questManager.RecordObjectiveProgress(HueQuests.Quest3Id, "release_hoa_dang");
        _questManager.CompleteQuest(HueQuests.Quest3Id);
        _progression.CompleteHue();
        _eventBus?.Publish(new StallPuzzleSolvedEvent(_moveCount));
        _eventBus?.Publish(new PuzzleSolvedEvent(HueQuests.Quest3Id));
        _eventBus?.Publish(new QuestCompletedEvent(HueQuests.Quest3Id, "Thả Đèn Hoa Đăng trên Sông Hương"));

        Vector2 pierCenter = new(GridOriginX + 0.5f * CellSize, GridOriginY + 2.5f * CellSize);
        Vector2 shrineCenter = new(GridOriginX + (_grid.Width - 0.5f) * CellSize, GridOriginY + 2.5f * CellSize);
        Vector2 riverCenter = new(GridOriginX + _grid.Width * 0.5f * CellSize, GridOriginY + _grid.Height * 0.5f * CellSize);

        _particles.EmitConfetti(riverCenter, 40);
        _particles.EmitSparkles(pierCenter, new Color(255, 215, 0, 255), 15);
        _particles.EmitSparkles(shrineCenter, HanoiPalette.LotusPink, 15);

        _audioService?.PlaySfx(SfxType.GateUnlocked);
        _audioService?.PlaySfx(SfxType.ColorBloomWave);
    }

    public void Draw()
    {
        if (!Raylib.IsWindowReady()) return;

        DrawNightBackdrop();
        DrawTitle();
        DrawRiverGrid();
        DrawEntities();
        DrawSelectionOutline();
        DrawControlSidebar();
        DrawHintBanner();

        if (_solved) DrawSolveCelebration();

        _particles.Draw();
    }

    private void DrawNightBackdrop()
    {
        // Festival night over Sông Hương: deep indigo sky melting into dark water
        Raylib.DrawRectangleGradientV(0, 0, 1280, 420, new Color(14, 22, 44, 255), new Color(32, 48, 78, 255));
        Raylib.DrawRectangleGradientV(0, 420, 1280, 300, new Color(16, 36, 58, 255), new Color(8, 18, 32, 255));

        // Stars
        for (int i = 0; i < 70; i++)
        {
            int h = ((i * 73856093) ^ 19349663) & 0x7fffffff;
            float sx = h % 1280;
            float sy = (h / 13) % 320;
            float tw = 0.5f + 0.5f * MathF.Sin(_totalTime * 2f + i);
            Raylib.DrawCircleV(new Vector2(sx, sy), (h % 3 == 0) ? 1.6f : 1f, Raylib.ColorAlpha(new Color(220, 230, 250, 255), 0.25f + 0.45f * tw));
        }

        // Full moon with halo
        Vector2 moon = new(1130f, 86f);
        Raylib.DrawCircleV(moon, 46f, Raylib.ColorAlpha(new Color(250, 244, 214, 255), 0.12f));
        Raylib.DrawCircleV(moon, 30f, new Color(250, 244, 214, 255));
        Raylib.DrawCircleV(moon + new Vector2(-8f, -6f), 5f, new Color(232, 224, 192, 255));
        Raylib.DrawCircleV(moon + new Vector2(10f, 8f), 3.5f, new Color(232, 224, 192, 255));

        // Thiên Mụ pagoda silhouette (right bank) & Trường Tiền arches silhouette (left)
        Color sil = new(10, 16, 30, 255);
        float pgX = 150f, pgY = 300f;
        for (int tier = 0; tier < 7; tier++)
        {
            float w = 64f - tier * 7f;
            Raylib.DrawRectangle((int)(pgX - w * 0.5f), (int)(pgY - (tier + 1) * 20f), (int)w, 20, sil);
            Raylib.DrawLineEx(new Vector2(pgX - w * 0.5f - 6f, pgY - (tier + 1) * 20f), new Vector2(pgX + w * 0.5f + 6f, pgY - (tier + 1) * 20f), 2f, sil);
        }
        Raylib.DrawLineEx(new Vector2(pgX, pgY - 140f), new Vector2(pgX, pgY - 156f), 2.5f, sil);
        for (int span = 0; span < 3; span++)
        {
            float sl = 950f + span * 110f;
            Vector2 prev = new(sl, 300f);
            for (int seg = 1; seg <= 10; seg++)
            {
                float u = seg / 10f;
                float x = sl + 110f * u;
                float y = 300f - 44f * 4f * u * (1f - u);
                Raylib.DrawLineEx(prev, new Vector2(x, y), 2.5f, sil);
                prev = new Vector2(x, y);
            }
        }
        Raylib.DrawRectangle(930, 298, 350, 6, sil);

        // Moon reflection shimmering on open water below
        for (int i = 0; i < 8; i++)
        {
            float ry = 630f + i * 10f;
            float rw = 30f - i * 2.5f + MathF.Sin(_totalTime * 2.5f + i) * 6f;
            Raylib.DrawEllipse((int)(1130f + MathF.Sin(_totalTime + i) * 4f), (int)ry, rw, 3f, Raylib.ColorAlpha(new Color(250, 240, 200, 255), 0.10f));
        }

        // Distant floating lanterns drifting on the dark water edges
        for (int i = 0; i < 6; i++)
        {
            float lx = (i * 230f + _totalTime * 12f) % 1380f - 50f;
            float ly = 648f + (i % 3) * 22f;
            Raylib.DrawCircleV(new Vector2(lx, ly), 9f, Raylib.ColorAlpha(new Color(255, 190, 90, 255), 0.25f));
            Raylib.DrawCircleV(new Vector2(lx, ly), 3.5f, new Color(255, 205, 110, 230));
        }
    }

    private void DrawTitle()
    {
        string title = "Thả Đèn Hoa Đăng trên Sông Hương";
        Vector2 sz = FontManager.Instance.MeasureText(title, 26f);
        FontManager.Instance.DrawTextOutline(title, new Vector2(640f - sz.X / 2f, 40f), 26f,
            new Color(255, 215, 64, 255), HanoiPalette.CartoonOutline, 2f);

        string sub = "Sông Hương Giang · Dọn luồng nước mở đường cho hoa đăng từ Bến Ngự xuôi dòng Chùa Thiên Mụ";
        Vector2 subSz = FontManager.Instance.MeasureText(sub, 12f);
        FontManager.Instance.DrawText(sub, new Vector2(640f - subSz.X / 2f, 72f), 12f, new Color(180, 210, 240, 220));
    }

    private void DrawRiverGrid()
    {
        // River base panel framed like a lacquered festival board
        float gridW = _grid.Width * CellSize;
        float gridH = _grid.Height * CellSize;
        Rectangle riverRec = new(GridOriginX, GridOriginY, gridW, gridH);
        Rectangle frame = new(GridOriginX - 10f, GridOriginY - 10f, gridW + 20f, gridH + 20f);
        VectorRenderer.DrawRoundedRect(frame, 0.06f, new Color(52, 38, 30, 255), new Color(222, 186, 110, 220), 2.5f);
        Raylib.DrawRectangleGradientV((int)riverRec.X, (int)riverRec.Y, (int)riverRec.Width, (int)riverRec.Height,
            new Color(20, 52, 82, 255), new Color(12, 34, 58, 255));

        // Flowing river wave reflections
        for (int r = 1; r < _grid.Height - 1; r++)
        {
            float wy = GridOriginY + (r + 0.5f) * CellSize;
            for (int w = 0; w < _grid.Width; w++)
            {
                float wx = GridOriginX + (w + 0.3f) * CellSize + MathF.Sin(_totalTime * 2f + r + w) * 12f;
                Raylib.DrawLineEx(new Vector2(wx, wy), new Vector2(wx + 28f, wy), 2f, new Color(40, 95, 140, 160));
            }
        }

        // Grid cells outline
        for (int x = 0; x <= _grid.Width; x++)
        {
            float lineX = GridOriginX + x * CellSize;
            Raylib.DrawLineEx(new Vector2(lineX, GridOriginY), new Vector2(lineX, GridOriginY + gridH), 1f, new Color(30, 65, 95, 120));
        }
        for (int y = 0; y <= _grid.Height; y++)
        {
            float lineY = GridOriginY + y * CellSize;
            Raylib.DrawLineEx(new Vector2(GridOriginX, lineY), new Vector2(GridOriginX + gridW, lineY), 1f, new Color(30, 65, 95, 120));
        }

        // Draw BFS path ribbon if clear
        var path = _grid.FindPath();
        if (path != null && path.Count > 1)
        {
            for (int i = 0; i < path.Count - 1; i++)
            {
                Vector2 p1 = new(GridOriginX + (path[i].x + 0.5f) * CellSize, GridOriginY + (path[i].y + 0.5f) * CellSize);
                Vector2 p2 = new(GridOriginX + (path[i + 1].x + 0.5f) * CellSize, GridOriginY + (path[i + 1].y + 0.5f) * CellSize);
                Raylib.DrawLineEx(p1, p2, 8f, Raylib.ColorAlpha(new Color(255, 215, 60, 255), 0.65f));
                Raylib.DrawCircleV(p1, 6f, new Color(255, 235, 130, 255));
            }
            Vector2 pLast = new(GridOriginX + (path[^1].x + 0.5f) * CellSize, GridOriginY + (path[^1].y + 0.5f) * CellSize);
            Raylib.DrawCircleV(pLast, 6f, new Color(255, 235, 130, 255));
        }

        // Entrance marker: Imperial Citadel Pier (0, 2) — glowing great lantern waiting to sail
        Vector2 entPos = new(GridOriginX - 12f, GridOriginY + 2.5f * CellSize);
        float pulse = 0.85f + 0.15f * MathF.Sin(_totalTime * 3f);
        Raylib.DrawCircleV(entPos, 26f * pulse, Raylib.ColorAlpha(new Color(255, 193, 60, 255), 0.25f));
        Raylib.DrawCircleV(entPos, 16f, new Color(255, 193, 7, 255));
        Raylib.DrawTriangle(entPos + new Vector2(-14f, 2f), entPos + new Vector2(0f, -12f), entPos + new Vector2(14f, 2f), new Color(255, 150, 40, 255));
        Raylib.DrawCircleV(entPos + new Vector2(0f, -4f), 4f, new Color(255, 250, 210, 255));
        FontManager.Instance.DrawTextOutline("Bến Ngự", new Vector2(GridOriginX - 76f, GridOriginY + 2.3f * CellSize), 11f, Color.White, new Color(30, 30, 40, 255), 1.5f);

        // Exit marker: Sacred Waters (6, 2) — lotus shrine of Thiên Mụ
        Vector2 exitPos = new(GridOriginX + gridW + 12f, GridOriginY + 2.5f * CellSize);
        Raylib.DrawCircleV(exitPos, 26f * pulse, Raylib.ColorAlpha(new Color(244, 143, 177, 255), 0.25f));
        Raylib.DrawCircleV(exitPos, 16f, new Color(244, 143, 177, 255));
        for (int p = 0; p < 5; p++)
        {
            float a = -MathF.PI / 2f + p * MathF.PI * 2f / 5f;
            Raylib.DrawCircleV(exitPos + new Vector2(MathF.Cos(a) * 9f, MathF.Sin(a) * 9f), 5f, new Color(252, 190, 212, 255));
        }
        Raylib.DrawCircleV(exitPos, 5f, new Color(255, 244, 214, 255));
        FontManager.Instance.DrawTextOutline("Thiên Mụ", new Vector2(GridOriginX + gridW + 18f, GridOriginY + 2.3f * CellSize), 11f, Color.White, new Color(30, 30, 40, 255), 1.5f);
    }

    private void DrawEntities()
    {
        foreach (var entity in _grid.Entities)
        {
            Vector2 visualPos = _visualPositions.TryGetValue(entity.Id, out var vp) ? vp : new Vector2(entity.X, entity.Y);
            float px = GridOriginX + visualPos.X * CellSize;
            float py = GridOriginY + visualPos.Y * CellSize;
            float pw = entity.Width * CellSize;
            float ph = entity.Height * CellSize;
            Rectangle bounds = new(px + 4f, py + 4f, pw - 8f, ph - 8f);

            if (entity.IsStaticWall)
            {
                // Mossy stone embankment with block courses
                Raylib.DrawRectangleRounded(bounds, 0.12f, 3, new Color(82, 90, 98, 255));
                Raylib.DrawRectangleRoundedLinesEx(bounds, 0.12f, 3, 1.5f, new Color(118, 128, 140, 255));
                for (float gx2 = bounds.X + 18f; gx2 < bounds.X + bounds.Width - 6f; gx2 += 24f)
                {
                    Raylib.DrawLineEx(new Vector2(gx2, bounds.Y + 4f), new Vector2(gx2, bounds.Y + bounds.Height - 4f), 1f, new Color(62, 70, 78, 255));
                }
                Raylib.DrawLineEx(new Vector2(bounds.X + 4f, bounds.Y + bounds.Height * 0.5f), new Vector2(bounds.X + bounds.Width - 4f, bounds.Y + bounds.Height * 0.5f), 1f, new Color(62, 70, 78, 255));
                int mossSeed = (int)(bounds.X * 7 + bounds.Y * 13);
                for (int m = 0; m < 3; m++)
                {
                    int h = ((mossSeed + m * 97) * 73856093) & 0x7fffffff;
                    float mx = bounds.X + 8f + h % Math.Max(1, (int)bounds.Width - 16);
                    float my = bounds.Y + 6f + (h / 11) % Math.Max(1, (int)bounds.Height - 12);
                    Raylib.DrawCircleV(new Vector2(mx, my), 3f, new Color(64, 96, 62, 190));
                }
                continue;
            }

            // Movable cultural obstacles
            switch (entity.Id)
            {
                case "Obj_DragonBoat" or "Obj_DragonBoat_1":
                    DrawDragonBoat(bounds);
                    break;
                case "Obj_FlowerRaft" or "Obj_FlowerRaft_1":
                    DrawFlowerRaft(bounds);
                    break;
                case "Obj_FisherSampan" or "Obj_FruitBoat" or "Obj_FruitBoat_1":
                    DrawFisherSampan(bounds);
                    break;
                case "Obj_WaterLilyPatch" or "Obj_LotusCluster" or "Obj_LotusCluster_1":
                    DrawWaterLilyPatch(bounds);
                    break;
                case "Obj_LanternPlatform" or "Obj_LanternPlatform_1":
                    DrawLanternPlatform(bounds);
                    break;
                default:
                    Raylib.DrawRectangleRounded(bounds, 0.25f, 4, new Color(100, 140, 180, 255));
                    break;
            }

            // Entity label
            if (EntityLabels.TryGetValue(entity.Id, out string? label))
            {
                Vector2 lsz = FontManager.Instance.MeasureText(label, 10f);
                FontManager.Instance.DrawText(label, new Vector2(bounds.X + (bounds.Width - lsz.X) / 2f, bounds.Y + bounds.Height - 14f), 10f, Color.White);
            }
        }
    }

    private static void DrawDragonBoat(Rectangle r)
    {
        // Royal Dragon Boat (Imperial Red & Gold)
        Raylib.DrawRectangleRounded(r, 0.4f, 6, new Color(185, 28, 28, 255));
        Raylib.DrawRectangleLinesEx(r, 2f, new Color(251, 191, 36, 255));

        if (r.Height > r.Width)
        {
            // Vertical boat (1x2): Dragon prow at top, canopy in center
            Vector2 prow = new(r.X + r.Width * 0.5f, r.Y + 16f);
            Raylib.DrawCircleV(prow, 12f, new Color(245, 158, 11, 255));
            Raylib.DrawCircleV(new Vector2(prow.X, prow.Y - 2f), 3f, Color.Red);

            Rectangle canopy = new(r.X + 8f, r.Y + 34f, r.Width - 16f, r.Height - 48f);
            Raylib.DrawRectangleRounded(canopy, 0.3f, 4, new Color(217, 119, 6, 255));
        }
        else
        {
            // Horizontal boat (2x1): Golden dragon prow on right
            Vector2 prow = new(r.X + r.Width - 14f, r.Y + r.Height * 0.5f);
            Raylib.DrawCircleV(prow, 12f, new Color(245, 158, 11, 255));
            Raylib.DrawCircleV(new Vector2(prow.X + 2f, prow.Y - 2f), 3f, Color.Red);

            Rectangle canopy = new(r.X + 16f, r.Y + 8f, r.Width - 42f, r.Height - 24f);
            Raylib.DrawRectangleRounded(canopy, 0.3f, 4, new Color(217, 119, 6, 255));
        }
    }

    private static void DrawFlowerRaft(Rectangle r)
    {
        // Bamboo Flower Raft with Lotus lanterns
        Raylib.DrawRectangleRounded(r, 0.35f, 6, new Color(133, 77, 39, 255));
        Raylib.DrawRectangleLinesEx(r, 2f, new Color(217, 180, 118, 255));

        // Lotus lanterns atop raft (distributed along major axis)
        Vector2 l1 = r.Width > r.Height
            ? new Vector2(r.X + r.Width * 0.3f, r.Y + r.Height * 0.5f)
            : new Vector2(r.X + r.Width * 0.5f, r.Y + r.Height * 0.3f);
        Vector2 l2 = r.Width > r.Height
            ? new Vector2(r.X + r.Width * 0.7f, r.Y + r.Height * 0.5f)
            : new Vector2(r.X + r.Width * 0.5f, r.Y + r.Height * 0.7f);

        Raylib.DrawCircleV(l1, 14f, new Color(244, 114, 182, 255));
        Raylib.DrawCircleV(l1, 6f, new Color(254, 240, 138, 255));

        Raylib.DrawCircleV(l2, 14f, new Color(244, 114, 182, 255));
        Raylib.DrawCircleV(l2, 6f, new Color(254, 240, 138, 255));
    }

    private static void DrawFisherSampan(Rectangle r)
    {
        // 1x1 Curved Wooden Fisher Sampan
        Raylib.DrawRectangleRounded(r, 0.5f, 6, new Color(78, 52, 46, 255));
        Raylib.DrawRectangleLinesEx(r, 1.5f, new Color(141, 110, 99, 255));

        // Bamboo arched cover
        Rectangle cover = new(r.X + 10f, r.Y + 12f, r.Width - 20f, r.Height - 28f);
        Raylib.DrawRectangleRounded(cover, 0.4f, 4, new Color(161, 136, 127, 255));
    }

    private static void DrawWaterLilyPatch(Rectangle r)
    {
        // 1x1 Lotus Leaf & Flower Patch
        Vector2 c = new(r.X + r.Width * 0.5f, r.Y + r.Height * 0.45f);
        Raylib.DrawCircleV(c, 24f, new Color(34, 139, 34, 240));
        Raylib.DrawCircleV(new Vector2(c.X - 6f, c.Y - 4f), 12f, new Color(255, 182, 193, 255));
        Raylib.DrawCircleV(new Vector2(c.X - 6f, c.Y - 4f), 5f, new Color(255, 255, 224, 255));
    }

    private static void DrawLanternPlatform(Rectangle r)
    {
        // 1x1 Floating Lantern Platform
        Raylib.DrawRectangleRounded(r, 0.3f, 4, new Color(101, 67, 33, 255));
        Raylib.DrawRectangleLinesEx(r, 1.5f, new Color(205, 133, 63, 255));

        Vector2 lc = new(r.X + r.Width * 0.5f, r.Y + r.Height * 0.45f);
        Raylib.DrawCircleV(lc, 12f, new Color(251, 191, 36, 255));
        Raylib.DrawCircleV(lc, 5f, new Color(255, 255, 240, 255));
    }

    private void DrawSelectionOutline()
    {
        if (_selectedObjectId == null) return;

        var entity = GetEntityById(_selectedObjectId);
        if (entity == null) return;

        Vector2 visualPos = _visualPositions.TryGetValue(entity.Id, out var vp) ? vp : new Vector2(entity.X, entity.Y);
        float px = GridOriginX + visualPos.X * CellSize;
        float py = GridOriginY + visualPos.Y * CellSize;
        float pw = entity.Width * CellSize;
        float ph = entity.Height * CellSize;

        // Glowing selection frame
        Rectangle outline = new(px + 2f, py + 2f, pw - 4f, ph - 4f);
        Raylib.DrawRectangleLinesEx(outline, 3f, new Color(255, 235, 59, 255));

        // Direction arrow indicators
        float cx = px + pw / 2f, cy = py + ph / 2f;
        if (entity.CanPushVertically)
        {
            FontManager.Instance.DrawText("▲", new Vector2(cx - 6f, cy - ph / 2f - 24f), 14f, Color.Yellow);
            FontManager.Instance.DrawText("▼", new Vector2(cx - 6f, cy + ph / 2f + 8f), 14f, Color.Yellow);
        }
        if (entity.CanPushHorizontally)
        {
            FontManager.Instance.DrawText("◄", new Vector2(cx - pw / 2f - 24f, cy - 8f), 14f, Color.Yellow);
            FontManager.Instance.DrawText("►", new Vector2(cx + pw / 2f + 8f, cy - 8f), 14f, Color.Yellow);
        }
    }

    private void DrawControlSidebar()
    {
        Rectangle sidePanel = new(1030f, 140f, 230f, 440f);
        VectorRenderer.DrawRoundedRect(sidePanel, 0.08f, new Color(24, 38, 55, 240), HanoiPalette.CartoonOutline, 1.5f);

        FontManager.Instance.DrawText("Điều Khiển:", new Vector2(1050f, 160f), 15f, new Color(255, 215, 64, 255));
        FontManager.Instance.DrawText("- Nhấp chuột / 1..5: Chọn thuyền", new Vector2(1050f, 190f), 11f, Color.White);
        FontManager.Instance.DrawText("- Kéo chuột / WASD: Đẩy vật cản", new Vector2(1050f, 210f), 11f, Color.White);
        FontManager.Instance.DrawText("- Thuyền rồng: Đẩy dọc", new Vector2(1050f, 230f), 11f, new Color(251, 191, 36, 255));
        FontManager.Instance.DrawText("- Bè hoa sen: Đẩy ngang", new Vector2(1050f, 250f), 11f, new Color(244, 114, 182, 255));
        FontManager.Instance.DrawText("- Thuyền chài: Đẩy 4 hướng", new Vector2(1050f, 270f), 11f, new Color(180, 210, 240, 255));

        FontManager.Instance.DrawText($"Số lượt đẩy: {_moveCount}", new Vector2(1050f, 320f), 14f, new Color(255, 235, 130, 255));

        // Buttons
        Rectangle btnReset = new(1050f, 485f, 92f, 36f);
        VectorRenderer.DrawRoundedRect(btnReset, 0.35f, new Color(236, 201, 75, 255), HanoiPalette.CartoonOutline, 1.5f);
        FontManager.Instance.DrawText("[R] Làm lại", new Vector2(1060f, 496f), 11f, HanoiPalette.CartoonOutline);

        Rectangle btnHint = new(1154f, 485f, 92f, 36f);
        VectorRenderer.DrawRoundedRect(btnHint, 0.35f, new Color(99, 102, 241, 255), HanoiPalette.CartoonOutline, 1.5f);
        FontManager.Instance.DrawText("[H] Gợi ý", new Vector2(1168f, 496f), 11f, Color.White);

        Rectangle btnExit = new(1050f, 530f, 196f, 36f);
        VectorRenderer.DrawRoundedRect(btnExit, 0.35f, new Color(100, 116, 139, 255), HanoiPalette.CartoonOutline, 1.5f);
        FontManager.Instance.DrawText("[Esc] Rời sông", new Vector2(1115f, 541f), 11f, Color.White);
    }

    private void DrawHintBanner()
    {
        if (string.IsNullOrEmpty(_hintMessage) || _hintTimer <= 0f) return;

        float alpha = Math.Clamp(_hintTimer, 0f, 1f);
        Vector2 sz = FontManager.Instance.MeasureText(_hintMessage, 13f);
        float boxW = Math.Min(sz.X + 36f, 960f);
        float boxX = 640f - boxW / 2f;
        float boxY = 600f;

        Rectangle box = new(boxX, boxY, boxW, 36f);
        VectorRenderer.DrawRoundedRect(box, 0.35f, Raylib.ColorAlpha(new Color(30, 58, 95, 235), alpha * 0.95f),
            HanoiPalette.CartoonOutline, 1.5f, withShadow: false);
        FontManager.Instance.DrawText(_hintMessage, new Vector2(boxX + 18f, boxY + 10f), 13f,
            Raylib.ColorAlpha(Color.White, alpha));
    }

    private void DrawSolveCelebration()
    {
        Color bloomColor = Raylib.ColorAlpha(new Color(255, 215, 60, 255), _bloomProgress * 0.35f);
        Raylib.DrawRectangle(0, 0, 1280, 720, bloomColor);

        if (_solveTimer > 0.3f)
        {
            string msg1 = "Sông Hương Rực Rỡ Ánh Hoa Đăng!";
            string msg2 = "Đại Đèn Hoa Đăng đã xuôi dòng về cõi thiêng! Cố Đô Huế phục hồi 100% sắc màu.";
            Vector2 s1 = FontManager.Instance.MeasureText(msg1, 32f);
            Vector2 s2 = FontManager.Instance.MeasureText(msg2, 16f);

            FontManager.Instance.DrawTextOutline(msg1, new Vector2(640f - s1.X / 2f, 230f), 32f,
                new Color(255, 215, 60, 255), HanoiPalette.CartoonOutline, 2.5f);
            FontManager.Instance.DrawText(msg2, new Vector2(640f - s2.X / 2f, 280f), 16f,
                new Color(250, 250, 250, 255));
        }

        if (_solveTimer > 0.8f)
        {
            Vector2 stampCenter = new(640f, 390f);
            VectorRenderer.DrawCartoonCircle(stampCenter, 60f,
                HanoiPalette.ApplyDesaturation(new Color(244, 143, 177, 255), _bloomProgress * 100f),
                HanoiPalette.CartoonOutline, 2.5f);
            string stampTxt = "Mở Cổng Hội An";
            Vector2 stSz = FontManager.Instance.MeasureText(stampTxt, 13f);
            FontManager.Instance.DrawText(stampTxt, new Vector2(640f - stSz.X / 2f, 382f), 13f, Color.White);
        }
    }

    public void Exit() { }
    public void Dispose() { }
}
