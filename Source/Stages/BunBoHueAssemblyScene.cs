using System;
using System.Collections.Generic;
using System.Numerics;
using GameProject.Core.Events;
using GameProject.Core.Hints;
using GameProject.Core.Models;
using GameProject.Core.Ports;
using GameProject.Core.Puzzles;
using GameProject.Core.Quests;
using GameProject.Graphics;
using GameProject.UI;
using Raylib_cs;

namespace GameProject.Stages;

/// <summary>
/// Quest 2: Bún Bò Huế Culinary Assembly minigame — interactively layer 5 authentic Huế ingredients into royal porcelain bowl.
/// Supports dual-mode execution: modal overlay in HueExplorationScene and standalone scene.
/// Features royal porcelain vector art, ingredient layering, steam curls, audio feedback, and +33.3% regional color bloom.
/// </summary>
public sealed class BunBoHueAssemblyScene : IScene
{
    private readonly SaveData _saveData;
    private readonly QuestManager _questManager;
    private readonly IEventBus? _eventBus;
    private readonly IAudioService? _audioService;
    private readonly IInputService? _inputService;
    private readonly LevelProgressionService _progression;

    private readonly BunBoHueCookingEngine _engine = new();

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
    private readonly float[] _slotDropTimers = new float[5];
    private readonly ParticleEmitter _particles = new();

    public bool IsSolved => _solved;
    public bool RequestExit { get; private set; }
    public bool RequestNextScene { get; private set; }
    public string? FeedbackMessage => _feedbackMessage;
    public IReadOnlyList<string?> CurrentSlots => _engine.CurrentSlots;
    public BunBoHueCookingEngine Engine => _engine;

    // Layout constants
    private const float BowlCenterX = 640f;
    private const float BowlCenterY = 300f;
    private const float BowlRadiusX = 155f;
    private const float BowlRadiusY = 96f;
    private const float SlotRadius = 32f;
    private const float CardW = 120f;
    private const float CardH = 80f;
    private const float HandStartX = 180f;
    private const float HandY = 600f;
    private const float HandSpacing = 180f;

    // Ingredient display definitions
    public static readonly (string Id, string Name, string Code)[] IngredientInfo =
    [
        (ItemIds.BunSoiTo,    "Bún Sợi To",    "[Bun]"),
        (ItemIds.BapBo,       "Bắp Bò",        "[BapBo]"),
        (ItemIds.ChaCua,      "Chả Cua",       "[ChaCua]"),
        (ItemIds.RauSongHue,  "Rau Sống Huế",  "[RauHue]"),
        (ItemIds.NuocDungHue, "Nước Dùng Huế", "[NuocHue]"),
    ];

    private static readonly string[] StepLabels =
    [
        "Lớp 1 (Đáy)", "Lớp 2 (Thịt)", "Lớp 3 (Chả)", "Lớp 4 (Rau)", "Lớp 5 (Nước)"
    ];

    public BunBoHueAssemblyScene(
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
    }

    public void Enter()
    {
        _questManager.StartQuest(HueQuests.Quest2Id);
        _feedbackMessage = "Bút Chì: Đặt bún sợi to xuống đáy tô trước, rồi bắp bò, chả cua, rau sống bắp chuối, rồi mới chan nước dùng ruốc sả nhé Mây!";
        _feedbackTimer = 6f;
        RequestExit = false;
        RequestNextScene = false;
        Array.Clear(_slotDropTimers, 0, _slotDropTimers.Length);
        _particles.Clear();
    }

