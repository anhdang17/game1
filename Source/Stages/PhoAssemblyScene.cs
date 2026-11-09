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
/// Quest 2: Phở Assembly mini-game — drag, click, or keyboard placement of ingredient cards into the authentic 5-step order.
/// Supported both as a modal overlay in HanoiExplorationScene and standalone scene.
/// Integrates PhoCookingEngine, Bát Tràng porcelain bowl vector visuals, +33.3% color bloom wave, and audio feedback.
/// </summary>
public sealed class PhoAssemblyScene : IScene
{
    // Dependencies
    private readonly SaveData _saveData;
    private readonly QuestManager _questManager;
    private readonly IEventBus _eventBus;
    private readonly IAudioService? _audioService;
    private readonly IInputService? _inputService;
    private readonly LevelProgressionService _progression;

    // Puzzle engine
    private readonly PhoCookingEngine _engine = new();

    // UI & Puzzle State
    private int _selectedIngredientIndex = -1;
    private int _hoveredSlot = -1;
    private string? _feedbackMessage;
    private float _feedbackTimer;
    private bool _solved;
    private float _solveTimer;
    private float _totalTime;
    private float _shakeTimer;
    private float _bloomProgress;

    // Navigation state
    public bool IsSolved => _solved;
    public bool RequestExit { get; private set; }
    public bool RequestNextScene { get; private set; }
    public string? FeedbackMessage => _feedbackMessage;
    public IReadOnlyList<string?> CurrentSlots => _engine.CurrentSlots;

    // Layout constants
    private const float BowlCenterX = 640f;
    private const float BowlCenterY = 300f;
    private const float BowlRadiusX = 150f;
    private const float BowlRadiusY = 92f;
    private const float SlotRadius = 32f;
    private const float CardW = 120f;
    private const float CardH = 80f;
    private const float HandStartX = 180f;
    private const float HandY = 600f;
    private const float HandSpacing = 180f;

    // Ingredient display info
    private static readonly (string Id, string Name, string Code)[] IngredientInfo =
    [
        (ItemIds.BanhPho,  "Bánh Phở",   "[Pho]"),
        (ItemIds.ThitBo,   "Thịt Bò",    "[Bo]"),
        (ItemIds.HanhLa,   "Hành Lá",    "[Rau]"),
        (ItemIds.NuocDung, "Nước Dùng",  "[Nuoc]"),
        (ItemIds.ThaoMoc,  "Thảo Mộc",   "[GiaVi]"),
    ];

    private static readonly string[] StepLabels =
    [
        "Lớp 1 (Đáy)", "Lớp 2 (Thịt)", "Lớp 3 (Rau)", "Lớp 4 (Nước)", "Lớp 5 (Gia vị)"
    ];

    public PhoAssemblyScene(
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
        _questManager.StartQuest(HanoiQuests.Quest2Id);
        _feedbackMessage = "Bút Chì: Bánh phở xuống trước, rồi thịt, rồi hành, rồi chan nước dùng, cuối cùng thảo mộc nhé Mây!";
        _feedbackTimer = 5f;
        RequestExit = false;
        RequestNextScene = false;
    }

    public void Update(float dt)
    {
        _totalTime += dt;

        if (_feedbackTimer > 0f) _feedbackTimer -= dt;
        if (_shakeTimer > 0f) _shakeTimer -= dt;

        if (_solved)
        {
            _solveTimer += dt;
            _bloomProgress = Math.Clamp(_solveTimer / 2.0f, 0f, 1f);
            if (_solveTimer >= 2.5f)
            {
                RequestExit = true;
                RequestNextScene = true;
            }
            return;
        }

        HandleInput();
    }

    public void PlaceCard(int slotIndex, int cardIndex)
    {
        if (slotIndex >= 0 && slotIndex < PhoCookingEngine.TotalSlots &&
            cardIndex >= 0 && cardIndex < IngredientInfo.Length)
        {
            TryPlaceIngredient(slotIndex, cardIndex);
        }
    }

    public bool Validate()
    {
        TryValidate();
        return _solved;
    }

