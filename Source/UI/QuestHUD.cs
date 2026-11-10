using System;
using System.Collections.Generic;
using System.Numerics;
using GameProject.Core.Events;
using GameProject.Core.Models;
using GameProject.Core.Quests;
using GameProject.Graphics;
using Raylib_cs;

namespace GameProject.UI;

/// <summary>
/// Upper-left HUD displaying active quest title, ingredient counter (n/5), progress bar,
/// turn-in alerts, and slide-in toast notifications.
/// </summary>
public sealed class QuestHUD
{
    public sealed class ToastMessage
    {
        public string Text { get; init; } = "";
        public string ItemId { get; init; } = "";
        public float Timer { get; set; } = 2.8f;
        public float Alpha { get; set; } = 1.0f;
    }

    public Rectangle Bounds { get; private set; } = new(20f, 20f, 265f, 82f);
    private readonly List<ToastMessage> _toasts = new();
    public IReadOnlyList<ToastMessage> ActiveToasts => _toasts;

    private float _pulseTimer;

    public QuestHUD(IEventBus eventBus)
    {
        eventBus.Subscribe<ItemCollectedEvent>(OnItemCollected);
    }

    private void OnItemCollected(ItemCollectedEvent ev)
    {
        ShowToast($"+ Đã nhận: {ev.ItemName}", ev.ItemId);
    }

    public void ShowToast(string text, string itemId = "")
    {
        _toasts.Add(new ToastMessage
        {
            Text = text,
            ItemId = itemId,
            Timer = 2.8f,
            Alpha = 1.0f
        });
    }

    public static string FormatObjectiveProgress(int collectedCount, int totalCount = 5)
    {
        return $"Thu thập nguyên liệu phở: ({collectedCount}/{totalCount})";
    }

    public void Update(float deltaTime, QuestManager questManager, QuestFlags flags)
    {
        _pulseTimer += deltaTime;

        // Update toasts
        for (int i = _toasts.Count - 1; i >= 0; i--)
        {
            var t = _toasts[i];
            t.Timer -= deltaTime;
            if (t.Timer <= 0.5f)
            {
                t.Alpha = Math.Clamp(t.Timer / 0.5f, 0f, 1f);
            }
            if (t.Timer <= 0f)
            {
                _toasts.RemoveAt(i);
            }
        }
    }

