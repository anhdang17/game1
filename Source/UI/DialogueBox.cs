using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using GameProject.Core.Dialogue;
using GameProject.Core.Movement;
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

    public void CompleteTypewriter() => FastForward();

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

        if (cancelPressed)
        {
            Dismiss();
            return;
        }

        // 3. Fast-forward typing if user presses confirm while typing
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
            if (Raylib.IsWindowReady())
            {
                float boxW = 880f;
                float boxX = (1280f - boxW) * 0.5f;
                float boxY = 550f;
                float choiceStartY = boxY - (_currentBranches.Count * 44f) - 10f;

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
            var selectedBranch = _currentBranches[index];
            var nextNode = _dialogueService.SelectChoice(selectedBranch);
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

                string prefix = isSelected ? $"> [{i + 1}] " : $"   [{i + 1}] ";
                string label = prefix + _currentBranches[i].ChoiceText;
                FontManager.Instance.DrawText(label, new Vector2(choiceRec.X + 12f, choiceRec.Y + 8f), 14f, HanoiPalette.CartoonOutline);
            }
        }

        // 2. Main Box Background — Handmade Dó Paper with Double Gold Trim
        Rectangle dialogRec = new(boxX, boxY, boxW, boxH);
        VectorRenderer.DrawDropShadow(dialogRec, 0.16f, offsetY: 4f, alpha: 0.28f);

        // Deckled edge background base
        Rectangle deckleRec = new(boxX - 2f, boxY - 2f, boxW + 4f, boxH + 4f);
        Raylib.DrawRectangleRounded(deckleRec, 0.18f, 6, new Color(243, 234, 211, 255));

        // Handmade dó paper body
        Raylib.DrawRectangleRounded(dialogRec, 0.16f, 6, new Color(250, 246, 236, 252));

        // Outer gold border (#D4AF37)
        Raylib.DrawRectangleRoundedLinesEx(dialogRec, 0.16f, 6, 2.2f, new Color(212, 175, 55, 255));

        // Inner secondary gold trim
        Rectangle innerRec = new(boxX + 5f, boxY + 5f, boxW - 10f, boxH - 10f);
        Raylib.DrawRectangleRoundedLinesEx(innerRec, 0.14f, 6, 1.0f, new Color(230, 202, 101, 190));

        // Corner Flourishes (Filigree accents at 4 corners)
        DrawCornerFlourish(new Vector2(boxX + 8f, boxY + 8f), 1f, 1f);
        DrawCornerFlourish(new Vector2(boxX + boxW - 8f, boxY + 8f), -1f, 1f);
        DrawCornerFlourish(new Vector2(boxX + 8f, boxY + boxH - 8f), 1f, -1f);
        DrawCornerFlourish(new Vector2(boxX + boxW - 8f, boxY + boxH - 8f), -1f, -1f);

        // 3. Avatar Portrait with Ornate Golden Locket Frame
        float avatarCenterX = (AvatarPosition == "Right") ? (boxX + boxW - 65f) : (boxX + 65f);
        float avatarCenterY = boxY + boxH * 0.5f;
        DrawAvatar(CurrentAvatar, new Vector2(avatarCenterX, avatarCenterY), 42f);

        // 4. Speaker Name Badge — Vermilion & Gold Lacquer Tag
        if (!string.IsNullOrEmpty(CurrentSpeaker))
        {
            Vector2 nameSz = FontManager.Instance.MeasureText(CurrentSpeaker, 15f);
            float badgeX = (AvatarPosition == "Right") ? (boxX + 30f) : (boxX + 130f);
            Rectangle nameBadge = new(badgeX, boxY - 16f, nameSz.X + 32f, 32f);

            // Shadow
            VectorRenderer.DrawDropShadow(nameBadge, 0.35f, offsetY: 2f, alpha: 0.25f);
            // Vermilion lacquer body (#BC2C1C)
            Raylib.DrawRectangleRounded(nameBadge, 0.35f, 6, new Color(188, 44, 28, 255));
            // Double gold border
            Raylib.DrawRectangleRoundedLinesEx(nameBadge, 0.35f, 6, 2.0f, new Color(235, 195, 65, 255));
            // Corner golden tabs
            Raylib.DrawCircleV(new Vector2(nameBadge.X + 4f, nameBadge.Y + 4f), 2.5f, new Color(255, 230, 110, 255));
            Raylib.DrawCircleV(new Vector2(nameBadge.X + nameBadge.Width - 4f, nameBadge.Y + 4f), 2.5f, new Color(255, 230, 110, 255));

            FontManager.Instance.DrawText(CurrentSpeaker, new Vector2(nameBadge.X + 16f, nameBadge.Y + 7f), 15f, new Color(255, 250, 238, 255));
        }

        // 5. Dialogue Typewriter Text
        float textX = (AvatarPosition == "Right") ? (boxX + 30f) : (boxX + 135f);
        float textW = boxW - 180f;
        DrawWrappedText(VisibleText, new Vector2(textX, boxY + 28f), textW, 16f, new Color(32, 28, 26, 255));

        // 6. Navigation Prompt in bottom-right corner
        string promptText = (_currentBranches.Count > 0 && IsTypingComplete)
            ? "[W/S / 1-3] Chọn  [Enter] Xác nhận"
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

    private static void DrawCornerFlourish(Vector2 corner, float dirX, float dirY)
    {
        Color gold = new(212, 175, 55, 230);
        Color goldLight = new(240, 215, 120, 255);

        // Filigree right-angle flourish lines
        Raylib.DrawLineEx(corner, corner + new Vector2(dirX * 14f, 0f), 1.5f, gold);
        Raylib.DrawLineEx(corner, corner + new Vector2(0f, dirY * 14f), 1.5f, gold);
        Raylib.DrawLineEx(corner + new Vector2(dirX * 3f, dirY * 3f), corner + new Vector2(dirX * 10f, dirY * 3f), 1.0f, goldLight);
        Raylib.DrawLineEx(corner + new Vector2(dirX * 3f, dirY * 3f), corner + new Vector2(dirX * 3f, dirY * 10f), 1.0f, goldLight);

        // Accent gold pip
        Raylib.DrawCircleV(corner + new Vector2(dirX * 5f, dirY * 5f), 1.8f, goldLight);
    }

    private static void DrawAvatar(string avatarKey, Vector2 center, float radius)
    {
        // Drop Shadow
        Raylib.DrawCircleV(center + new Vector2(2f, 3f), radius + 4f, Raylib.ColorAlpha(Color.Black, 0.22f));

        // Ornate Golden Locket Frame (#D4AF37)
        Raylib.DrawCircleV(center, radius + 5f, new Color(212, 175, 55, 255));
        Raylib.DrawCircleV(center, radius + 2f, new Color(130, 95, 40, 255));
        Raylib.DrawCircleV(center, radius, new Color(252, 248, 236, 255));

        // Top decorative golden crest
        Raylib.DrawCircleV(new Vector2(center.X, center.Y - radius - 3f), 3.5f, new Color(245, 215, 95, 255));
        Raylib.DrawCircleLines((int)center.X, (int)(center.Y - radius - 3f), 3.5f, new Color(150, 110, 45, 255));

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
            case "Avatar_CoLan":
            case "Avatar_ThaoMoc":
                // Friendly vendor with flat cap
                Raylib.DrawCircleV(center, 18f, HanoiPalette.SkinPeach);
                Rectangle cap = new(center.X - 18f, center.Y - 20f, 36f, 10f);
                Raylib.DrawRectangleRounded(cap, 0.4f, 4, new Color(85, 107, 47, 255));
                Raylib.DrawCircleV(new Vector2(center.X - 5f, center.Y - 2f), 2f, HanoiPalette.CartoonOutline);
                Raylib.DrawCircleV(new Vector2(center.X + 5f, center.Y - 2f), 2f, HanoiPalette.CartoonOutline);
                break;

            default:
                // Default emblem / star icon
                Raylib.DrawCircleV(center, 14f, HanoiPalette.WallYellow);
                Raylib.DrawCircleLines((int)center.X, (int)center.Y, 14f, HanoiPalette.CartoonOutline);
                VectorRenderer.DrawFivePointStar(center, 7f, HanoiPalette.CartoonOutline);
                break;
        }
    }
}
