using System;
using System.Text.Json.Serialization;

namespace GameProject.Core.Models;

/// <summary>
/// Serializable quest and progression flags matching the exact JSON schema specifications.
/// Explicit JsonPropertyName attributes preserve PascalCase with underscores.
/// </summary>
public sealed class QuestFlags
{
    [JsonPropertyName("Q1_Started")]
    public bool Q1_Started { get; set; }

    [JsonPropertyName("Q1_BanhPho_Collected")]
    public bool Q1_BanhPho_Collected { get; set; }

    [JsonPropertyName("Q1_ThitBo_Collected")]
    public bool Q1_ThitBo_Collected { get; set; }

    [JsonPropertyName("Q1_NuocDung_Collected")]
    public bool Q1_NuocDung_Collected { get; set; }

    [JsonPropertyName("Q1_HanhLa_Collected")]
    public bool Q1_HanhLa_Collected { get; set; }

    [JsonPropertyName("Q1_ThaoMoc_Collected")]
    public bool Q1_ThaoMoc_Collected { get; set; }

    [JsonPropertyName("Q1_Completed")]
    public bool Q1_Completed { get; set; }

    [JsonPropertyName("Q2_Started")]
    public bool Q2_Started { get; set; }

    [JsonPropertyName("Q2_PhoAssembled")]
    public bool Q2_PhoAssembled { get; set; }

    [JsonPropertyName("Q2_Completed")]
    public bool Q2_Completed { get; set; }

    [JsonPropertyName("Q3_Started")]
    public bool Q3_Started { get; set; }

    [JsonPropertyName("Q3_GridSolved")]
    public bool Q3_GridSolved { get; set; }

    [JsonPropertyName("Q3_Completed")]
    public bool Q3_Completed { get; set; }

    [JsonPropertyName("Gate_LongBien_Unlocked")]
    public bool Gate_LongBien_Unlocked { get; set; }

    [JsonPropertyName("GrayShadow_LetterRead")]
    public bool GrayShadow_LetterRead { get; set; }

    [JsonPropertyName("Level1_Finished")]
    public bool Level1_Finished { get; set; }

    // Hue Chapter 2 Flags
    [JsonPropertyName("Hue_Q1_Started")]
    public bool Hue_Q1_Started { get; set; }

    [JsonPropertyName("Hue_Q1_BunSoiTo_Collected")]
    public bool Hue_Q1_BunSoiTo_Collected { get; set; }

    [JsonPropertyName("Hue_Q1_BapBo_Collected")]
    public bool Hue_Q1_BapBo_Collected { get; set; }

    [JsonPropertyName("Hue_Q1_ChaCua_Collected")]
    public bool Hue_Q1_ChaCua_Collected { get; set; }

    [JsonPropertyName("Hue_Q1_NuocDungHue_Collected")]
    public bool Hue_Q1_NuocDungHue_Collected { get; set; }

    [JsonPropertyName("Hue_Q1_RauSongHue_Collected")]
    public bool Hue_Q1_RauSongHue_Collected { get; set; }

    [JsonPropertyName("Hue_Q1_Completed")]
    public bool Hue_Q1_Completed { get; set; }

    [JsonPropertyName("Hue_Q2_Started")]
    public bool Hue_Q2_Started { get; set; }

    [JsonPropertyName("Hue_Q2_BunBoCooked")]
    public bool Hue_Q2_BunBoCooked { get; set; }

    [JsonPropertyName("Hue_Q2_Completed")]
    public bool Hue_Q2_Completed { get; set; }

    [JsonPropertyName("Hue_Q3_Started")]
    public bool Hue_Q3_Started { get; set; }

    [JsonPropertyName("Hue_Q3_PuzzleSolved")]
    public bool Hue_Q3_PuzzleSolved { get; set; }

    [JsonPropertyName("Hue_Q3_Completed")]
    public bool Hue_Q3_Completed { get; set; }

    [JsonPropertyName("Gate_HoiAn_Unlocked")]
    public bool Gate_HoiAn_Unlocked { get; set; }

    [JsonPropertyName("Hue_Finished")]
    public bool Hue_Finished { get; set; }

    public static QuestFlags CreateDefault() => new();

    public int GetQ1CollectedCount()
    {
        int count = 0;
        if (Q1_BanhPho_Collected) count++;
        if (Q1_ThitBo_Collected) count++;
        if (Q1_NuocDung_Collected) count++;
        if (Q1_HanhLa_Collected) count++;
        if (Q1_ThaoMoc_Collected) count++;
        return count;
    }

    public bool AreAllQ1IngredientsCollected() => GetQ1CollectedCount() == 5;

    public int GetHueQ1CollectedCount()
    {
        int count = 0;
        if (Hue_Q1_BunSoiTo_Collected) count++;
        if (Hue_Q1_BapBo_Collected) count++;
        if (Hue_Q1_ChaCua_Collected) count++;
        if (Hue_Q1_NuocDungHue_Collected) count++;
        if (Hue_Q1_RauSongHue_Collected) count++;
        return count;
    }

    public bool AreAllHueQ1IngredientsCollected() => GetHueQ1CollectedCount() == 5;