    public void Draw(QuestFlags flags, int screenWidth = 1280)
    {
        if (!Raylib.IsWindowReady()) return;

        // 1. Draw Quest Tracker Card — Rustic Bamboo Pole Styling
        VectorRenderer.DrawDropShadow(Bounds, 0.25f, offsetY: 3f, alpha: 0.25f);

        // Woven bamboo background
        Raylib.DrawRectangleRounded(Bounds, 0.2f, 6, new Color(250, 246, 234, 245));

        // Woven bamboo slats (subtle horizontal reed lines)
        for (float y = Bounds.Y + 8f; y < Bounds.Y + Bounds.Height - 6f; y += 12f)
        {
            Raylib.DrawLineEx(new Vector2(Bounds.X + 12f, y), new Vector2(Bounds.X + Bounds.Width - 10f, y), 1.0f, new Color(228, 215, 190, 85));
        }

        // Segmented Bamboo Pole Frame (#C29B38)
        Raylib.DrawRectangleRoundedLinesEx(Bounds, 0.2f, 6, 2.5f, new Color(194, 155, 56, 255));
        Raylib.DrawRectangleRoundedLinesEx(new Rectangle(Bounds.X + 2f, Bounds.Y + 2f, Bounds.Width - 4f, Bounds.Height - 4f), 0.18f, 6, 1.0f, new Color(145, 110, 45, 180));

        // Bamboo notch joints along the perimeter
        DrawBambooJoint(new Vector2(Bounds.X + Bounds.Width * 0.35f, Bounds.Y + 1f), isVertical: false);
        DrawBambooJoint(new Vector2(Bounds.X + Bounds.Width * 0.68f, Bounds.Y + 1f), isVertical: false);
        DrawBambooJoint(new Vector2(Bounds.X + Bounds.Width * 0.35f, Bounds.Y + Bounds.Height - 1f), isVertical: false);
        DrawBambooJoint(new Vector2(Bounds.X + Bounds.Width * 0.68f, Bounds.Y + Bounds.Height - 1f), isVertical: false);
        DrawBambooJoint(new Vector2(Bounds.X + 1f, Bounds.Y + Bounds.Height * 0.5f), isVertical: true);
        DrawBambooJoint(new Vector2(Bounds.X + Bounds.Width - 1f, Bounds.Y + Bounds.Height * 0.5f), isVertical: true);

        // Left accent bamboo stalk
        Rectangle strip = new(Bounds.X + 4f, Bounds.Y + 6f, 5f, Bounds.Height - 12f);
        Raylib.DrawRectangleRounded(strip, 0.5f, 4, new Color(90, 155, 55, 255));
        Raylib.DrawLineEx(new Vector2(strip.X, strip.Y + strip.Height * 0.45f), new Vector2(strip.X + strip.Width, strip.Y + strip.Height * 0.45f), 1.5f, new Color(55, 105, 35, 255));

        // Header tag
        FontManager.Instance.DrawText("NHIỆM VỤ HIỆN TẠI", new Vector2(Bounds.X + 16f, Bounds.Y + 8f), 10f, new Color(140, 109, 80, 255));

        // Quest title
        FontManager.Instance.DrawText("Nấu Phở Hà Nội", new Vector2(Bounds.X + 16f, Bounds.Y + 22f), 15f, new Color(45, 35, 30, 255));

        // Objective status
        int count = flags.GetQ1CollectedCount();
        if (flags.Q1_Completed)
        {
            VectorRenderer.DrawCheckmark(new Vector2(Bounds.X + 22f, Bounds.Y + 50f), 10f, new Color(46, 133, 64, 255));
            FontManager.Instance.DrawText("Đã gom đủ 5 nguyên liệu!", new Vector2(Bounds.X + 32f, Bounds.Y + 44f), 12f, new Color(46, 133, 64, 255));
        }
        else if (flags.AreAllQ1IngredientsCollected())
        {
            float pulse = 0.7f + MathF.Sin(_pulseTimer * 6f) * 0.3f;
            Color alertColor = Raylib.ColorAlpha(new Color(192, 86, 33, 255), pulse);
            VectorRenderer.DrawFivePointStar(new Vector2(Bounds.X + 22f, Bounds.Y + 50f), 6f, alertColor);
            FontManager.Instance.DrawText("Mang nguyên liệu về cho Bà An!", new Vector2(Bounds.X + 32f, Bounds.Y + 44f), 12f, alertColor);
        }
        else
        {
            string objText = FormatObjectiveProgress(count, 5);
            FontManager.Instance.DrawText(objText, new Vector2(Bounds.X + 16f, Bounds.Y + 42f), 12f, new Color(60, 50, 45, 255));

            // Progress Bar
            float barW = 225f;
            float barH = 6f;
            Rectangle barBg = new(Bounds.X + 16f, Bounds.Y + 62f, barW, barH);
            Raylib.DrawRectangleRounded(barBg, 0.5f, 4, new Color(220, 214, 201, 255));

            float fillW = barW * (count / 5.0f);
            if (fillW > 0f)
            {
                Rectangle barFill = new(Bounds.X + 16f, Bounds.Y + 62f, fillW, barH);
                Raylib.DrawRectangleRounded(barFill, 0.5f, 4, new Color(56, 161, 105, 255));
            }
        }

        // 2. Draw Toast Notifications (Upper-Right)
        float toastStartY = 24f;
        float toastW = 280f;
        float toastH = 40f;
        float toastX = screenWidth - toastW - 20f;

        for (int i = 0; i < _toasts.Count; i++)
        {
            var toast = _toasts[i];
            float toastY = toastStartY + i * (toastH + 10f);
            Rectangle toastRec = new(toastX, toastY, toastW, toastH);

            Color bg = Raylib.ColorAlpha(new Color(30, 34, 42, 235), toast.Alpha);
            Color border = Raylib.ColorAlpha(HanoiPalette.WallYellow, toast.Alpha);
            Color textCol = Raylib.ColorAlpha(Color.White, toast.Alpha);

            VectorRenderer.DrawRoundedRect(toastRec, 0.4f, bg, border, 1.2f, withShadow: false);
            FontManager.Instance.DrawText(toast.Text, new Vector2(toastRec.X + 14f, toastRec.Y + 11f), 13f, textCol);
        }
    }

    private static void DrawBambooJoint(Vector2 center, bool isVertical)
    {
        Color darkJoint = new(125, 85, 30, 230);
        Color lightJoint = new(240, 210, 105, 255);

        if (isVertical)
        {
            Raylib.DrawLineEx(new Vector2(center.X - 3f, center.Y - 1f), new Vector2(center.X + 3f, center.Y - 1f), 1.5f, darkJoint);
            Raylib.DrawLineEx(new Vector2(center.X - 3f, center.Y + 1f), new Vector2(center.X + 3f, center.Y + 1f), 1.2f, lightJoint);
        }
        else
        {
            Raylib.DrawLineEx(new Vector2(center.X - 1f, center.Y - 3f), new Vector2(center.X - 1f, center.Y + 3f), 1.5f, darkJoint);
            Raylib.DrawLineEx(new Vector2(center.X + 1f, center.Y - 3f), new Vector2(center.X + 1f, center.Y + 3f), 1.2f, lightJoint);
        }
    }
}

