using System.Collections.Generic;
using HackathonJuego;
using UnityEngine;

[DisallowMultipleComponent]
public class MauSceneRoundDebugUI : MonoBehaviour
{
    private readonly List<int[]> _distributionPermutations = new();

    private Gameplay _gameplay;

    private void Awake()
    {
        BuildPermutations();
    }

    private void OnGUI()
    {
        ResolveGameplay();

        if (_gameplay == null || _gameplay.Runner == null || !_gameplay.Runner.IsRunning)
        {
            DrawMessage("Inicia una sesion Fusion para probar el flujo de Gameplay.");
            return;
        }

        int localStation = _gameplay.GetLocalStationIndex();

        GUILayout.BeginArea(new Rect(16f, 16f, 460f, 900f), GUI.skin.box);
        GUILayout.Label($"Local Player: {_gameplay.Runner.LocalPlayer}");
        GUILayout.Label($"State: {_gameplay.State}");
        GUILayout.Label($"Round: {_gameplay.CurrentRound}");
        GUILayout.Label($"Turn Index: {_gameplay.PlayerTurnIndex}");
        GUILayout.Label($"Dinero / Bombas: {_gameplay.DineroEnJuego} / {_gameplay.BombasEnJuego}");
        GUILayout.Label($"Caja abierta: {_gameplay.OpenedBoxIndex}");
        GUILayout.Label($"Timer juez: {_gameplay.JudgeTimer:0.0}");

        if (localStation < 0)
        {
            GUILayout.Label("Todavia no tienes una estacion asignada.");
        }
        else
        {
            GUILayout.Space(8f);
            GUILayout.Label($"Tu estacion: {GetStationLabel(localStation)} ({localStation})");
            DrawRoleActions(localStation);
        }

        GUILayout.Space(12f);
        DrawScoreboard();
        DrawBoxes();
        GUILayout.EndArea();
    }

    private void DrawRoleActions(int localStation)
    {
        if (localStation == 0 && _gameplay.State == EGameplayState.P0_Config)
        {
            GUILayout.Label("Acciones del configurador");

            if (GUILayout.Button("Elegir 2 Money / 1 Bomb"))
                _gameplay.RPC_SeleccionarPaquete(1, _gameplay.Runner.LocalPlayer);

            if (GUILayout.Button("Elegir 1 Money / 2 Bomb"))
                _gameplay.RPC_SeleccionarPaquete(2, _gameplay.Runner.LocalPlayer);
        }

        if (localStation == 1 && _gameplay.State == EGameplayState.P1_Inspect)
        {
            GUILayout.Label("Acciones del inspector");

            for (int boxIndex = 0; boxIndex < 3; boxIndex++)
            {
                if (GUILayout.Button($"Inspeccionar caja {boxIndex}"))
                    _gameplay.RPC_InspeccionarCaja(boxIndex, _gameplay.Runner.LocalPlayer);
            }
        }

        if (localStation == 1 && _gameplay.State == EGameplayState.P1_Pass)
        {
            GUILayout.Label("Acciones del inspector");

            if (GUILayout.Button("Pasar cajas al distribuidor"))
                _gameplay.RPC_PasarCajas(_gameplay.Runner.LocalPlayer);
        }

        if (localStation == 2 && _gameplay.State == EGameplayState.P2_Distribute)
        {
            GUILayout.Label("Acciones del distribuidor");
            GUILayout.Label("Cada boton representa una permutacion completa de reparto.");

            for (int i = 0; i < _distributionPermutations.Count; i++)
            {
                int[] permutation = _distributionPermutations[i];
                string label = $"P0<-{permutation[0]} | P1<-{permutation[1]} | P2<-{permutation[2]}";

                if (GUILayout.Button(label))
                {
                    for (int stationIndex = 0; stationIndex < permutation.Length; stationIndex++)
                        _gameplay.RPC_AsignarCaja(permutation[stationIndex], stationIndex, _gameplay.Runner.LocalPlayer);
                }
            }
        }
    }

    private void DrawScoreboard()
    {
        GUILayout.Label("Jugadores / estaciones");

        foreach (var pair in _gameplay.PlayerData)
        {
            PlayerData data = pair.Value;
            GUILayout.Label(
                $"Player {pair.Key} | Connected {data.IsConnected} | Ready {data.IsReady} | Station {data.StationIndex} | Score {data.Score}");
        }
    }

    private void DrawBoxes()
    {
        GUILayout.Space(8f);
        GUILayout.Label("Cajas");

        for (int boxIndex = 0; boxIndex < 3; boxIndex++)
        {
            bool revealActive = _gameplay.State == EGameplayState.Reveal || _gameplay.State == EGameplayState.Finished;
            string visibleContent = revealActive ? _gameplay.BoxContents[boxIndex].ToString() : "Hidden";

            GUILayout.Label(
                $"Caja {boxIndex} | AssignedStation {_gameplay.BoxAssignments[boxIndex]} | Opened {_gameplay.OpenedBoxIndex == boxIndex} | Content {visibleContent}");
        }
    }

    private void DrawMessage(string message)
    {
        GUILayout.BeginArea(new Rect(16f, 16f, 460f, 80f), GUI.skin.box);
        GUILayout.Label(message);
        GUILayout.EndArea();
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
            _distributionPermutations.Add(new[] { permutation[0], permutation[1], permutation[2] });
        }
    }

    private void ResolveGameplay()
    {
        if (_gameplay != null)
            return;

        _gameplay = FindFirstObjectByType<Gameplay>();
    }

    private static string GetStationLabel(int stationIndex)
    {
        switch (stationIndex)
        {
            case 0: return "Configurador";
            case 1: return "Inspector";
            case 2: return "Distribuidor";
            default: return "Sin estacion";
        }
    }
}
