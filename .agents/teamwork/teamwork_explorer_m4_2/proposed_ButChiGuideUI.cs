using System;
using System.Numerics;
using GameProject.Core.Hints;
using GameProject.Core.Models;
using GameProject.Core.Ports;
using GameProject.Graphics;
using Raylib_cs;

namespace GameProject.UI;

/// <summary>
/// Presentation component for Bút Chì companion guide.
/// Features an animated character portrait with gentle bobbing, an expanding cartoon speech bubble,
/// on-demand hint triggers ([H] or click), an idle exclamation alert (>45s), and non-intrusive sound cues.
/// Designed for headless testability (operates safely when Raylib display server is absent).
/// </summary>
public sealed class ButChiGuideUI
{
    public const float DefaultHintDuration = 6.0f;
    public const float DebounceCooldown = 0.35f;

    private readonly IHintService _hintService;
    private readonly IAudioService? _audioService;
    private readonly IInputService? _inputService;

    // Progression & Idle Tracking
    private float _idleSeconds;
    private int _failedAttemptsCount;
    private bool _showIdleAlert;

    // Hint Display State
    private string _currentHintText = string.Empty;
    private float _hintTimer;
    private bool _isHintVisible;
    private float _bubbleAlpha;
    private float _debounceTimer;
    private float _animTime;

    // Layout configuration
    public Vector2 AnchorPosition { get; set; } = new(1210f, 650f);
    public float BadgeRadius { get; set; } = 30f;

    // Public properties for status and test inspection
    public float IdleSeconds => _idleSeconds;
    public int FailedAttemptsCount => _failedAttemptsCount;
    public bool IsIdleAlertActive => _showIdleAlert;
    public bool IsHintActive => _isHintVisible && _hintTimer > 0f;
    public string CurrentHintText => _currentHintText;
    public float BubbleAlpha => _bubbleAlpha;

    public ButChiGuideUI(
        IHintService hintService,
        IAudioService? audioService = null,
        IInputService? inputService = null)
    {
        _hintService = hintService ?? throw new ArgumentNullException(nameof(hintService));
        _audioService = audioService;
        _inputService = inputService;
    }

    /// <summary>
    /// Updates companion guide state, ticks timers, checks idle thresholds, and responds to inputs.
    /// </summary>
    public void Update(float deltaTime, string activeQuestId, Level1State state)
    {
        if (deltaTime <= 0f) return;

        _animTime += deltaTime;
        if (_debounceTimer > 0f) _debounceTimer -= deltaTime;

        // 1. Idle progression tracking (only when a full hint is not already displayed)
        if (!IsHintActive)
        {
            _idleSeconds += deltaTime;
            bool shouldAlert = _hintService.CheckIdlePrompt(_idleSeconds, _failedAttemptsCount, out _);
            if (shouldAlert && !_showIdleAlert)
            {
                _showIdleAlert = true;
                _audioService?.PlaySfx(SfxType.InteractPrompt);
            }
        }

        // 2. Input detection (On-Demand [H] key or keycap button click)
        bool hintRequested = false;
        if (_debounceTimer <= 0f)
        {
            if (_inputService != null && _inputService.IsHintJustPressed)
            {
                hintRequested = true;
            }
            else if (Raylib.IsWindowReady())
            {
                if (Raylib.IsKeyPressed(KeyboardKey.H))
                {
                    hintRequested = true;
                }
                else if (Raylib.IsMouseButtonPressed(MouseButton.Left))
                {
                    Vector2 mouse = Raylib.GetMousePosition();
                    if (Vector2.Distance(mouse, AnchorPosition) <= BadgeRadius + 8f)
                    {
                        hintRequested = true;
                    }
                }
            }
        }

        if (hintRequested)
        {
            TriggerHint(activeQuestId, state);
        }

        // 3. Hint Display Timer & Smooth Alpha Transition
        if (_isHintVisible)
        {
            _hintTimer -= deltaTime;
            if (_hintTimer <= 0f)
            {
                _isHintVisible = false;
            }
            _bubbleAlpha = Math.Min(_bubbleAlpha + deltaTime * 6.0f, 1.0f);
        }
        else
        {
            _bubbleAlpha = Math.Max(_bubbleAlpha - deltaTime * 5.0f, 0.0f);
        }
    }

