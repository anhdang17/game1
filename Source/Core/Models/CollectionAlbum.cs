using System;
using System.Collections.Generic;

namespace GameProject.Core.Models;

/// <summary>
/// Predefined identifier constants for Hanoi album items.
/// </summary>
public static class AlbumIds
{
    public const string HanoiJournalPage = "JournalPage_Hanoi_01";
    public const string HanoiLotusStamp = "Stamp_HoaSen_Hanoi";

    public const string FactCardPho = "FactCard_Hanoi_Pho";
    public const string FactCardLongBien = "FactCard_Hanoi_LongBien";
    public const string FactCardSenTayHo = "FactCard_Hanoi_SenTayHo";
    public const string FactCardPhoCo = "FactCard_Hanoi_PhoCo";
}

/// <summary>
/// Persistent state model tracking collected journal pages, travel stamps, and unlocked cultural fact cards.
/// </summary>
public sealed class CollectionAlbum
{
    private List<string> _collectedJournalPages = new();
    private List<string> _collectedStamps = new();
    private List<string> _unlockedFactCards = new();

    public List<string> CollectedJournalPages
    {
        get => _collectedJournalPages ??= new List<string>();
        set => _collectedJournalPages = value ?? new List<string>();
    }

    [System.Text.Json.Serialization.JsonIgnore]
    public List<string> JournalPages => CollectedJournalPages;

    public List<string> CollectedStamps
    {
        get => _collectedStamps ??= new List<string>();
        set => _collectedStamps = value ?? new List<string>();
    }

    [System.Text.Json.Serialization.JsonIgnore]
    public List<string> TravelStamps => CollectedStamps;

    public List<string> UnlockedFactCards
    {
        get => _unlockedFactCards ??= new List<string>();
        set => _unlockedFactCards = value ?? new List<string>();
    }

    public static CollectionAlbum CreateDefault() => new();

    public void Sanitize()
    {
        CollectedJournalPages ??= new List<string>();
        CollectedStamps ??= new List<string>();
        UnlockedFactCards ??= new List<string>();

        CollectedJournalPages.RemoveAll(string.IsNullOrWhiteSpace);
        CollectedStamps.RemoveAll(string.IsNullOrWhiteSpace);
        UnlockedFactCards.RemoveAll(string.IsNullOrWhiteSpace);
    }

    public bool AddJournalPage(string pageId)
    {
        Sanitize();
        if (!string.IsNullOrWhiteSpace(pageId) && !CollectedJournalPages.Contains(pageId, StringComparer.OrdinalIgnoreCase))
        {
            CollectedJournalPages.Add(pageId);
            return true;
        }
        return false;
    }

    public bool AddStamp(string stampId)
    {
        Sanitize();
        if (!string.IsNullOrWhiteSpace(stampId) && !CollectedStamps.Contains(stampId, StringComparer.OrdinalIgnoreCase))
        {
            CollectedStamps.Add(stampId);
            return true;
        }
        return false;
    }

    public bool AddFactCard(string cardId)
    {
        Sanitize();
        if (!string.IsNullOrWhiteSpace(cardId) && !UnlockedFactCards.Contains(cardId, StringComparer.OrdinalIgnoreCase))
        {
            UnlockedFactCards.Add(cardId);
            return true;
        }
        return false;
    }

    public bool HasJournalPage(string pageId)
    {
        Sanitize();
        return !string.IsNullOrWhiteSpace(pageId) &&
               CollectedJournalPages.Contains(pageId, StringComparer.OrdinalIgnoreCase);
    }

    public bool HasStamp(string stampId)
    {
        Sanitize();
        return !string.IsNullOrWhiteSpace(stampId) &&
               CollectedStamps.Contains(stampId, StringComparer.OrdinalIgnoreCase);
    }

    public bool HasFactCard(string cardId)
    {
        Sanitize();
        return !string.IsNullOrWhiteSpace(cardId) &&
               UnlockedFactCards.Contains(cardId, StringComparer.OrdinalIgnoreCase);
    }
}
