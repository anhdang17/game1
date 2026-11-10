using System;
using GameProject.Audio;
using GameProject.Core.Events;
using GameProject.Core.Models;
using GameProject.Core.Persistence;
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
        var eventBus = new EventBus();
        var saveLoadService = new SaveLoadService(SaveLoadService.DefaultSavePath, null, eventBus);
        var saveData = saveLoadService.Load() ?? SaveData.CreateDefault();
        var inputService = new RaylibInputService();

        var explorationScene = new HanoiExplorationScene(
            saveData: saveData,
            audioService: audioService,
            inputService: inputService,
            eventBus: eventBus
        );
        sceneManager.SwitchScene(explorationScene);

        if (args.Length > 0 && args[0] == "--screenshot")
        {
            for (int f = 0; f < 5; f++)
            {
                sceneManager.Update(0.016f);
                Raylib.BeginDrawing();
                Raylib.ClearBackground(Color.Black);
                sceneManager.Draw();
                Raylib.EndDrawing();
            }
            Raylib.TakeScreenshot("hanoi_exploration_screenshot.png");

            var puzzleScene = new StallPuzzleScene(saveData, explorationScene.QuestManager, eventBus, audioService, inputService);
            sceneManager.SwitchScene(puzzleScene);
            for (int f = 0; f < 5; f++)
            {
                sceneManager.Update(0.016f);
                Raylib.BeginDrawing();
                Raylib.ClearBackground(Color.Black);
                sceneManager.Draw();
                Raylib.EndDrawing();
            }
            Raylib.TakeScreenshot("stall_puzzle_screenshot.png");

            sceneManager.Dispose();
            FontManager.Instance.Dispose();
            VectorRenderer.UnloadStallTextures();
            if (Raylib.IsWindowReady()) Raylib.CloseWindow();
            return;
        }

        // 3. Main Game Loop (60 FPS)
        while (!Raylib.WindowShouldClose())
        {
            float dt = Raylib.GetFrameTime();
            // Cap delta time to prevent large physics jumps on window pause/unfocus
            if (dt > 0.1f) dt = 0.1f;

            // Scene coordinator transitions
            if (sceneManager.CurrentScene is HanoiExplorationScene exp && exp.RequestWorldMap)
            {
                exp.RequestWorldMap = false;
                saveLoadService.Save(saveData);
                sceneManager.SwitchScene(new WorldMapScene(saveData, eventBus, inputService));
            }
            else if (sceneManager.CurrentScene is WorldMapScene wm)
            {
                if (wm.RequestTravelTo == "Hanoi")
                {
                    sceneManager.SwitchScene(new HanoiExplorationScene(saveData, audioService: audioService, inputService: inputService, eventBus: eventBus));
                }
                else if (wm.RequestAlbum)
                {
                    sceneManager.SwitchScene(new AlbumViewerScene(saveData, eventBus, inputService));
                }
            }
            else if (sceneManager.CurrentScene is AlbumViewerScene album && album.RequestClose)
            {
                sceneManager.SwitchScene(new WorldMapScene(saveData, eventBus, inputService));
            }

            sceneManager.Update(dt);

            Raylib.BeginDrawing();
            Raylib.ClearBackground(Color.Black);

            sceneManager.Draw();

            Raylib.EndDrawing();
        }

        // 4. Clean Shutdown
        saveLoadService.Save(saveData);
        sceneManager.Dispose();
        FontManager.Instance.Dispose();
        VectorRenderer.UnloadStallTextures();

        if (Raylib.IsWindowReady())
        {
            Raylib.CloseWindow();
        }
    }
}
