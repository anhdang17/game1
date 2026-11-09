using System;
using System.Numerics;
using Raylib_cs;

namespace GameProject.Graphics;

/// <summary>
/// Color restoration ripple bloom effect transitioning Hanoi from monochrome gray to full vibrant color.
/// Supports GLSL 330 fragment shader and automatic CPU/plain-render fallback.
/// </summary>
public sealed class ColorBloomEffect : IDisposable
{
    private RenderTexture2D _sceneTarget;
    private Shader _desatShader;
    private bool _shaderSupported;
    private int _screenWidth;
    private int _screenHeight;

    private int _locColorPercent;
    private int _locBloomCenter;
    private int _locBloomRadius;
    private int _locBloomIntensity;

    private float _currentColorPercent;
    private float _targetColorPercent;
    private Vector2 _bloomCenter;
    private float _bloomRadius;
    private float _bloomIntensity;
    private float _waveDuration = 1.5f;
    private float _waveElapsed;
    private bool _isWaveActive;

    public float CurrentColorPercent => _currentColorPercent;

    private const string FragmentShaderCode = @"
#version 330
in vec2 fragTexCoord;
in vec4 fragColor;
out vec4 finalColor;
uniform sampler2D texture0;
uniform float u_ColorPercent;
uniform vec2 u_BloomCenter;
uniform float u_BloomRadius;
uniform float u_BloomIntensity;

const vec3 LUMINANCE = vec3(0.2126, 0.7152, 0.0722);

void main()
{
    vec4 texel = texture(texture0, fragTexCoord);
    float gray = dot(texel.rgb, LUMINANCE);

    float dist = distance(gl_FragCoord.xy, u_BloomCenter);
    float ripple = 0.0;
    if (dist < u_BloomRadius && u_BloomRadius > 0.0)
    {
        ripple = smoothstep(u_BloomRadius, max(0.0, u_BloomRadius - 80.0), dist);
    }

    float effective = clamp(u_ColorPercent + ripple * 0.35, 0.0, 1.0);
    vec3 baseColor = mix(vec3(gray), texel.rgb, effective);

    if (ripple > 0.0 && effective < 1.0)
    {
        vec3 warmGlow = vec3(1.15, 0.95, 0.70) * sin(ripple * 3.14159) * u_BloomIntensity;
        baseColor += warmGlow * (1.0 - effective);
    }

    finalColor = vec4(baseColor, texel.a) * fragColor;
}";

    public void Initialize(int width, int height)
    {
        _screenWidth = width;
        _screenHeight = height;

        if (Raylib.IsWindowReady())
        {
            _sceneTarget = Raylib.LoadRenderTexture(width, height);
            _desatShader = Raylib.LoadShaderFromMemory(null, FragmentShaderCode);
            _shaderSupported = Raylib.IsShaderValid(_desatShader);

            if (_shaderSupported)
            {
                _locColorPercent = Raylib.GetShaderLocation(_desatShader, "u_ColorPercent");
                _locBloomCenter = Raylib.GetShaderLocation(_desatShader, "u_BloomCenter");
                _locBloomRadius = Raylib.GetShaderLocation(_desatShader, "u_BloomRadius");
                _locBloomIntensity = Raylib.GetShaderLocation(_desatShader, "u_BloomIntensity");
            }
        }
    }

    public void SetColorPercent(float percent)
    {
        _currentColorPercent = Math.Clamp(percent, 0f, 100f);
        _targetColorPercent = _currentColorPercent;
    }

    public void TriggerWave(Vector2 epicenter, float targetPercent, float duration = 1.5f)
    {
        _bloomCenter = epicenter;
        _targetColorPercent = Math.Clamp(targetPercent, 0f, 100f);
        _waveDuration = MathF.Max(0.1f, duration);
        _waveElapsed = 0f;
        _isWaveActive = true;
        _bloomIntensity = 1.0f;
    }

    public void Update(float dt)
    {
        if (_isWaveActive)
        {
            _waveElapsed += dt;
            float progress = Math.Clamp(_waveElapsed / _waveDuration, 0f, 1f);
            float ease = 1f - MathF.Pow(1f - progress, 3f); // Cubic ease out

            _bloomRadius = ease * MathF.Max(_screenWidth, _screenHeight) * 1.5f;
            _currentColorPercent = _currentColorPercent + (_targetColorPercent - _currentColorPercent) * ease;
            _bloomIntensity = 1f - progress;

            if (progress >= 1f)
            {
                _isWaveActive = false;
                _bloomRadius = 0f;
                _currentColorPercent = _targetColorPercent;
            }
        }
    }

    public void BeginRender()
    {
        if (Raylib.IsRenderTextureValid(_sceneTarget))
        {
            Raylib.BeginTextureMode(_sceneTarget);
            Raylib.ClearBackground(Color.Black);
        }
    }

    public void EndRenderAndDraw()
    {
        if (!Raylib.IsRenderTextureValid(_sceneTarget)) return;

        Raylib.EndTextureMode();

        // Raylib / OpenGL RenderTexture is inverted on Y-axis
        Rectangle sourceRec = new(0, 0, _sceneTarget.Texture.Width, -_sceneTarget.Texture.Height);
        Rectangle destRec = new(0, 0, _screenWidth, _screenHeight);

        if (_shaderSupported)
        {
            float normColor = _currentColorPercent / 100f;
            Raylib.SetShaderValue(_desatShader, _locColorPercent, normColor, ShaderUniformDataType.Float);
            Raylib.SetShaderValue(_desatShader, _locBloomCenter, _bloomCenter, ShaderUniformDataType.Vec2);
            Raylib.SetShaderValue(_desatShader, _locBloomRadius, _bloomRadius, ShaderUniformDataType.Float);
            Raylib.SetShaderValue(_desatShader, _locBloomIntensity, _bloomIntensity, ShaderUniformDataType.Float);

            Raylib.BeginShaderMode(_desatShader);
            Raylib.DrawTexturePro(_sceneTarget.Texture, sourceRec, destRec, Vector2.Zero, 0f, Color.White);
            Raylib.EndShaderMode();
        }
        else
        {
            Raylib.DrawTexturePro(_sceneTarget.Texture, sourceRec, destRec, Vector2.Zero, 0f, Color.White);
        }
    }

    public void Dispose()
    {
        if (Raylib.IsRenderTextureValid(_sceneTarget)) Raylib.UnloadRenderTexture(_sceneTarget);
        if (_shaderSupported) Raylib.UnloadShader(_desatShader);
        _shaderSupported = false;
    }
}

