using System;

namespace GameProject.Stages;

/// <summary>
/// Lifecycle interface for game stages and exploration scenes.
/// </summary>
public interface IScene : IDisposable
{
    void Enter();
    void Update(float deltaTime);
    void Draw();
    void Exit();
}

/// <summary>
/// Scene coordinator managing active scene lifecycle transitions.
/// </summary>
public sealed class SceneManager : IDisposable
{
    public IScene? CurrentScene { get; private set; }

    public void SwitchScene(IScene newScene)
    {
        ArgumentNullException.ThrowIfNull(newScene);

        CurrentScene?.Exit();
        CurrentScene?.Dispose();

        CurrentScene = newScene;
        CurrentScene.Enter();
    }

    public void Update(float deltaTime)
    {
        CurrentScene?.Update(deltaTime);
    }

    public void Draw()
    {
        CurrentScene?.Draw();
    }

    public void Dispose()
    {
        CurrentScene?.Exit();
        CurrentScene?.Dispose();
        CurrentScene = null;
    }
}

