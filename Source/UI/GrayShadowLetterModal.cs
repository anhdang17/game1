using System;
using System.Numerics;
using GameProject.Graphics;
using Raylib_cs;

namespace GameProject.UI;

/// <summary>
/// Story narrative cutscene modal presenting the mysterious letter left by Bóng Xám at the Long Biên Gate.
/// Features vintage parchment aesthetics, progressive typewriter text reveal, skip mechanics, and continue button.
/// </summary>
public sealed class GrayShadowLetterModal
{
    public const string LetterText =
        "Hỡi Bé Mây và cây Bút Chì nhỏ bé,\n\n" +
        "Các ngươi đã mang lại tiếng cười và sắc màu ấm áp cho những mái ngói rêu phong của Phố Cổ Hà Nội...\n" +
        "Nhưng đừng vội mừng! Màu sắc chỉ là một mảnh ký ức mong manh trôi theo dòng thời gian.\n\n" +
        "Nếu muốn tìm lại trọn vẹn Cuốn Sổ Ký Ức, hãy vượt Cầu Long Biên, xuôi về phương Nam tới Cố Đô Huế.\n" +
        "Nơi dòng Hương Giang lững lờ và những lăng tẩm hoàng cung trầm mặc đang chìm trong màn sương xám lạnh...\n\n" +
        "Ta đợi các ngươi ở đó!\n" +
        "— Bóng Xám";

    public bool IsActive { get; private set; }
    public float TypewriterProgress { get; private set; }
    public int VisibleCharCount => Math.Clamp((int)(TypewriterProgress * 35.0f), 0, LetterText.Length);
    public bool IsTypingComplete => VisibleCharCount >= LetterText.Length;
    public bool RequestClose { get; private set; }

    private float _timer;

    public void Show()
    {
        IsActive = true;
        TypewriterProgress = 0f;
        RequestClose = false;
        _timer = 0f;
    }

    public void Update(float dt, bool isConfirmPressed = false, bool isSkipPressed = false)
    {
        if (!IsActive) return;

        _timer += dt;

        bool confirm = isConfirmPressed;
        bool skip = isSkipPressed;

        if (Raylib.IsWindowReady())
        {
            confirm = confirm || Raylib.IsKeyPressed(KeyboardKey.Enter) || Raylib.IsKeyPressed(KeyboardKey.Space);
            skip = skip || Raylib.IsKeyPressed(KeyboardKey.Space) || Raylib.IsKeyPressed(KeyboardKey.Enter) ||
                   (Raylib.IsMouseButtonPressed(MouseButton.Left) && !IsTypingComplete);
        }

        if (!IsTypingComplete)
        {
            TypewriterProgress += dt;
            if (skip || confirm)
            {
                TypewriterProgress = LetterText.Length / 35.0f; // Instant fast-forward
            }
        }
        else if (confirm)
        {
            Dismiss();
        }
    }

    public void Dismiss()
    {
        IsActive = false;
        RequestClose = true;
    }

    public void Draw()
    {
        if (!IsActive || !Raylib.IsWindowReady()) return;

        // 1. Backdrop Dim
        Raylib.DrawRectangle(0, 0, 1280, 720, Raylib.ColorAlpha(Color.Black, 0.72f));

        // 2. Modal Body (Parchment)
        Rectangle modalRec = new(220f, 90f, 840f, 540f);
        VectorRenderer.DrawRoundedRect(modalRec, 0.08f, new Color(252, 248, 235, 255), HanoiPalette.CartoonOutline, 2.5f);

        Rectangle innerRec = new(230f, 100f, 820f, 520f);
        Raylib.DrawRectangleLinesEx(innerRec, 1.5f, Raylib.ColorAlpha(HanoiPalette.TerracottaRoof, 0.35f));

        // 3. Header Emblem & Title
        Vector2 emblemCenter = new(640f, 132f);
        Raylib.DrawCircleV(emblemCenter, 18f, new Color(166, 43, 43, 255));
        Raylib.DrawCircleLines((int)emblemCenter.X, (int)emblemCenter.Y, 18f, HanoiPalette.CartoonOutline);
        VectorRenderer.DrawEnvelopeIcon(emblemCenter, 18f, 13f, Color.White, new Color(245, 235, 220, 255), HanoiPalette.CartoonOutline);

        string headerTitle = "Bức Thư Bí Ẩn Của Bóng Xám";
        Vector2 hSz = FontManager.Instance.MeasureText(headerTitle, 20f);
        FontManager.Instance.DrawTextOutline(headerTitle, new Vector2(640f - hSz.X / 2f, 160f), 20f,
            HanoiPalette.TerracottaRoof, HanoiPalette.CartoonOutline, 1.5f);

        // 4. Letter Content (Typewriter reveal)
        string currentText = LetterText.Substring(0, VisibleCharCount);
        string[] lines = currentText.Split('\n');
        float lineY = 205f;
        for (int i = 0; i < lines.Length; i++)
        {
            if (!string.IsNullOrEmpty(lines[i]))
            {
                Color textColor = lines[i].StartsWith("—") ? HanoiPalette.TerracottaDark : new Color(40, 32, 28, 255);
                FontManager.Instance.DrawText(lines[i], new Vector2(265f, lineY), 14f, textColor);
            }
            lineY += 24f;
        }

        // 5. Footer Action
        if (!IsTypingComplete)
        {
            string skipHint = "[Phím Trắng / Enter] Bỏ qua";
            Vector2 sSz = FontManager.Instance.MeasureText(skipHint, 12f);
            FontManager.Instance.DrawText(skipHint, new Vector2(640f - sSz.X / 2f, 595f), 12f, new Color(130, 115, 100, 200));
        }
        else
        {
            float pulse = 0.85f + 0.15f * MathF.Sin(_timer * 4.0f);
            Rectangle btnRect = new(510f, 568f, 260f, 42f);
            Color btnColor = Raylib.ColorAlpha(new Color(56, 161, 105, 255), pulse);
            VectorRenderer.DrawRoundedRect(btnRect, 0.35f, btnColor, HanoiPalette.CartoonOutline, 2.0f);

            string btnText = "[Enter / Space] Tiếp tục hành trình ->";
            Vector2 bSz = FontManager.Instance.MeasureText(btnText, 13f);
            FontManager.Instance.DrawText(btnText, new Vector2(640f - bSz.X / 2f, btnRect.Y + 12f), 13f, Color.White);
        }
    }
}