    /// <summary>
    /// Triggers an on-demand contextual hint evaluated from current quest state.
    /// </summary>
    public void TriggerHint(string activeQuestId, Level1State state)
    {
        _currentHintText = _hintService.GetCurrentHint(activeQuestId, state);
        _hintTimer = DefaultHintDuration;
        _isHintVisible = true;
        _showIdleAlert = false;
        _idleSeconds = 0f;
        _failedAttemptsCount = 0;
        _debounceTimer = DebounceCooldown;
        _audioService?.PlaySfx(SfxType.DialogueBlip);
    }

    /// <summary>
    /// Displays a customized contextual hint message (e.g. for mini-games).
    /// </summary>
    public void ShowCustomHint(string message, float duration = DefaultHintDuration)
    {
        _currentHintText = message;
        _hintTimer = duration;
        _isHintVisible = true;
        _showIdleAlert = false;
        _idleSeconds = 0f;
        _debounceTimer = DebounceCooldown;
        _audioService?.PlaySfx(SfxType.DialogueBlip);
    }

    /// <summary>
    /// Immediately dismisses the active hint bubble.
    /// </summary>
    public void DismissHint()
    {
        _hintTimer = 0f;
        _isHintVisible = false;
    }

    /// <summary>
    /// Notifies companion that player has achieved progression (item collected, correct move made).
    /// Resets idle timer and clears alerts.
    /// </summary>
    public void NotifyProgress()
    {
        _idleSeconds = 0f;
        _failedAttemptsCount = 0;
        _showIdleAlert = false;
    }

    /// <summary>
    /// Notifies companion that player failed a puzzle attempt.
    /// </summary>
    public void NotifyFailure()
    {
        _failedAttemptsCount++;
        if (_failedAttemptsCount >= HintManager.FailedAttemptsThreshold)
        {
            _showIdleAlert = true;
        }
    }

    /// <summary>
    /// Resets idle timer to zero without dismissing active hint.
    /// </summary>
    public void ResetIdleTimer()
    {
        _idleSeconds = 0f;
        _showIdleAlert = false;
    }

    /// <summary>
    /// Renders companion badge, hover bobbing portrait, idle exclamation badge, and animated speech bubble.
    /// Safe to call only when Raylib window is initialized.
    /// </summary>
    public void Draw(int screenWidth = 1280, int screenHeight = 720)
    {
        if (!Raylib.IsWindowReady()) return;

        Vector2 pos = AnchorPosition;

        // 1. Companion Circular Base Badge & Shadow
        Raylib.DrawCircle((int)pos.X + 2, (int)pos.Y + 3, BadgeRadius, Raylib.ColorAlpha(Color.Black, 0.22f));
        Raylib.DrawCircleV(pos, BadgeRadius, new Color(254, 252, 246, 245));
        Raylib.DrawCircleLines((int)pos.X, (int)pos.Y, BadgeRadius, HanoiPalette.CartoonOutline);

        // Subtle accent ring
        Raylib.DrawCircleLines((int)pos.X, (int)pos.Y, BadgeRadius - 2f, new Color(255, 213, 79, 180));

        // 2. Animated Companion Portrait (Gentle bobbing pencil)
        VectorRenderer.DrawButChi(pos + new Vector2(0f, -4f), _animTime, 1.15f);

        // 3. Golden [H] Keycap Tag Badge (Attached to bottom of companion circle)
        Rectangle keycapRec = new(pos.X - 18f, pos.Y + BadgeRadius - 10f, 36f, 18f);
        Raylib.DrawRectangleRounded(keycapRec, 0.4f, 4, HanoiPalette.WallYellow);
        Raylib.DrawRectangleRoundedLinesEx(keycapRec, 0.4f, 4, 1.2f, HanoiPalette.CartoonOutline);
        Vector2 hSize = FontManager.Instance.MeasureText("[H]", 11f);
        FontManager.Instance.DrawText("[H]", new Vector2(keycapRec.X + (keycapRec.Width - hSize.X) * 0.5f, keycapRec.Y + 3f), 11f, HanoiPalette.CartoonOutline);

        // 4. Idle Exclamation Alert Pulse (! when stalled > 45s or 3 failed attempts)
        if (_showIdleAlert && !IsHintActive)
        {
            float alertBob = MathF.Sin(_animTime * 6f) * 4f;
            Vector2 alertPos = new(pos.X, pos.Y - BadgeRadius - 16f + alertBob);

            Raylib.DrawCircle((int)alertPos.X, (int)alertPos.Y, 13f, new Color(230, 80, 60, 255));
            Raylib.DrawCircleLines((int)alertPos.X, (int)alertPos.Y, 13f, Color.White);
            FontManager.Instance.DrawText("!", new Vector2(alertPos.X - 3f, alertPos.Y - 8f), 15f, Color.White);

            // Nudge label pill to the left
            string promptMsg = "Bấm [H] nghe Bút Chì!";
            Vector2 msgSz = FontManager.Instance.MeasureText(promptMsg, 12f);
            Rectangle pill = new(alertPos.X - msgSz.X - 22f, alertPos.Y - 11f, msgSz.X + 16f, 22f);
            VectorRenderer.DrawRoundedRect(pill, 0.4f, new Color(254, 250, 235, 240), HanoiPalette.CartoonOutline, 1.2f, withShadow: false);
            FontManager.Instance.DrawText(promptMsg, new Vector2(pill.X + 8f, pill.Y + 4f), 12f, new Color(40, 30, 25, 255));
        }

        // 5. Animated Speech Bubble (Expands to the left when hint is active)
        if (_bubbleAlpha > 0.01f && !string.IsNullOrEmpty(_currentHintText))
        {
            DrawSpeechBubble(pos, screenWidth);
        }
    }