    public void Reset()
    {
        _engine.ResetPuzzle();
        _selectedIngredientIndex = -1;
        _feedbackMessage = "Bắt đầu lại từ đầu nhé! Nhớ: bánh phở trước tiên.";
        _feedbackTimer = 3f;
        _eventBus.Publish(new PhoPuzzleResetEvent());
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

        // Escape to exit
        if (Raylib.IsKeyPressed(KeyboardKey.Escape))
        {
            RequestExit = true;
            return;
        }

        // 1-5 keys to select ingredient card
        for (int i = 0; i < 5; i++)
        {
            if (Raylib.IsKeyPressed(KeyboardKey.One + i))
            {
                _selectedIngredientIndex = (_selectedIngredientIndex == i) ? -1 : i;
                _audioService?.PlaySfx(SfxType.ButtonClick);
            }
        }

        Vector2 mouse = Raylib.GetMousePosition();
        _hoveredSlot = GetHoveredSlot(mouse);

        if (Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            // Check button clicks
            Rectangle btnValidate = new(520f, 490f, 240f, 44f);
            if (Raylib.CheckCollisionPointRec(mouse, btnValidate))
            {
                TryValidate();
                return;
            }

            Rectangle btnReset = new(390f, 495f, 110f, 36f);
            if (Raylib.CheckCollisionPointRec(mouse, btnReset))
            {
                Reset();
                return;
            }

            Rectangle btnExit = new(780f, 495f, 110f, 36f);
            if (Raylib.CheckCollisionPointRec(mouse, btnExit))
            {
                RequestExit = true;
                return;
            }

            // Check card in tray
            int clickedCard = GetClickedCard(mouse);
            if (clickedCard >= 0)
            {
                _selectedIngredientIndex = (_selectedIngredientIndex == clickedCard) ? -1 : clickedCard;
                _audioService?.PlaySfx(SfxType.ButtonClick);
                return;
            }

            // Check bowl slot placement
            if (_hoveredSlot >= 0 && _selectedIngredientIndex >= 0)
            {
                TryPlaceIngredient(_hoveredSlot, _selectedIngredientIndex);
                return;
            }
        }

        // Right click to remove from slot
        if (Raylib.IsMouseButtonPressed(MouseButton.Right) && _hoveredSlot >= 0)
        {
            _engine.RemoveIngredient(_hoveredSlot);
            _audioService?.PlaySfx(SfxType.ButtonClick);
        }

        // Enter / Space to validate
        if (Raylib.IsKeyPressed(KeyboardKey.Enter) || Raylib.IsKeyPressed(KeyboardKey.Space))
        {
            TryValidate();
        }

        // R to reset
        if (Raylib.IsKeyPressed(KeyboardKey.R))
        {
            Reset();
        }

        // H for on-demand hint
        if ((_inputService != null && _inputService.IsHintJustPressed) || (Raylib.IsWindowReady() && Raylib.IsKeyPressed(KeyboardKey.H)))
        {
            int nextSlot = -1;
            for (int s = 0; s < PhoCookingEngine.TotalSlots; s++)
            {
                if (string.IsNullOrEmpty(_engine.CurrentSlots[s])) { nextSlot = s; break; }
            }
            var hintMgr = new HintManager();
            _feedbackMessage = hintMgr.GetPhoAssemblyHint(nextSlot, _engine.CurrentSlots);
            _feedbackTimer = 6f;
            _audioService?.PlaySfx(SfxType.DialogueBlip);
        }
    }

    private void TryPlaceIngredient(int slotIndex, int cardIndex)
    {
        string ingredientId = IngredientInfo[cardIndex].Id;
        _engine.PlaceIngredient(slotIndex, ingredientId);
        _selectedIngredientIndex = -1;

        bool correct = IsCorrectSoFar(slotIndex, ingredientId);
        _eventBus.Publish(new PhoIngredientPlacedEvent(slotIndex, ingredientId, correct));
        _audioService?.PlaySfx(SfxType.ButtonClick);
    }

    private static bool IsCorrectSoFar(int slot, string id)
    {
        string[] correct = [ItemIds.BanhPho, ItemIds.ThitBo, ItemIds.HanhLa, ItemIds.NuocDung, ItemIds.ThaoMoc];
        return slot < correct.Length && (correct[slot] == id || (slot == 4 && id == ItemIds.GiaVi));
    }

