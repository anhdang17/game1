using System;
using System.Collections.Generic;
using System.Linq;
using GameProject.Core.Models;

namespace GameProject.Core.Puzzles;

public interface IPhoPuzzleEngine
{
    IReadOnlyList<string?> CurrentSlots { get; }
    bool PlaceIngredient(int slotIndex, string ingredientId);
    bool RemoveIngredient(int slotIndex);
    bool ValidateAssembly(out string errorMessage);
    void ResetPuzzle();
    bool IsSolved { get; }
}

public sealed class PhoCookingEngine : IPhoPuzzleEngine
{
    public const int TotalSlots = 5;

    // The authentic 5-step layering order:
    // 1: Bánh phở tươi chần kỹ (Base noodles)
    // 2: Thịt bò thái mỏng (Thin beef slices)
    // 3: Hành hoa chẻ & rau mùi (Scallions & herbs)
    // 4: Nước dùng hầm xương sôi (Boiling hot bone broth)
    // 5: Gia vị chanh tiêu / Thảo mộc (Seasoning/garnish)
    private static readonly string[] CorrectSequence = new[]
    {
        ItemIds.BanhPho,
        ItemIds.ThitBo,
        ItemIds.HanhLa,
        ItemIds.NuocDung,
        ItemIds.ThaoMoc
    };

    private static readonly string[] AlternativeFinalGarnish = new[]
    {
        ItemIds.BanhPho,
        ItemIds.ThitBo,
        ItemIds.HanhLa,
        ItemIds.NuocDung,
        ItemIds.GiaVi
    };

    private readonly string?[] _slots = new string?[TotalSlots];
    public IReadOnlyList<string?> CurrentSlots => _slots;
    public bool IsSolved { get; private set; } = false;

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
                errorMessage = "Bát phở vẫn còn thiếu nguyên liệu, hãy hoàn thiện đủ cả 5 bước nhé!";
                return false;
            }
        }

        // Check if all ingredients are distinct
        var distinctCount = _slots.Distinct().Count();
        if (distinctCount != TotalSlots)
        {
            errorMessage = "Mỗi bước nấu phở chỉ dùng một nguyên liệu riêng biệt!";
            return false;
        }

        // Check step-by-step order with Bút Chì contextual guidance
        // Step 1: Noodles must be at base
        if (_slots[0] != CorrectSequence[0])
        {
            if (_slots[0] == ItemIds.NuocDung)
            {
                errorMessage = "Nước dùng mà chan trước khi cho bánh phở thì bánh sẽ bị nát và nguội mất, Mây ơi! Đặt bánh phở tươi xuống đáy bát trước nhé!";
            }
            else
            {
                errorMessage = "Hãy đặt bánh phở tươi đã chần nóng xuống đáy bát trước tiên!";
            }
            return false;
        }

        // Step 2: Meat over noodles
        if (_slots[1] != CorrectSequence[1])
        {
            if (_slots[1] == ItemIds.HanhLa)
            {
                errorMessage = "Thịt bò thái mỏng phải trải đều trên bánh phở trước, rồi mới rắc hành hoa lên trên để giữ trọn vị ngọt!";
            }
            else
            {
                errorMessage = "Xếp các lát thịt bò tươi mềm đều lên trên lớp bánh phở nhé!";
            }
            return false;
        }

        // Step 3: Scallions and herbs over meat
        if (_slots[2] != CorrectSequence[2])
        {
            errorMessage = "Rắc một nắm hành hoa chẻ và rau mùi thơm ngát lên trên lớp thịt bò tươi!";
            return false;
        }

        // Step 4: Boiling broth ladled over
        if (_slots[3] != CorrectSequence[3])
        {
            errorMessage = "Giờ là lúc chan nước dùng hầm xương đang sôi sùng sục để làm chín tái thịt và dậy mùi thảo mộc!";
            return false;
        }

        // Step 5: Garnish/Spice
        if (_slots[4] != CorrectSequence[4] && _slots[4] != AlternativeFinalGarnish[4])
        {
            errorMessage = "Cuối cùng, thêm chút chanh ớt, hạt tiêu Bắc hoặc hương thảo mộc để hoàn thiện hương vị!";
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
