using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class MauSceneRoundDebugUI : MonoBehaviour
{
    private readonly List<BoxAssignment[]> _distributionPermutations = new();

    private GameRoundManager _roundManager;

    private void Awake()
    {
        BuildPermutations();
    }

    private void OnGUI()
    {
        ResolveManager();

        if (_roundManager == null || _roundManager.Runner == null || !_roundManager.Runner.IsRunning)
        {
            DrawMessage("Inicia una sesion desde Startup, elige MauScene y entra con hasta 3 jugadores para probar la ronda.");
            return;
        }

        PlayerRoundController localController = _roundManager.GetPlayerControllerByPlayerRef(_roundManager.Runner.LocalPlayer);

        GUILayout.BeginArea(new Rect(16f, 16f, 460f, 900f), GUI.skin.box);
        GUILayout.Label($"Local Player: {_roundManager.Runner.LocalPlayer}");
        GUILayout.Label($"Phase: {_roundManager.Phase}");
        GUILayout.Label($"Config: {_roundManager.ChosenConfig}");
        GUILayout.Label($"Round Sequence: {_roundManager.RoundSequence}");
        GUILayout.Label($"Players Assigned: {_roundManager.GetAssignedPlayerCount()} / {_roundManager.RequiredPlayerCount}");

        if (localController == null)
        {
            GUILayout.Label("Todavia no tienes un slot asignado. Espera a que el host te registre.");
            DrawScoreboard();
            GUILayout.EndArea();
            return;
        }

        GUILayout.Space(8f);
        GUILayout.Label($"Tu rol: {localController.Role}");
        GUILayout.Label($"Tu slot: {localController.SlotIndex}");
        GUILayout.Label($"Tu score: {localController.Score}");
        GUILayout.Label($"Final box index: {localController.FinalBoxIndex}");

        if (localController.HasPrivateInspectionResult)
        {
            GUILayout.Label($"Inspeccion privada: Caja {localController.PrivateInspectedBoxIndex} = {localController.PrivateInspectedContent}");
        }

        GUILayout.Space(8f);
        DrawRoleActions(localController);

        GUILayout.Space(12f);
        DrawScoreboard();
        DrawBoxes();
        GUILayout.EndArea();
    }

    private void DrawRoleActions(PlayerRoundController localController)
    {
        if (localController.Role == PlayerRole.Configurator && _roundManager.Phase == RoundPhase.WaitingForConfig)
        {
            GUILayout.Label("Acciones del configurador");

            if (GUILayout.Button("Elegir 2 Money / 1 Bomb"))
                localController.ChooseRoundConfiguration(RoundConfigType.TwoMoneyOneBomb);

            if (GUILayout.Button("Elegir 1 Money / 2 Bomb"))
                localController.ChooseRoundConfiguration(RoundConfigType.OneMoneyTwoBomb);
        }

        if (localController.Role == PlayerRole.Inspector && _roundManager.Phase == RoundPhase.Inspecting)
        {
            GUILayout.Label("Acciones del inspector");

            for (int boxIndex = 0; boxIndex < 3; boxIndex++)
            {
                if (GUILayout.Button($"Inspeccionar caja {boxIndex}"))
                    localController.InspectBox(boxIndex);
            }
        }

        if (localController.Role == PlayerRole.Distributor && _roundManager.Phase == RoundPhase.Distributing)
        {
            GUILayout.Label("Acciones del distribuidor");
            GUILayout.Label("Cada boton representa una permutacion completa de reparto.");

            for (int i = 0; i < _distributionPermutations.Count; i++)
            {
                BoxAssignment[] permutation = _distributionPermutations[i];
                string label = $"P0<-{permutation[0].BoxIndex} | P1<-{permutation[1].BoxIndex} | P2<-{permutation[2].BoxIndex}";

                if (GUILayout.Button(label))
                    localController.SubmitDistribution(permutation[0], permutation[1], permutation[2]);
            }
        }
    }

    private void DrawScoreboard()
    {
        GUILayout.Label("Slots");

        for (int slotIndex = 0; slotIndex < 3; slotIndex++)
        {
            PlayerRoundController controller = _roundManager.GetPlayerControllerBySlot(slotIndex);

            if (controller == null)
                continue;

            GUILayout.Label(
                $"Slot {slotIndex} | Role {controller.Role} | Player {controller.AssignedPlayer} | Score {controller.Score} | Box {controller.FinalBoxIndex}");
        }
    }

    private void DrawBoxes()
    {
        GUILayout.Space(8f);
        GUILayout.Label("Cajas");

        for (int boxIndex = 0; boxIndex < 3; boxIndex++)
        {
            NetworkBoxState box = _roundManager.GetBoxState(boxIndex);
            string visibleContent = box.IsRevealed ? box.Content.ToString() : "Hidden";

            GUILayout.Label(
                $"Caja {boxIndex} | OwnerSlot {box.CurrentOwnerSlot} | Inspected {box.WasInspected} | Final {box.IsFinallyAssigned} | Reveal {box.IsRevealed} | Content {visibleContent}");
        }
    }

    private void DrawMessage(string message)
    {
        GUILayout.BeginArea(new Rect(16f, 16f, 460f, 80f), GUI.skin.box);
        GUILayout.Label(message);
        GUILayout.EndArea();
    }

    private void ResolveManager()
    {
        if (_roundManager != null)
            return;

        _roundManager = FindFirstObjectByType<GameRoundManager>();
    }

    private void BuildPermutations()
    {
        int[] boxes = { 0, 1, 2 };
        int[][] permutations =
        {
            new[] { boxes[0], boxes[1], boxes[2] },
            new[] { boxes[0], boxes[2], boxes[1] },
            new[] { boxes[1], boxes[0], boxes[2] },
            new[] { boxes[1], boxes[2], boxes[0] },
            new[] { boxes[2], boxes[0], boxes[1] },
            new[] { boxes[2], boxes[1], boxes[0] }
        };

        for (int i = 0; i < permutations.Length; i++)
        {
            int[] permutation = permutations[i];
            _distributionPermutations.Add(new[]
            {
                new BoxAssignment(permutation[0], 0),
                new BoxAssignment(permutation[1], 1),
                new BoxAssignment(permutation[2], 2)
            });
        }
    }
}
