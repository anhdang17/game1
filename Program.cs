using System;
using GameProject.Audio;
using GameProject.Graphics;
using GameProject.Stages;
using Raylib_cs;

namespace GameProject;

public static class Program
{
    public static void Main(string[] args)
    {
        // 1. Raylib Window Configuration
        Raylib.SetConfigFlags(ConfigFlags.Msaa4xHint | ConfigFlags.VSyncHint);
        Raylib.InitWindow(1280, 720, "Du Ký Bé Mây - Vertical Slice (Hà Nội)");
        Raylib.SetTargetFPS(60);

        // 2. Services Initialization
        using var audioService = new RaylibAudioService();
        using var sceneManager = new SceneManager();

        var explorationScene = new HanoiExplorationScene(audioService: audioService);
        sceneManager.SwitchScene(explorationScene);

        // 3. Main Game Loop (60 FPS)
        while (!Raylib.WindowShouldClose())
        {
            float dt = Raylib.GetFrameTime();
            // Cap delta time to prevent large physics jumps on window pause/unfocus
            if (dt > 0.1f) dt = 0.1f;

            sceneManager.Update(dt);

            Raylib.BeginDrawing();
            Raylib.ClearBackground(Color.Black);

            sceneManager.Draw();

            Raylib.EndDrawing();
        }

        // 4. Clean Shutdown
        sceneManager.Dispose();
        FontManager.Instance.Dispose();

        if (Raylib.IsWindowReady())
        {
            Raylib.CloseWindow();
        }
    }
}
