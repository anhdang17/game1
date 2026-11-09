using System;
using System.Collections.Generic;

namespace GameProject.Core.Models;

/// <summary>
/// Predefined item identifier constants for Hanoi ingredients and key items.
/// </summary>
public static class ItemIds
{
    public const string BanhPho = "Item_BanhPho";
    public const string ThitBo = "Item_ThitBo";
    public const string NuocDung = "Item_NuocDung";
    public const string HanhLa = "Item_HanhLa";
    public const string ThaoMoc = "Item_ThaoMoc";
    public const string GiaVi = "Item_GiaVi"; // Alias for ThaoMoc garnish

    public static readonly IReadOnlyList<string> AllPhoIngredients = new[]
    {
        BanhPho,
        ThitBo,
        NuocDung,
        HanhLa,
        ThaoMoc
    };
}

/// <summary>
/// Manages collected inventory items with idempotent addition and collection queries.
/// </summary>
public sealed class Inventory
{
    private readonly List<string> _items = new();

    public IReadOnlyList<string> Items => _items.AsReadOnly();
    public int Count => _items.Count;

    public Inventory() { }

    public Inventory(IEnumerable<string>? initialItems)
    {
        if (initialItems != null)
        {
            foreach (var item in initialItems)
            {
                AddItem(item);
            }
        }
    }

    private static bool IsThaoMocOrGiaVi(string id) =>
        string.Equals(id, ItemIds.ThaoMoc, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(id, ItemIds.GiaVi, StringComparison.OrdinalIgnoreCase);

    public bool AddItem(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId)) return false;
        if (HasItem(itemId)) return false; // Idempotent: duplicates and aliases rejected

        _items.Add(itemId);
        return true;
    }

    public bool RemoveItem(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId)) return false;

        bool isAliasTarget = IsThaoMocOrGiaVi(itemId);

        for (int i = 0; i < _items.Count; i++)
        {
            if (string.Equals(_items[i], itemId, StringComparison.OrdinalIgnoreCase) ||
                (isAliasTarget && IsThaoMocOrGiaVi(_items[i])))
            {
                _items.RemoveAt(i);
                return true;
            }
        }
        return false;
    }

    public bool HasItem(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId)) return false;

        if (IsThaoMocOrGiaVi(itemId))
        {
            return _items.Contains(ItemIds.ThaoMoc, StringComparer.OrdinalIgnoreCase) ||
                   _items.Contains(ItemIds.GiaVi, StringComparer.OrdinalIgnoreCase);
        }

        return _items.Contains(itemId, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<string> GetItems() => Items;

    public void Clear() => _items.Clear();

    public List<string> ToList() => new(_items);
}
