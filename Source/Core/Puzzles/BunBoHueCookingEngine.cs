using System;
using System.Collections.Generic;
using System.Linq;
using GameProject.Core.Models;


namespace GameProject.Core.Puzzles;

/// <summary>
/// General contract for puzzle engines in the game.
/// </summary>
public interface IPuzzleEngine
{
    bool IsSolved { get; }
    void ResetPuzzle();
}

/// <summary>
/// Domain contract for Bún Bò Huế cooking mini-game puzzle.
/// </summary>
public interface IBunBoPuzzleEngine : IPuzzleEngine
{
    IReadOnlyList<string?> CurrentSlots { get; }
    bool PlaceIngredient(int slotIndex, string ingredientId);
    bool RemoveIngredient(int slotIndex);
    bool ValidateAssembly(out string errorMessage);
}

/// <summary>
/// Pure domain puzzle engine for Bún Bò Huế culinary assembly.
/// Enforces the authentic 5-step layering order:
/// 1: Bún sợi to làng Vân Cù (Base thick rice noodles)
/// 2: Bắp bò hoa thái mỏng (Braised beef shank slices)
/// 3: Chả cua quết nhuyễn vàng ươm (Hue crab cakes)
/// 4: Rau sống bắp chuối bào thanh mát (Herbs & shredded banana blossom)
/// 5: Nước dùng ruốc sả thơm nồng sôi sùng sục (Spicy lemongrass & shrimp paste broth)
/// Headless-testable with zero Raylib or audio dependencies.
/// </summary>
public sealed class BunBoHueCookingEngine : IBunBoPuzzleEngine
{
    public const int TotalSlots = 5;

    public static readonly string[] CorrectSequence =
    [
        ItemIds.BunSoiTo,
        ItemIds.BapBo,
        ItemIds.ChaCua,
        ItemIds.RauSongHue,
        ItemIds.NuocDungHue
    ];

    private readonly string?[] _slots = new string?[TotalSlots];
    public IReadOnlyList<string?> CurrentSlots => _slots;
    public bool IsSolved { get; private set; }

    public bool PlaceIngredient(int slotIndex, string ingredientId)
    {
        if (slotIndex < 0 || slotIndex >= TotalSlots) return false;
        if (string.IsNullOrWhiteSpace(ingredientId)) return false;

        _slots[slotIndex] = ingredientId;
        return true;
    }

    public bool RemoveIngredient(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= TotalSlots) return false;
        _slots[slotIndex] = null;
        return true;
    }

    public bool ValidateAssembly(out string errorMessage)
    {
        errorMessage = string.Empty;
        IsSolved = false;

        // Check if any slot is empty
        for (int i = 0; i < TotalSlots; i++)
        {
            if (string.IsNullOrEmpty(_slots[i]))
            {
                errorMessage = "Tô Bún Bò Huế vẫn còn thiếu nguyên liệu, hãy hoàn thiện đủ cả 5 bước nhé Mây!";
                return false;
            }
        }

        // Check if all ingredients are distinct
        if (_slots.Distinct().Count() != TotalSlots)
        {
            errorMessage = "Mỗi bước nấu Bún Bò Huế chỉ dùng một nguyên liệu riêng biệt!";
            return false;
        }

        // Check recognized Hue ingredients
        foreach (var slot in _slots)
        {
            if (!ItemIds.AllBunBoIngredients.Contains(slot!))
            {
                errorMessage = "Ôi chà! Nguyên liệu này không có trong danh sách chuẩn của Bún Bò Huế. Thử lại với các nguyên liệu đặc trưng khác của Huế xem sao Mây nhé!";
                return false;
            }
        }

        // Step 1: Noodles must be placed at the base
        if (_slots[0] != CorrectSequence[0])
        {
            if (_slots[0] == ItemIds.NuocDungHue)
            {
                errorMessage = "Nước dùng mà chan trước khi cho bún thì bún sẽ bị nát và nguội mất, Mây ơi! Đặt bún sợi to làng Vân Cù xuống đáy tô trước nhé!";
            }
            else
            {
                errorMessage = "Hãy đặt bún sợi to làng Vân Cù đã chần nóng xuống đáy tô trước tiên!";
            }
            return false;
        }

        // Step 2: Beef shank slices over noodles
        if (_slots[1] != CorrectSequence[1])
        {
            if (_slots[1] == ItemIds.ChaCua)
            {
                errorMessage = "Hãy xếp từng lát bắp bò hoa thơm mềm lên trên lớp bún trước, rồi mới đặt chả cua Huế nhé!";
            }
            else
            {
                errorMessage = "Xếp các lát bắp bò hoa tươi ngoqn đều lên trên lớp bún nhé Mây!";
            }
            return false;
        }

        // Step 3: Crab cakes over beef
        if (_slots[2] != CorrectSequence[2])
        {
            errorMessage = "Đặt những viên chả cua quết nhuyễn vàng ươm, thơm lừng lên trên bắp bò nhé!";
            return false;
        }

        // Step 4: Fresh herbs and banana blossom
        if (_slots[3] != CorrectSequence[3])
        {
            errorMessage = "Rải một nhúm rau sống thanh mát cùng bắp chuối thái mỏng đặc trưng xứ Huế lên trên!";
            return false;
        }

        // Step 5: Boiling broth ladled over
        if (_slots[4] != CorrectSequence[4])
        {
            errorMessage = "Cuối cùng, chan muôi nước dùng ruốc sả thơm nồng, sôi sùng sục để hoàn thiện tô Bún Bò Huế trứ danh!";
            return false;
        }

        IsSolved = true;
        return true;
    }

    public void ResetPuzzle()
    {
        for (int i = 0; i < TotalSlots; i++)
        {
            _slots[i] = null;
        }
        IsSolved = false;
    }
}