    private void TryValidate()
    {
        bool success = _engine.ValidateAssembly(out string errorMsg);
        _eventBus.Publish(new PhoAssemblyAttemptedEvent(success, errorMsg));

        if (success)
        {
            _solved = true;
            _solveTimer = 0f;
            _saveData.Level1State.QuestFlags.Q2_PhoAssembled = true;
            _saveData.Level1State.QuestFlags.Q2_Completed = true;
            _questManager.RecordObjectiveProgress(HanoiQuests.Quest2Id, "assemble_pho");
            _questManager.CompleteQuest(HanoiQuests.Quest2Id);
            _progression.OnQuest2Completed();
            _eventBus.Publish(new PhoAssemblyCompletedEvent());
            _feedbackMessage = null;

            _audioService?.PlaySfx(SfxType.PhoCookSuccess);
            _audioService?.PlaySfx(SfxType.ColorBloomWave);
        }
        else
        {
            _shakeTimer = 0.4f;
            _feedbackMessage = errorMsg;
            _feedbackTimer = 5f;
            _audioService?.PlaySfx(SfxType.PhoCookFailed);
        }
    }

    private static int GetHoveredSlot(Vector2 mouse)
    {
        for (int i = 0; i < 5; i++)
        {
            Vector2 slotPos = GetSlotPosition(i);
            if (Vector2.Distance(mouse, slotPos) <= SlotRadius + 8f)
                return i;
        }
        return -1;
    }

    private static int GetClickedCard(Vector2 mouse)
    {
        for (int i = 0; i < 5; i++)
        {
            Rectangle cardRect = GetCardRect(i);
            if (Raylib.CheckCollisionPointRec(mouse, cardRect))
                return i;
        }
        return -1;
    }

    private static Vector2 GetSlotPosition(int index)
    {
        float angle = -MathF.PI / 2f + index * (2f * MathF.PI / 5f);
        return new Vector2(
            BowlCenterX + MathF.Cos(angle) * 96f,
            BowlCenterY + MathF.Sin(angle) * 72f
        );
    }

    private static Rectangle GetCardRect(int index)
    {
        float x = HandStartX + index * HandSpacing;
        return new Rectangle(x - CardW / 2f, HandY - CardH / 2f, CardW, CardH);
    }

    public void Draw()
    {
        if (!Raylib.IsWindowReady()) return;

        // Dark modal backdrop overlay
        Raylib.DrawRectangle(0, 0, 1280, 720, Raylib.ColorAlpha(new Color(20, 16, 12, 255), 0.75f));

        // Center bamboo staging tray
        Rectangle trayRec = new(100f, 60f, 1080f, 620f);
        VectorRenderer.DrawRoundedRect(trayRec, 0.05f, new Color(252, 248, 235, 255), HanoiPalette.CartoonOutline, 2.5f);

        DrawTitle();
        DrawBatTrangPorcelainBowl(new Vector2(BowlCenterX, BowlCenterY), BowlRadiusX, BowlRadiusY, _engine.CurrentSlots, _totalTime);
        DrawSlots();
        DrawIngredientCards();
        DrawControlButtons();
        DrawFeedback();

        if (_solved) DrawSolveOverlay();
    }

    private void DrawTitle()
    {
        string title = "Ghép Bát Phở Hà Nội Chuẩn Vị";
        Vector2 sz = FontManager.Instance.MeasureText(title, 26f);
        FontManager.Instance.DrawTextOutline(title, new Vector2(640f - sz.X / 2f, 75f), 26f,
            HanoiPalette.WallYellow, HanoiPalette.CartoonOutline, 2f);

        string sub = "Bát Tràng hoa lam · Đặt 5 lớp nguyên liệu theo đúng thứ tự gia truyền của Bà An";
        Vector2 subSz = FontManager.Instance.MeasureText(sub, 12f);
        FontManager.Instance.DrawText(sub, new Vector2(640f - subSz.X / 2f, 108f), 12f, new Color(110, 90, 70, 220));
    }

