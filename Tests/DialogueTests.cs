using System.Collections.Generic;
using GameProject.Core.Dialogue;
using Xunit;

namespace GameProject.Tests;

public class DialogueTests
{
    private const string SampleDialogueJson = """
    {
      "dialogueId": "Dialogue_BaAn_Intro",
      "startNodeId": "Node_01",
      "nodes": [
        {
          "id": "Node_01",
          "speaker": "Bà Cụ Bán Phở",
          "speakerAvatar": "Avatar_BaAn_Sad",
          "avatarPosition": "Right",
          "text": "Chào cháu, Bé Mây... Phố cổ mình giờ ảm đạm quá, bà muốn nấu nồi phở truyền thống mà thiếu nguyên liệu.",
          "nextNodeId": "Node_02",
          "branches": []
        },
        {
          "id": "Node_02",
          "speaker": "Bé Mây",
          "speakerAvatar": "Avatar_May_Determined",
          "avatarPosition": "Left",
          "text": "Bà đừng lo ạ! Cháu và Bút Chì sẽ đi gom đủ nguyên liệu về cho bà ngay!",
          "branches": [
            {
              "choiceText": "Cháu sẵn sàng rồi, thưa bà!",
              "nextNodeId": "Node_Accept"
            },
            {
              "choiceText": "Bà cần những nguyên liệu gì ạ?",
              "nextNodeId": "Node_AskRecipe"
            }
          ]
        },
        {
          "id": "Node_Accept",
          "speaker": "Bà Cụ Bán Phở",
          "speakerAvatar": "Avatar_BaAn_Smile",
          "avatarPosition": "Right",
          "text": "Cảm ơn cháu nhiều lắm! Hãy ghé các quầy hàng quanh Hàng Buồm và Lãn Ông nhé!",
          "nextNodeId": null,
          "actionTrigger": "Start_Quest_1"
        },
        {
          "id": "Node_AskRecipe",
          "speaker": "Bà Cụ Bán Phở",
          "speakerAvatar": "Avatar_BaAn_Smile",
          "avatarPosition": "Right",
          "text": "Bà cần bánh phở tươi, thịt bò, nước dùng hầm xương, hành lá và hoa hồi quế chi.",
          "nextNodeId": null,
          "actionTrigger": "Start_Quest_1"
        }
      ]
    }
    """;

    [Fact]
    public void LoadDialogueJson_ParsesGraphAndStartNodeCorrectly()
    {
        var service = new DialogueService();
        service.LoadDialogueJson(SampleDialogueJson);

        var startNode = service.StartDialogue("Dialogue_BaAn_Intro");

        Assert.NotNull(startNode);
        Assert.Equal("Node_01", startNode.Id);
        Assert.Equal("Bà Cụ Bán Phở", startNode.Speaker);
        Assert.Contains("Phố cổ mình giờ ảm đạm quá", startNode.Text);
        Assert.True(service.IsDialogueActive);
    }

    [Fact]
    public void NodeTraversal_AdvancesLinearDialogueToNextNode()
    {
        var service = new DialogueService();
        service.LoadDialogueJson(SampleDialogueJson);

        service.StartDialogue("Dialogue_BaAn_Intro");
        var nextNode = service.AdvanceToNext();

        Assert.NotNull(nextNode);
        Assert.Equal("Node_02", nextNode.Id);
        Assert.Equal("Bé Mây", nextNode.Speaker);
        Assert.Equal(2, nextNode.Branches.Count);
    }

    [Fact]
    public void BranchChoices_SelectsBranchAndTransitionsCorrectly()
    {
        var service = new DialogueService();
        service.LoadDialogueJson(SampleDialogueJson);

        service.StartDialogue("Dialogue_BaAn_Intro");
        service.AdvanceToNext(); // at Node_02 with choices

        // Select Choice 0: "Cháu sẵn sàng rồi, thưa bà!" -> Node_Accept
        var chosenNode = service.SelectChoice(0);

        Assert.NotNull(chosenNode);
        Assert.Equal("Node_Accept", chosenNode.Id);
        Assert.Equal("Start_Quest_1", chosenNode.ActionTrigger);
    }

    [Fact]
    public void ActionTrigger_FiresEventWhenNodeIsReached()
    {
        var service = new DialogueService();
        service.LoadDialogueJson(SampleDialogueJson);

        string? triggeredAction = null;
        service.OnActionTriggered += action => triggeredAction = action;

        service.StartDialogue("Dialogue_BaAn_Intro");
        service.AdvanceToNext();
        service.SelectChoice(1); // Node_AskRecipe -> ActionTrigger "Start_Quest_1"

        Assert.Equal("Start_Quest_1", triggeredAction);
    }

    [Fact]
    public void BrokenLink_GracefullyEndsDialogueWithoutCrash()
    {
        const string brokenJson = """
        {
          "dialogueId": "Dialogue_Broken",
          "startNodeId": "N1",
          "nodes": [
            {
              "id": "N1",
              "speaker": "Bé Mây",
              "speakerAvatar": "Avatar_May",
              "text": "Hello",
              "nextNodeId": "NonExistentNode"
            }
          ]
        }
        """;

        var service = new DialogueService();
        service.LoadDialogueJson(brokenJson);

        service.StartDialogue("Dialogue_Broken");
        var next = service.AdvanceToNext();

        // Target does not exist, should return null and end dialogue gracefully
        Assert.Null(next);
        Assert.False(service.IsDialogueActive);
    }

    [Fact]
    public void MalformedJson_HandlesGracefullyWithoutThrowing()
    {
        var service = new DialogueService();
        service.LoadDialogueJson("{ INVALID JSON [[[");

        var node = service.StartDialogue("Dialogue_BaAn_Intro");
        Assert.Null(node);
        Assert.False(service.IsDialogueActive);
    }
}