    public bool GetFlag(string flagName)
    {
        return flagName switch
        {
            nameof(Q1_Started) => Q1_Started,
            nameof(Q1_BanhPho_Collected) => Q1_BanhPho_Collected,
            nameof(Q1_ThitBo_Collected) => Q1_ThitBo_Collected,
            nameof(Q1_NuocDung_Collected) => Q1_NuocDung_Collected,
            nameof(Q1_HanhLa_Collected) => Q1_HanhLa_Collected,
            nameof(Q1_ThaoMoc_Collected) => Q1_ThaoMoc_Collected,
            nameof(Q1_Completed) => Q1_Completed,
            nameof(Q2_Started) => Q2_Started,
            nameof(Q2_PhoAssembled) => Q2_PhoAssembled,
            nameof(Q2_Completed) => Q2_Completed,
            nameof(Q3_Started) => Q3_Started,
            nameof(Q3_GridSolved) => Q3_GridSolved,
            nameof(Q3_Completed) => Q3_Completed,
            nameof(Gate_LongBien_Unlocked) => Gate_LongBien_Unlocked,
            nameof(GrayShadow_LetterRead) => GrayShadow_LetterRead,
            nameof(Level1_Finished) => Level1_Finished,

            nameof(Hue_Q1_Started) => Hue_Q1_Started,
            nameof(Hue_Q1_BunSoiTo_Collected) => Hue_Q1_BunSoiTo_Collected,
            nameof(Hue_Q1_BapBo_Collected) => Hue_Q1_BapBo_Collected,
            nameof(Hue_Q1_ChaCua_Collected) => Hue_Q1_ChaCua_Collected,
            nameof(Hue_Q1_NuocDungHue_Collected) => Hue_Q1_NuocDungHue_Collected,
            nameof(Hue_Q1_RauSongHue_Collected) => Hue_Q1_RauSongHue_Collected,
            nameof(Hue_Q1_Completed) => Hue_Q1_Completed,
            nameof(Hue_Q2_Started) => Hue_Q2_Started,
            nameof(Hue_Q2_BunBoCooked) => Hue_Q2_BunBoCooked,
            nameof(Hue_Q2_Completed) => Hue_Q2_Completed,
            nameof(Hue_Q3_Started) => Hue_Q3_Started,
            nameof(Hue_Q3_PuzzleSolved) => Hue_Q3_PuzzleSolved,
            nameof(Hue_Q3_Completed) => Hue_Q3_Completed,
            nameof(Gate_HoiAn_Unlocked) => Gate_HoiAn_Unlocked,
            nameof(Hue_Finished) => Hue_Finished,
            _ => false
        };
    }

    public void SetFlag(string flagName, bool value)
    {
        switch (flagName)
        {
            case nameof(Q1_Started): Q1_Started = value; break;
            case nameof(Q1_BanhPho_Collected): Q1_BanhPho_Collected = value; break;
            case nameof(Q1_ThitBo_Collected): Q1_ThitBo_Collected = value; break;
            case nameof(Q1_NuocDung_Collected): Q1_NuocDung_Collected = value; break;
            case nameof(Q1_HanhLa_Collected): Q1_HanhLa_Collected = value; break;
            case nameof(Q1_ThaoMoc_Collected): Q1_ThaoMoc_Collected = value; break;
            case nameof(Q1_Completed): Q1_Completed = value; break;
            case nameof(Q2_Started): Q2_Started = value; break;
            case nameof(Q2_PhoAssembled): Q2_PhoAssembled = value; break;
            case nameof(Q2_Completed): Q2_Completed = value; break;
            case nameof(Q3_Started): Q3_Started = value; break;
            case nameof(Q3_GridSolved): Q3_GridSolved = value; break;
            case nameof(Q3_Completed): Q3_Completed = value; break;
            case nameof(Gate_LongBien_Unlocked): Gate_LongBien_Unlocked = value; break;
            case nameof(GrayShadow_LetterRead): GrayShadow_LetterRead = value; break;
            case nameof(Level1_Finished): Level1_Finished = value; break;

            case nameof(Hue_Q1_Started): Hue_Q1_Started = value; break;
            case nameof(Hue_Q1_BunSoiTo_Collected): Hue_Q1_BunSoiTo_Collected = value; break;
            case nameof(Hue_Q1_BapBo_Collected): Hue_Q1_BapBo_Collected = value; break;
            case nameof(Hue_Q1_ChaCua_Collected): Hue_Q1_ChaCua_Collected = value; break;
            case nameof(Hue_Q1_NuocDungHue_Collected): Hue_Q1_NuocDungHue_Collected = value; break;
            case nameof(Hue_Q1_RauSongHue_Collected): Hue_Q1_RauSongHue_Collected = value; break;
            case nameof(Hue_Q1_Completed): Hue_Q1_Completed = value; break;
            case nameof(Hue_Q2_Started): Hue_Q2_Started = value; break;
            case nameof(Hue_Q2_BunBoCooked): Hue_Q2_BunBoCooked = value; break;
            case nameof(Hue_Q2_Completed): Hue_Q2_Completed = value; break;
            case nameof(Hue_Q3_Started): Hue_Q3_Started = value; break;
            case nameof(Hue_Q3_PuzzleSolved): Hue_Q3_PuzzleSolved = value; break;
            case nameof(Hue_Q3_Completed): Hue_Q3_Completed = value; break;
            case nameof(Gate_HoiAn_Unlocked): Gate_HoiAn_Unlocked = value; break;
            case nameof(Hue_Finished): Hue_Finished = value; break;
        }
    }
}