    private void DrawBatTrangPorcelainBowl(Vector2 center, float radiusX, float radiusY, IReadOnlyList<string?> slots, float animTime)
    {
        // 1. Soft contact shadow
        Raylib.DrawEllipse((int)center.X + 4, (int)center.Y + 12, radiusX + 10, radiusY * 0.65f + 8, Raylib.ColorAlpha(Color.Black, 0.22f));

        // 2. Bowl porcelain outer hull (Off-white ceramic)
        Color porcelainColor = _solved
            ? HanoiPalette.ApplyDesaturation(new Color(255, 253, 245, 255), 50f + _bloomProgress * 50f)
            : new Color(255, 253, 245, 255);
        Raylib.DrawEllipse((int)center.X, (int)center.Y, radiusX, radiusY, porcelainColor);
        Raylib.DrawEllipseLines((int)center.X, (int)center.Y, radiusX, radiusY, HanoiPalette.CartoonOutline);

        // 3. Bát Tràng cobalt blue decorative band (Hoa lam)
        Color cobaltBlue = new(26, 86, 168, 220);
        Raylib.DrawEllipseLines((int)center.X, (int)center.Y - 2, radiusX - 6, radiusY - 4, cobaltBlue);
        for (int i = 0; i < 16; i++)
        {
            float angle = i * (MathF.PI * 2f / 16f);
            Vector2 pt = center + new Vector2(MathF.Cos(angle) * (radiusX - 10), MathF.Sin(angle) * (radiusY - 6));
            Raylib.DrawCircleV(pt, 2.5f, cobaltBlue);
        }

        // 4. Inner cavity well
        Raylib.DrawEllipse((int)center.X, (int)center.Y + 6, radiusX - 22, radiusY - 16, new Color(248, 244, 230, 255));

        // 5. Dynamic Layering inside bowl
        bool hasNoodles = slots.Contains(ItemIds.BanhPho);
        bool hasMeat = slots.Contains(ItemIds.ThitBo);
        bool hasHerbs = slots.Contains(ItemIds.HanhLa);
        bool hasBroth = slots.Contains(ItemIds.NuocDung);
        bool hasSpices = slots.Contains(ItemIds.ThaoMoc) || slots.Contains(ItemIds.GiaVi);

        if (hasNoodles)
        {
            // Ivory noodle ribbon folds at bowl bottom
            Color noodleColor = new(255, 250, 235, 255);
            Raylib.DrawEllipse((int)center.X, (int)center.Y + 14, radiusX - 44, radiusY * 0.45f, noodleColor);
            Raylib.DrawEllipseLines((int)center.X, (int)center.Y + 14, radiusX - 44, radiusY * 0.45f, new Color(220, 205, 175, 255));
        }

        if (hasMeat)
        {
            // Thin marbled beef slices
            Color beefColor = new(190, 75, 75, 255);
            Raylib.DrawEllipse((int)center.X - 20, (int)center.Y + 6, 24, 14, beefColor);
            Raylib.DrawEllipse((int)center.X + 22, (int)center.Y + 8, 26, 15, beefColor);
            Raylib.DrawEllipse((int)center.X, (int)center.Y - 2, 22, 13, beefColor);
        }

        if (hasHerbs)
        {
            // Green scallion sprinkles and cilantro
            Color scallionGreen = new(46, 139, 87, 240);
            for (int i = 0; i < 8; i++)
            {
                float a = i * (MathF.PI * 2f / 8f) + animTime * 0.2f;
                Vector2 sp = center + new Vector2(MathF.Cos(a) * 36f, MathF.Sin(a) * 22f + 4f);
                Raylib.DrawCircleV(sp, 3.5f, scallionGreen);
            }
        }

        if (hasBroth)
        {
            // Translucent golden broth glaze covering interior + rising steam curls
            Raylib.DrawEllipse((int)center.X, (int)center.Y + 6, radiusX - 24, radiusY - 18,
                Raylib.ColorAlpha(new Color(214, 158, 46, 255), 0.40f));

            // Rising steam curls
            for (int s = 0; s < 3; s++)
            {
                float sx = center.X - 30f + s * 30f + MathF.Sin(animTime * 3f + s) * 6f;
                float sy = center.Y - 10f - MathF.Abs(MathF.Sin(animTime * 2f + s)) * 25f;
                Raylib.DrawCircleV(new Vector2(sx, sy), 4f + s, Raylib.ColorAlpha(Color.White, 0.25f));
            }
        }

        if (hasSpices)
        {
            // Star anise flower garnish at center
            Color starAniseBrown = new(115, 60, 30, 255);
            Raylib.DrawPoly(center + new Vector2(0, -6), 8, 12f, 0f, starAniseBrown);
            Raylib.DrawCircleV(center + new Vector2(0, -6), 3.5f, new Color(200, 160, 100, 255));
        }
    }