    public void Update(float dt)
    {
        _totalTime += dt;

        if (_feedbackTimer > 0f) _feedbackTimer -= dt;
        if (_shakeTimer > 0f) _shakeTimer -= dt;

        for (int i = 0; i < _slotDropTimers.Length; i++)
        {
            if (_slotDropTimers[i] > 0f)
            {
                _slotDropTimers[i] = MathF.Max(0f, _slotDropTimers[i] - dt * 3.5f);
            }
        }

        if (_engine.CurrentSlots.Contains(ItemIds.NuocDungHue) || _solved)
        {
            _particles.EmitSteam(new Vector2(BowlCenterX, BowlCenterY - 10f), dt);
        }

        _particles.Update(dt);

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
        if (slotIndex >= 0 && slotIndex < BunBoHueCookingEngine.TotalSlots &&
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
        Array.Clear(_slotDropTimers, 0, _slotDropTimers.Length);
        _particles.Clear();
        _feedbackMessage = "Bắt đầu lại từ đầu nhé! Nhớ: bún sợi to Vân Cù trước tiên.";
        _feedbackTimer = 3f;
        _eventBus?.Publish(new PhoPuzzleResetEvent());
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
            Rectangle btnValidate = new(500f, 490f, 280f, 46f);
            if (Raylib.CheckCollisionPointRec(mouse, btnValidate))
            {
                TryValidate();
                return;
            }

            Rectangle btnReset = new(370f, 495f, 110f, 36f);
            if (Raylib.CheckCollisionPointRec(mouse, btnReset))
            {
                Reset();
                return;
            }

            Rectangle btnExit = new(800f, 495f, 110f, 36f);
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

        // H for hint
        if ((_inputService != null && _inputService.IsHintJustPressed) || Raylib.IsKeyPressed(KeyboardKey.H))
        {
            GiveHint();
        }
    }

    private void GiveHint()
    {
        for (int s = 0; s < BunBoHueCookingEngine.TotalSlots; s++)
        {
            if (string.IsNullOrEmpty(_engine.CurrentSlots[s]))
            {
                string targetId = BunBoHueCookingEngine.CorrectSequence[s];
                int idx = Array.FindIndex(IngredientInfo, x => x.Id == targetId);
                string name = idx >= 0 ? IngredientInfo[idx].Name : targetId;
                _feedbackMessage = $"Bút Chì: Bước {s + 1} cần '{name}', hãy đặt vào tô nhé Mây!";
                _feedbackTimer = 5f;
                _audioService?.PlaySfx(SfxType.DialogueBlip);
                return;
            }
        }

        _feedbackMessage = "Bút Chì: Cả 5 bước đã được lấp đầy, hãy nhấn [Enter] để xác nhận món ăn!";
        _feedbackTimer = 5f;
        _audioService?.PlaySfx(SfxType.DialogueBlip);
    }

    private void TryPlaceIngredient(int slotIndex, int cardIndex)
    {
        string ingredientId = IngredientInfo[cardIndex].Id;
        _engine.PlaceIngredient(slotIndex, ingredientId);
        _selectedIngredientIndex = -1;

        if (slotIndex >= 0 && slotIndex < _slotDropTimers.Length)
        {
            _slotDropTimers[slotIndex] = 1.0f;
        }

        Vector2 dropPos = GetSlotPosition(slotIndex);
        _particles.EmitSparkles(dropPos, HanoiPalette.WallYellowLight, 8);

        bool correct = slotIndex < BunBoHueCookingEngine.CorrectSequence.Length &&
                       BunBoHueCookingEngine.CorrectSequence[slotIndex] == ingredientId;
        _eventBus?.Publish(new PhoIngredientPlacedEvent(slotIndex, ingredientId, correct));
        _audioService?.PlaySfx(SfxType.ButtonClick);
    }

    private void TryValidate()
    {
        bool success = _engine.ValidateAssembly(out string errorMsg);
        _eventBus?.Publish(new PhoAssemblyAttemptedEvent(success, errorMsg));

        if (success)
        {
            _solved = true;
            _solveTimer = 0f;
            _saveData.QuestFlags.Hue_Q2_BunBoCooked = true;
            _saveData.QuestFlags.Hue_Q2_Completed = true;
            _questManager.RecordObjectiveProgress(HueQuests.Quest2Id, "cook_bun_bo");
            _questManager.CompleteQuest(HueQuests.Quest2Id);
            _progression.OnHueQuest2Completed();
            _eventBus?.Publish(new PuzzleSolvedEvent(HueQuests.Quest2Id));
            _eventBus?.Publish(new QuestCompletedEvent(HueQuests.Quest2Id, "Nấu Bún Bò Huế chuẩn vị Cố Đô"));
            _feedbackMessage = null;

            Vector2 bowlCenter = new(BowlCenterX, BowlCenterY);
            _particles.EmitConfetti(bowlCenter, 60);
            _particles.EmitSparkles(bowlCenter, HanoiPalette.LotusPink, 20);
            _particles.EmitSparkles(bowlCenter, HanoiPalette.WallYellow, 20);
            _particles.EmitSparkles(bowlCenter, new Color(255, 140, 0, 255), 20);

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
            BowlCenterX + MathF.Cos(angle) * 105f,
            BowlCenterY + MathF.Sin(angle) * 78f
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
        Raylib.DrawRectangle(0, 0, 1280, 720, Raylib.ColorAlpha(new Color(20, 16, 12, 255), 0.78f));

        // Center royal staging tray
        Rectangle trayRec = new(100f, 60f, 1080f, 620f);
        VectorRenderer.DrawRoundedRect(trayRec, 0.05f, new Color(250, 246, 235, 255), HanoiPalette.CartoonOutline, 2.5f);

        DrawTitle();
        DrawRoyalPorcelainBowl(new Vector2(BowlCenterX, BowlCenterY), BowlRadiusX, BowlRadiusY, _engine.CurrentSlots, _totalTime);
        DrawSlots();
        DrawIngredientCards();
        DrawControlButtons();
        DrawFeedback();

        if (_solved) DrawSolveOverlay();

        _particles.Draw();
    }

    private float GetBounceY(int s)
    {
        if (s < 0 || s >= _slotDropTimers.Length) return 0f;
        return -MathF.Sin((1f - _slotDropTimers[s]) * MathF.PI) * _slotDropTimers[s] * 20f;
    }

    private static int FindIngredientSlot(IReadOnlyList<string?> slots, string targetId)
    {
        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i] == targetId) return i;
        }
        return -1;
    }

    private void DrawTitle()
    {
        string title = "Ghép Tô Bún Bò Huế Chuẩn Vị Cố Đô";
        Vector2 sz = FontManager.Instance.MeasureText(title, 26f);
        FontManager.Instance.DrawTextOutline(title, new Vector2(640f - sz.X / 2f, 75f), 26f,
            new Color(230, 81, 0, 255), HanoiPalette.CartoonOutline, 2f);

        string sub = "Sứ Ngự Hoàng Gia · Bày biện 5 lớp nguyên liệu theo đúng phong vị truyền thống của Mệ Bích";
        Vector2 subSz = FontManager.Instance.MeasureText(sub, 12f);
        FontManager.Instance.DrawText(sub, new Vector2(640f - subSz.X / 2f, 108f), 12f, new Color(110, 90, 70, 220));
    }

    private void DrawRoyalPorcelainBowl(Vector2 center, float radiusX, float radiusY, IReadOnlyList<string?> slots, float animTime)
    {
        // 1. Shadow
        Raylib.DrawEllipse((int)center.X + 4, (int)center.Y + 12, radiusX + 10, radiusY * 0.65f + 8, Raylib.ColorAlpha(Color.Black, 0.22f));

        // 2. Bowl outer porcelain hull (Hue Royal Imperial Cobalt Blue / Cream)
        Color porcelainColor = _solved
            ? HanoiPalette.ApplyDesaturation(new Color(255, 253, 245, 255), 50f + _bloomProgress * 50f)
            : new Color(255, 253, 245, 255);
        Raylib.DrawEllipse((int)center.X, (int)center.Y, radiusX, radiusY, porcelainColor);
        Raylib.DrawEllipseLines((int)center.X, (int)center.Y, radiusX, radiusY, HanoiPalette.CartoonOutline);

        // 3. Royal gold rim & cobalt lotus motif
        Color imperialGold = new(218, 165, 32, 240);
        Color cobaltBlue = new(20, 60, 140, 220);
        Raylib.DrawEllipseLines((int)center.X, (int)center.Y - 2, radiusX - 4, radiusY - 3, imperialGold);
        Raylib.DrawEllipseLines((int)center.X, (int)center.Y - 2, radiusX - 8, radiusY - 5, cobaltBlue);
        for (int i = 0; i < 18; i++)
        {
            float angle = i * (MathF.PI * 2f / 18f);
            Vector2 pt = center + new Vector2(MathF.Cos(angle) * (radiusX - 11), MathF.Sin(angle) * (radiusY - 7));
            Raylib.DrawCircleV(pt, 2.5f, imperialGold);
        }

        // 4. Inner cavity well
        Raylib.DrawEllipse((int)center.X, (int)center.Y + 6, radiusX - 22, radiusY - 16, new Color(248, 242, 225, 255));

        // 5. Layering inside bowl
        bool hasNoodles = slots.Contains(ItemIds.BunSoiTo);
        bool hasMeat = slots.Contains(ItemIds.BapBo);
        bool hasCrab = slots.Contains(ItemIds.ChaCua);
        bool hasHerbs = slots.Contains(ItemIds.RauSongHue);
        bool hasBroth = slots.Contains(ItemIds.NuocDungHue);

        int noodleSlot = FindIngredientSlot(slots, ItemIds.BunSoiTo);
        int meatSlot = FindIngredientSlot(slots, ItemIds.BapBo);
        int crabSlot = FindIngredientSlot(slots, ItemIds.ChaCua);
        int herbSlot = FindIngredientSlot(slots, ItemIds.RauSongHue);
        int brothSlot = FindIngredientSlot(slots, ItemIds.NuocDungHue);

        float noodleBounceY = noodleSlot >= 0 ? GetBounceY(noodleSlot) : 0f;
        float meatBounceY = meatSlot >= 0 ? GetBounceY(meatSlot) : 0f;
        float crabBounceY = crabSlot >= 0 ? GetBounceY(crabSlot) : 0f;
        float herbBounceY = herbSlot >= 0 ? GetBounceY(herbSlot) : 0f;
        float brothBounceY = brothSlot >= 0 ? GetBounceY(brothSlot) : 0f;

        if (hasNoodles)
        {
            // Thick cylindrical Vân Cù vermicelli coils
            Color noodleColor = new(255, 252, 240, 255);
            Raylib.DrawEllipse((int)center.X, (int)(center.Y + 14 + noodleBounceY), radiusX - 44, radiusY * 0.45f, noodleColor);
            Raylib.DrawEllipseLines((int)center.X, (int)(center.Y + 14 + noodleBounceY), radiusX - 44, radiusY * 0.45f, new Color(210, 195, 160, 255));
            // Thick strand accents
            for (int s = -2; s <= 2; s++)
            {
                Vector2 p1 = new(center.X + s * 22f - 10f, center.Y + 12f + noodleBounceY);
                Vector2 p2 = new(center.X + s * 22f + 10f, center.Y + 16f + noodleBounceY);
                Raylib.DrawLineEx(p1, p2, 3f, new Color(240, 235, 215, 255));
            }
        }

        if (hasMeat)
        {
            // Braised beef shank slices with tendon rings
            Color beefColor = new(160, 50, 45, 255);
            Color tendonColor = new(230, 190, 160, 255);
            Raylib.DrawEllipse((int)center.X - 25, (int)(center.Y + 4 + meatBounceY), 26, 16, beefColor);
            Raylib.DrawEllipseLines((int)center.X - 25, (int)(center.Y + 4 + meatBounceY), 16, 8, tendonColor);

            Raylib.DrawEllipse((int)center.X + 26, (int)(center.Y + 6 + meatBounceY), 28, 17, beefColor);
            Raylib.DrawEllipseLines((int)center.X + 26, (int)(center.Y + 6 + meatBounceY), 17, 9, tendonColor);
        }

        if (hasCrab)
        {
            // Golden orange crab cake patties
            Color crabColor = new(235, 135, 45, 255);
            Color crabHighlight = new(255, 185, 95, 255);
            Raylib.DrawCircleV(new Vector2(center.X - 8f, center.Y + 10f + crabBounceY), 14f, crabColor);
            Raylib.DrawCircleV(new Vector2(center.X - 9f, center.Y + 8f + crabBounceY), 6f, crabHighlight);

            Raylib.DrawCircleV(new Vector2(center.X + 12f, center.Y - 2f + crabBounceY), 13f, crabColor);
            Raylib.DrawCircleV(new Vector2(center.X + 11f, center.Y - 4f + crabBounceY), 5f, crabHighlight);
        }

        if (hasHerbs)
        {
            // Green mint leaves & pink shredded banana blossom
            Color mintGreen = new(46, 139, 87, 240);
            Color bananaBlossomPink = new(219, 112, 147, 240);

            for (int i = 0; i < 7; i++)
            {
                float a = i * (MathF.PI * 2f / 7f) + animTime * 0.2f;
                Vector2 sp = center + new Vector2(MathF.Cos(a) * 38f, MathF.Sin(a) * 22f + 4f + herbBounceY);
                Raylib.DrawCircleV(sp, 3.5f, mintGreen);

                // Banana blossom curls
                Vector2 bp1 = center + new Vector2(MathF.Cos(a + 0.3f) * 32f, MathF.Sin(a + 0.3f) * 18f + 6f + herbBounceY);
                Vector2 bp2 = bp1 + new Vector2(6f, -3f);
                Raylib.DrawLineEx(bp1, bp2, 2f, bananaBlossomPink);
            }
        }

        if (hasBroth)
        {
            // Spicy red-orange lemongrass & ruốc broth glaze
            Color brothRedOrange = new(220, 75, 25, 120);
            Raylib.DrawEllipse((int)center.X, (int)(center.Y + 6 + brothBounceY), radiusX - 24, radiusY - 18, brothRedOrange);

            // Chili oil sheen droplets
            for (int c = 0; c < 5; c++)
            {
                float ca = c * (MathF.PI * 2f / 5f);
                Vector2 cp = center + new Vector2(MathF.Cos(ca) * 45f, MathF.Sin(ca) * 25f + 4f + brothBounceY);
                Raylib.DrawCircleV(cp, 4f, new Color(240, 50, 20, 160));
            }

            // Rising steam curls
            for (int s = 0; s < 3; s++)
            {
                float sx = center.X - 30f + s * 30f + MathF.Sin(animTime * 3f + s) * 6f;
                float sy = center.Y - 12f + brothBounceY - MathF.Abs(MathF.Sin(animTime * 2f + s)) * 25f;
                Raylib.DrawCircleV(new Vector2(sx, sy), 4f + s, Raylib.ColorAlpha(Color.White, 0.25f));
            }
        }
    }

    private void DrawSlots()
    {
        for (int i = 0; i < 5; i++)
        {
            Vector2 pos = GetSlotPosition(i);
            pos.Y += GetBounceY(i);
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
                DrawHueIngredientIcon(placedId!, pos, 20f);
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
        string header = "Khay nguyên liệu đặc sản xứ Huế:";
        FontManager.Instance.DrawText(header, new Vector2(HandStartX - 70f, HandY - CardH / 2f - 24f), 13f,
            new Color(80, 60, 40, 230));

        for (int i = 0; i < 5; i++)
        {
            Rectangle rect = GetCardRect(i);
            bool selected = _selectedIngredientIndex == i;

            if (selected) rect.Y -= 10f;

            Color cardBg = selected ? new Color(255, 240, 160, 255) : new Color(255, 252, 242, 255);
            VectorRenderer.DrawRoundedRect(rect, 0.25f, cardBg, HanoiPalette.CartoonOutline, selected ? 2.5f : 1.5f);

            // Step number badge
            Rectangle badge = new(rect.X + 4f, rect.Y + 4f, 22f, 18f);
            VectorRenderer.DrawRoundedRect(badge, 0.4f, new Color(194, 65, 12, 255), Color.White, 0f, withShadow: false);
            FontManager.Instance.DrawText($"{i + 1}", new Vector2(badge.X + 6f, badge.Y + 2f), 11f, Color.White);

            // Vector Icon
            Vector2 iconCenter = new(rect.X + CardW / 2f, rect.Y + CardH / 2f - 6f);
            DrawHueIngredientIcon(IngredientInfo[i].Id, iconCenter, 20f);

            // Name
            string name = IngredientInfo[i].Name;
            Vector2 nsz = FontManager.Instance.MeasureText(name, 11f);
            FontManager.Instance.DrawText(name, new Vector2(rect.X + CardW / 2f - nsz.X / 2f, rect.Y + CardH - 20f),
                11f, HanoiPalette.CartoonOutline);
        }
    }

    private static void DrawHueIngredientIcon(string itemId, Vector2 center, float size)
    {
        switch (itemId)
        {
            case ItemIds.BunSoiTo:
                // Thick ivory cylinder coils
                Raylib.DrawCircleV(center, size * 0.7f, new Color(252, 250, 240, 255));
                Raylib.DrawCircleLines((int)center.X, (int)center.Y, size * 0.7f, new Color(210, 195, 160, 255));
                Raylib.DrawLineEx(new Vector2(center.X - 8f, center.Y - 3f), new Vector2(center.X + 6f, center.Y - 1f), 2.5f, new Color(200, 180, 140, 255));
                Raylib.DrawLineEx(new Vector2(center.X - 5f, center.Y + 3f), new Vector2(center.X + 8f, center.Y + 4f), 2.5f, new Color(200, 180, 140, 255));
                break;

            case ItemIds.BapBo:
                // Braised beef shank with white tendon line
                Rectangle meatRec = new(center.X - size * 0.7f, center.Y - size * 0.5f, size * 1.4f, size);
                Raylib.DrawRectangleRounded(meatRec, 0.45f, 4, new Color(175, 45, 40, 255));
                Raylib.DrawLineEx(new Vector2(meatRec.X + 4f, meatRec.Y + 5f), new Vector2(meatRec.X + meatRec.Width - 4f, meatRec.Y + 7f), 2f, new Color(250, 210, 200, 255));
                Raylib.DrawLineEx(new Vector2(meatRec.X + 6f, meatRec.Y + meatRec.Height - 5f), new Vector2(meatRec.X + meatRec.Width - 6f, meatRec.Y + meatRec.Height - 6f), 1.5f, new Color(250, 210, 200, 255));
                break;

            case ItemIds.ChaCua:
                // Golden orange crab cake ball
                Raylib.DrawCircleV(center, size * 0.65f, new Color(240, 140, 40, 255));
                Raylib.DrawCircleV(new Vector2(center.X - 3f, center.Y - 3f), size * 0.25f, new Color(255, 195, 95, 255));
                Raylib.DrawCircleLines((int)center.X, (int)center.Y, size * 0.65f, new Color(190, 95, 25, 255));
                break;

            case ItemIds.RauSongHue:
                // Mint leaf & banana blossom curl
                Raylib.DrawCircleV(new Vector2(center.X - 4f, center.Y), size * 0.45f, new Color(46, 139, 87, 255));
                Raylib.DrawCircleV(new Vector2(center.X + 5f, center.Y + 2f), size * 0.35f, new Color(60, 179, 113, 255));
                Raylib.DrawLineEx(new Vector2(center.X - 5f, center.Y + 5f), new Vector2(center.X + 7f, center.Y - 6f), 2f, new Color(219, 112, 147, 255));
                break;

            case ItemIds.NuocDungHue:
                // Red-orange broth drop
                Color brothColor = new(225, 75, 25, 255);
                Raylib.DrawCircleV(new Vector2(center.X, center.Y + 2f), size * 0.55f, brothColor);
                Vector2 apex = new(center.X, center.Y - size * 0.7f);
                Raylib.DrawTriangle(new Vector2(center.X + size * 0.5f, center.Y), apex, new Vector2(center.X - size * 0.5f, center.Y), brothColor);
                Raylib.DrawCircleV(new Vector2(center.X - 2f, center.Y - 2f), 2.5f, new Color(255, 200, 120, 220));
                break;

            default:
                InventoryBar.DrawIngredientIcon(itemId, center, size, true);
                break;
        }
    }

    private void DrawControlButtons()
    {
        // Reset button
        Rectangle btnReset = new(370f, 495f, 110f, 36f);
        VectorRenderer.DrawRoundedRect(btnReset, 0.35f, new Color(236, 201, 75, 255), HanoiPalette.CartoonOutline, 1.5f);
        string rText = "[R] Làm lại";
        Vector2 rSz = FontManager.Instance.MeasureText(rText, 11f);
        FontManager.Instance.DrawText(rText, new Vector2(btnReset.X + (btnReset.Width - rSz.X) / 2f, btnReset.Y + 11f), 11f, HanoiPalette.CartoonOutline);

        // Validate button
        Rectangle btnVal = new(500f, 490f, 280f, 46f);
        VectorRenderer.DrawRoundedRect(btnVal, 0.35f, new Color(46, 125, 50, 255), HanoiPalette.CartoonOutline, 2f);
        string vText = "[Enter] Thưởng thức Bún Bò";
        Vector2 vSz = FontManager.Instance.MeasureText(vText, 13f);
        FontManager.Instance.DrawText(vText, new Vector2(btnVal.X + (btnVal.Width - vSz.X) / 2f, btnVal.Y + 14f), 13f, Color.White);

        // Exit button
        Rectangle btnExit = new(800f, 495f, 110f, 36f);
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
        float boxW = Math.Min(sz.X + 36f, 1020f);
        float boxX = 640f - boxW / 2f + shake;
        float boxY = 445f;

        Rectangle box = new(boxX, boxY, boxW, 36f);
        bool isError = !_feedbackMessage.StartsWith("Bút Chì");
        Color boxColor = isError ? new Color(220, 60, 50, 230) : new Color(40, 140, 80, 230);
        VectorRenderer.DrawRoundedRect(box, 0.35f, Raylib.ColorAlpha(boxColor, alpha * 0.95f),
            HanoiPalette.CartoonOutline, 1.5f, withShadow: false);
        FontManager.Instance.DrawText(msg, new Vector2(boxX + 18f, boxY + 10f), 13f,
            Raylib.ColorAlpha(Color.White, alpha));
    }

    private void DrawSolveOverlay()
    {
        Color bloomColor = Raylib.ColorAlpha(new Color(255, 215, 60, 255), _bloomProgress * 0.35f);
        Raylib.DrawRectangle(0, 0, 1280, 720, bloomColor);

        if (_solveTimer > 0.3f)
        {
            string msg1 = "Tô Bún Bò Huế Tuyệt Hảo!";
            string msg2 = "Cố Đô Huế bừng thêm 33.3% màu sắc! Mệ Bích rất hài lòng với tay nghề của Mây.";
            Vector2 s1 = FontManager.Instance.MeasureText(msg1, 32f);
            Vector2 s2 = FontManager.Instance.MeasureText(msg2, 16f);
            FontManager.Instance.DrawTextOutline(msg1, new Vector2(640f - s1.X / 2f, 230f), 32f,
                new Color(255, 179, 0, 255), HanoiPalette.CartoonOutline, 2.5f);
            FontManager.Instance.DrawText(msg2, new Vector2(640f - s2.X / 2f, 280f), 16f,
                new Color(50, 40, 30, 255));
        }

        if (_solveTimer > 0.8f)
        {
            Vector2 stampCenter = new(640f, 390f);
            VectorRenderer.DrawCartoonCircle(stampCenter, 60f,
                HanoiPalette.ApplyDesaturation(new Color(230, 81, 0, 255), _bloomProgress * 100f),
                HanoiPalette.CartoonOutline, 2.5f);
            string stampTxt = "+33.3% Sắc Màu";
            Vector2 stSz = FontManager.Instance.MeasureText(stampTxt, 13f);
            FontManager.Instance.DrawText(stampTxt, new Vector2(640f - stSz.X / 2f, 382f), 13f, Color.White);
        }
    }

    public void Exit() { }
    public void Dispose() { }
}
