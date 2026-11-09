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

    public bool IsSolved => _solved;
    public int MoveCount => _moveCount;
    public bool RequestNextScene { get; private set; }

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
    }

    public void Enter()
    {
        _questManager.StartQuest(HanoiQuests.Quest3Id);
        _hintMessage = "Bút Chì: Đẩy các sạp hàng để mở thông đường từ trái sang phải theo hàng giữa ra cổng cầu!";
        _hintTimer = 6f;
        RequestNextScene = false;
    }

    public void Update(float dt)
    {
        _totalTime += dt;
        if (_hintTimer > 0f) _hintTimer -= dt;

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
        _eventBus.Publish(new StallPuzzleResetEvent());
        _hintMessage = "Đã làm lại! Thử tìm đường khác nhé.";
        _hintTimer = 3f;
        _audioService?.PlaySfx(SfxType.ButtonClick);
    }

    private void HandleInput()
    {
        if (!Raylib.IsWindowReady()) return;

        Vector2 mouse = Raylib.GetMousePosition();

        if (Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            string? clickedId = GetEntityAtMouse(mouse);
            if (clickedId != null)
            {
                _selectedObjectId = (_selectedObjectId == clickedId) ? null : clickedId;
                _audioService?.PlaySfx(SfxType.ButtonClick);
            }
            else
            {
                _selectedObjectId = null;
            }
        }

        // Push selected object with WASD/arrows
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

        if (Raylib.IsKeyPressed(KeyboardKey.R))
        {
            ResetGrid();
        }

        if ((_inputService != null && _inputService.IsHintJustPressed) || (Raylib.IsWindowReady() && Raylib.IsKeyPressed(KeyboardKey.H)))
        {
            var hintMgr = new HintManager();
            _hintMessage = hintMgr.GetStallPuzzleHint(_grid.IsPathClear(), _moveCount);
            _hintTimer = 6f;
            _audioService?.PlaySfx(SfxType.DialogueBlip);
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
            float x1 = GridOriginX + entity.X * CellSize;
            float y1 = GridOriginY + entity.Y * CellSize;
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

        string sub = "Click chọn vật · WASD/Mũi tên để đẩy · [R] Làm lại · [H] Gợi ý";
        Vector2 subSz = FontManager.Instance.MeasureText(sub, 11f);
        FontManager.Instance.DrawText(sub, new Vector2(640f - subSz.X / 2f, 56f), 11f,
            new Color(100, 80, 60, 200));
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

            float x = GridOriginX + entity.X * CellSize;
            float y = GridOriginY + entity.Y * CellSize;
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

            // Label
            EntityLabels.TryGetValue(entity.Id, out string? label);
            label ??= entity.Name;
            Vector2 lsz = FontManager.Instance.MeasureText(label, 11f);
            FontManager.Instance.DrawText(label,
                new Vector2(x + w / 2f - lsz.X / 2f, y + h / 2f - 7f), 11f, HanoiPalette.CartoonOutline);

            // Push arrows if selected
            if (isSelected)
            {
                float cx = x + w / 2f, cy = y + h / 2f;
                if (entity.CanPushVertically)
                {
                    Raylib.DrawTriangle(new Vector2(cx, cy - h / 2f - 18f), new Vector2(cx - 8f, cy - h / 2f - 6f),
                        new Vector2(cx + 8f, cy - h / 2f - 6f), Raylib.ColorAlpha(Color.White, 0.8f));
                    Raylib.DrawTriangle(new Vector2(cx, cy + h / 2f + 18f), new Vector2(cx + 8f, cy + h / 2f + 6f),
                        new Vector2(cx - 8f, cy + h / 2f + 6f), Raylib.ColorAlpha(Color.White, 0.8f));
                }
                if (entity.CanPushHorizontally)
                {
                    Raylib.DrawTriangle(new Vector2(cx - w / 2f - 18f, cy), new Vector2(cx - w / 2f - 6f, cy - 8f),
                        new Vector2(cx - w / 2f - 6f, cy + 8f), Raylib.ColorAlpha(Color.White, 0.8f));
                    Raylib.DrawTriangle(new Vector2(cx + w / 2f + 18f, cy), new Vector2(cx + w / 2f + 6f, cy + 8f),
                        new Vector2(cx + w / 2f + 6f, cy - 8f), Raylib.ColorAlpha(Color.White, 0.8f));
                }
            }
        }
    }

    private void DrawPathIndicator()
    {
        var path = _grid.FindPath();
        if (path != null && path.Count > 1)
        {
            // Draw real-time connecting golden ribbon
            for (int i = 0; i < path.Count - 1; i++)
            {
                Vector2 p1 = new(GridOriginX + (path[i].x + 0.5f) * CellSize, GridOriginY + (path[i].y + 0.5f) * CellSize);
                Vector2 p2 = new(GridOriginX + (path[i + 1].x + 0.5f) * CellSize, GridOriginY + (path[i + 1].y + 0.5f) * CellSize);
                Raylib.DrawLineEx(p1, p2, 6f, new Color(255, 215, 0, 230));
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
        Rectangle legendBox = new(1050f, 260f, 196f, 220f);
        VectorRenderer.DrawRoundedRect(legendBox, 0.25f, new Color(255, 252, 240, 220),
            HanoiPalette.CartoonOutline, 1.5f);
        FontManager.Instance.DrawText("Chú thích:", new Vector2(1065f, 274f), 12f, new Color(80, 60, 40, 255));

        int li = 0;
        foreach (var (id, color) in EntityColors)
        {
            float lY = 295f + li * 36f;
            Raylib.DrawRectangleRounded(new Rectangle(1065f, lY, 20f, 16f), 0.3f, 4, color);
            EntityLabels.TryGetValue(id, out string? lbl);
            FontManager.Instance.DrawText(lbl ?? id, new Vector2(1092f, lY), 11f, new Color(60, 45, 30, 255));
            li++;
        }
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
    public void Dispose() { }
}