    private void DrawSlots()
    {
        for (int i = 0; i < 5; i++)
        {
            Vector2 pos = GetSlotPosition(i);
            bool isHovered = _hoveredSlot == i;
            string? placedId = i < _engine.CurrentSlots.Count ? _engine.CurrentSlots[i] : null;
            bool hasFill = placedId != null;

            Color slotBg = hasFill ? new Color(255, 245, 215, 255) : new Color(238, 230, 215, 255);
            if (isHovered && !hasFill) slotBg = new Color(255, 235, 170, 255);

            Raylib.DrawCircleV(pos, SlotRadius, slotBg);
            Raylib.DrawRing(pos, SlotRadius - 2f, SlotRadius, 0f, 360f, 24, HanoiPalette.CartoonOutline);

            string stepLabel = StepLabels[i];
            Vector2 lsz = FontManager.Instance.MeasureText(stepLabel, 10f);
            FontManager.Instance.DrawText(stepLabel, new Vector2(pos.X - lsz.X / 2f, pos.Y + SlotRadius + 4f), 10f,
                new Color(100, 80, 60, 220));

            if (hasFill)
            {
                InventoryBar.DrawIngredientIcon(placedId!, pos, 22f, true);
                int infoIdx = Array.FindIndex(IngredientInfo, x => x.Id == placedId);
                if (infoIdx >= 0)
                {
                    string name = IngredientInfo[infoIdx].Name;
                    Vector2 nsz = FontManager.Instance.MeasureText(name, 9f);
                    FontManager.Instance.DrawText(name, new Vector2(pos.X - nsz.X / 2f, pos.Y + 22f), 9f,
                        new Color(70, 50, 30, 230));
                }
            }
            else
            {
                FontManager.Instance.DrawText("?", new Vector2(pos.X - 5f, pos.Y - 8f), 16f,
                    Raylib.ColorAlpha(HanoiPalette.CartoonOutline, 0.35f));
            }
        }
    }

    private void DrawIngredientCards()
    {
        string header = "Khay nguyên liệu trong balo Bé Mây:";
        FontManager.Instance.DrawText(header, new Vector2(HandStartX - 70f, HandY - CardH / 2f - 24f), 13f,
            new Color(80, 60, 40, 230));

        for (int i = 0; i < 5; i++)
        {
            Rectangle rect = GetCardRect(i);
            bool selected = _selectedIngredientIndex == i;

            if (selected) rect.Y -= 10f;

            Color cardBg = selected ? new Color(255, 240, 160, 255) : new Color(255, 252, 242, 255);
            VectorRenderer.DrawRoundedRect(rect, 0.25f, cardBg, HanoiPalette.CartoonOutline, selected ? 2.5f : 1.5f);

            // Badge
            Rectangle badge = new(rect.X + 4f, rect.Y + 4f, 22f, 18f);
            VectorRenderer.DrawRoundedRect(badge, 0.4f, HanoiPalette.TerracottaRoof, Color.White, 0f, withShadow: false);
            FontManager.Instance.DrawText($"{i + 1}", new Vector2(badge.X + 6f, badge.Y + 2f), 11f, Color.White);

            // Icon
            Vector2 iconCenter = new(rect.X + CardW / 2f, rect.Y + CardH / 2f - 6f);
            InventoryBar.DrawIngredientIcon(IngredientInfo[i].Id, iconCenter, 22f, true);

            // Name
            string name = IngredientInfo[i].Name;
            Vector2 nsz = FontManager.Instance.MeasureText(name, 11f);
            FontManager.Instance.DrawText(name, new Vector2(rect.X + CardW / 2f - nsz.X / 2f, rect.Y + CardH - 20f),
                11f, HanoiPalette.CartoonOutline);
        }
    }