    private void DrawSpeechBubble(Vector2 anchor, int screenWidth)
    {
        float alpha = _bubbleAlpha;
        float bubbleW = 460f;
        float bubbleH = 92f;
        float bubbleX = anchor.X - bubbleW - 22f;
        float bubbleY = anchor.Y - bubbleH * 0.5f - 10f;

        Rectangle bubbleRec = new(bubbleX, bubbleY, bubbleW, bubbleH);

        Color fillBg = Raylib.ColorAlpha(new Color(254, 252, 246, 250), alpha);
        Color borderCol = Raylib.ColorAlpha(HanoiPalette.CartoonOutline, alpha);

        // Drop shadow & Body
        VectorRenderer.DrawDropShadow(bubbleRec, 0.25f, offsetY: 3f, alpha: 0.22f * alpha);
        Raylib.DrawRectangleRounded(bubbleRec, 0.25f, 6, fillBg);
        Raylib.DrawRectangleRoundedLinesEx(bubbleRec, 0.25f, 6, 1.6f, borderCol);

        // Right callout pointer notch (Pointing to Bút Chì)
        Vector2 tip = new(anchor.X - 6f, anchor.Y - 8f);
        Vector2 p1 = new(bubbleRec.X + bubbleRec.Width, anchor.Y - 16f);
        Vector2 p2 = new(bubbleRec.X + bubbleRec.Width, anchor.Y);
        Raylib.DrawTriangle(p1, tip, p2, fillBg);
        Raylib.DrawLineEx(p1, tip, 1.6f, borderCol);
        Raylib.DrawLineEx(p2, tip, 1.6f, borderCol);

        // Speaker Header Badge
        string headerTitle = "Bút Chì Gợi Ý";
        Vector2 headerSz = FontManager.Instance.MeasureText(headerTitle, 12f);
        Rectangle headerBadge = new(bubbleX + 16f, bubbleY - 10f, headerSz.X + 20f, 20f);
        Raylib.DrawRectangleRounded(headerBadge, 0.4f, 4, Raylib.ColorAlpha(HanoiPalette.WallYellow, alpha));
        Raylib.DrawRectangleRoundedLinesEx(headerBadge, 0.4f, 4, 1.2f, borderCol);
        FontManager.Instance.DrawText(headerTitle, new Vector2(headerBadge.X + 10f, headerBadge.Y + 3f), 12f, borderCol);

        // Multi-line wrapped hint text
        DrawWrappedText(_currentHintText, new Vector2(bubbleX + 16f, bubbleY + 16f), 13f, bubbleW - 32f,
            Raylib.ColorAlpha(new Color(38, 30, 26, 255), alpha));
    }

    private static void DrawWrappedText(string text, Vector2 origin, float size, float maxWidth, Color color)
    {
        if (string.IsNullOrEmpty(text)) return;
        string[] words = text.Split(' ');
        string line = "";
        float y = origin.Y;
        float lineH = size + 5f;

        foreach (string word in words)
        {
            string testLine = string.IsNullOrEmpty(line) ? word : line + " " + word;
            Vector2 testSz = FontManager.Instance.MeasureText(testLine, size);
            if (testSz.X > maxWidth && !string.IsNullOrEmpty(line))
            {
                FontManager.Instance.DrawText(line, new Vector2(origin.X, y), size, color);
                y += lineH;
                line = word;
            }
            else
            {
                line = testLine;
            }
        }
        if (!string.IsNullOrEmpty(line))
        {
            FontManager.Instance.DrawText(line, new Vector2(origin.X, y), size, color);
        }
    }
}