    private void DrawControlButtons()
    {
        // Reset button
        Rectangle btnReset = new(390f, 495f, 110f, 36f);
        VectorRenderer.DrawRoundedRect(btnReset, 0.35f, new Color(236, 201, 75, 255), HanoiPalette.CartoonOutline, 1.5f);
        string rText = "[R] Làm lại";
        Vector2 rSz = FontManager.Instance.MeasureText(rText, 11f);
        FontManager.Instance.DrawText(rText, new Vector2(btnReset.X + (btnReset.Width - rSz.X) / 2f, btnReset.Y + 11f), 11f, HanoiPalette.CartoonOutline);

        // Validate button
        Rectangle btnVal = new(520f, 490f, 240f, 46f);
        VectorRenderer.DrawRoundedRect(btnVal, 0.35f, new Color(56, 161, 105, 255), HanoiPalette.CartoonOutline, 2f);
        string vText = "[Enter] Xác nhận bát phở";
        Vector2 vSz = FontManager.Instance.MeasureText(vText, 13f);
        FontManager.Instance.DrawText(vText, new Vector2(btnVal.X + (btnVal.Width - vSz.X) / 2f, btnVal.Y + 14f), 13f, Color.White);

        // Exit button
        Rectangle btnExit = new(780f, 495f, 110f, 36f);
        VectorRenderer.DrawRoundedRect(btnExit, 0.35f, new Color(160, 174, 192, 255), HanoiPalette.CartoonOutline, 1.5f);
        string eText = "[Esc] Thoát";
        Vector2 eSz = FontManager.Instance.MeasureText(eText, 11f);
        FontManager.Instance.DrawText(eText, new Vector2(btnExit.X + (btnExit.Width - eSz.X) / 2f, btnExit.Y + 11f), 11f, Color.White);
    }

    private void DrawFeedback()
    {
        if (string.IsNullOrEmpty(_feedbackMessage) || _feedbackTimer <= 0f) return;

        float alpha = Math.Clamp(_feedbackTimer, 0f, 1f);
        float shake = _shakeTimer > 0f ? MathF.Sin(_totalTime * 40f) * 4f : 0f;

        string msg = _feedbackMessage;
        Vector2 sz = FontManager.Instance.MeasureText(msg, 13f);
        float boxW = Math.Min(sz.X + 36f, 960f);
        float boxX = 640f - boxW / 2f + shake;
        float boxY = 445f;

        Rectangle box = new(boxX, boxY, boxW, 36f);
        bool isError = !_feedbackMessage.StartsWith("Bút Chì");
        Color boxColor = isError ? new Color(220, 60, 50, 230) : new Color(50, 160, 100, 230);
        VectorRenderer.DrawRoundedRect(box, 0.35f, Raylib.ColorAlpha(boxColor, alpha * 0.95f),
            HanoiPalette.CartoonOutline, 1.5f, withShadow: false);
        FontManager.Instance.DrawText(msg, new Vector2(boxX + 18f, boxY + 10f), 13f,
            Raylib.ColorAlpha(Color.White, alpha));
    }

    private void DrawSolveOverlay()
    {
        Color bloomColor = Raylib.ColorAlpha(new Color(255, 230, 80, 255), _bloomProgress * 0.35f);
        Raylib.DrawRectangle(0, 0, 1280, 720, bloomColor);

        if (_solveTimer > 0.3f)
        {
            string msg1 = "Bát Phở Hà Nội Hoàn Hảo!";
            string msg2 = "Phố cổ bừng thêm 33.3% màu sắc! Bà An đã chỉ đường ra Cầu Long Biên.";
            Vector2 s1 = FontManager.Instance.MeasureText(msg1, 32f);
            Vector2 s2 = FontManager.Instance.MeasureText(msg2, 16f);
            FontManager.Instance.DrawTextOutline(msg1, new Vector2(640f - s1.X / 2f, 230f), 32f,
                HanoiPalette.WallYellow, HanoiPalette.CartoonOutline, 2.5f);
            FontManager.Instance.DrawText(msg2, new Vector2(640f - s2.X / 2f, 280f), 16f,
                new Color(50, 40, 30, 255));
        }

        if (_solveTimer > 0.8f)
        {
            Vector2 stampCenter = new(640f, 390f);
            VectorRenderer.DrawCartoonCircle(stampCenter, 60f,
                HanoiPalette.ApplyDesaturation(HanoiPalette.LotusPink, _bloomProgress * 100f),
                HanoiPalette.CartoonOutline, 2.5f);
            string stampTxt = "+33.3% Sắc Màu";
            Vector2 stSz = FontManager.Instance.MeasureText(stampTxt, 13f);
            FontManager.Instance.DrawText(stampTxt, new Vector2(640f - stSz.X / 2f, 382f), 13f, Color.White);
        }
    }

    public void Exit() { }
    public void Dispose() { }
}
