using System.Collections.Generic;
using UnityEngine;
using Fusion;
using DG.Tweening;

namespace HackathonJuego
{
    public struct PlayerData : INetworkStruct
    {
        [Networked, Capacity(24)] public string Nickname { get => default; set { } }
        public PlayerRef PlayerRef;
        public bool IsConnected;
        public bool IsReady;
        public int StationIndex;
        public int Score;
    }

    public enum EGameplayState
    {
        Lobby = 0,
        P0_Config = 1,
        P1_Inspect = 2,
        P1_Pass = 3,
        P2_Distribute = 4,
        Reveal = 5,
        Finished = 6
    }

    public class Gameplay : NetworkBehaviour, IPlayerJoined, IPlayerLeft
    {
        [Header("Cámaras de las Estaciones")]
        public Camera[] StationCameras;

        [Header("Movimiento de Cajas")]
        [Tooltip("El parent de las 3 cajas (Caja_0, Caja_1, Caja_2)")]
        public Transform boxParent;
        [Tooltip("Empty en la posición del belt del Observer")]
        public Transform observerBeltTarget;
        [Tooltip("Empty en la posición del belt del Juez")]
        public Transform judgeBeltTarget;
        public float beltMoveDuration = 1.5f;
        public Ease beltMoveEase = Ease.InOutQuad;

        [Networked] public int CurrentRound { get; set; } = 1;
        [Networked] public int PlayerTurnIndex { get; set; } = 0;
        [Networked] public int DineroEnJuego { get; set; }
        [Networked] public int BombasEnJuego { get; set; }

        [Networked, Capacity(3)] public NetworkArray<int> BoxContents { get; }
        [Networked] public int OpenedBoxIndex { get; set; } = -1;

        // Asignaciones del Juez: BoxAssignments[boxIndex] = stationIndex (-1 = sin asignar)
        [Networked, Capacity(3)] public NetworkArray<int> BoxAssignments { get; }
        // Puntuación de la ronda por estación
        [Networked, Capacity(3)] public NetworkArray<int> RoundScores { get; }
        // Timer del Juez
        [Networked] public float JudgeTimer { get; set; }
        public float judgeTimerDuration = 30f;

        [Networked, Capacity(3)] public NetworkDictionary<PlayerRef, PlayerData> PlayerData { get; }
        [Networked] public EGameplayState State { get; set; }

        private bool _camerasAssigned = false;

        // --- RPC: SELECCIONAR PAQUETE (versión vieja con opción) ---
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_SeleccionarPaquete(int opcionSeleccionada, RpcInfo info = default)
        {
            if (State != EGameplayState.P0_Config) return;

            if (PlayerData.TryGet(info.Source, out var data) && data.StationIndex == 0)
            {
                if (opcionSeleccionada == 1) { DineroEnJuego = 2; BombasEnJuego = 1; }
                else if (opcionSeleccionada == 2) { DineroEnJuego = 1; BombasEnJuego = 2; }

                MezclarCajas();
                RPC_MoverCajasAObserver();
            }
        }

        // --- RPC: CONFIGURAR CAJAS (usado por ArchitectPanel y PlayerClicker) ---
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_ConfigurarCajas(int box0, int box1, int box2, RpcInfo info = default)
        {
            if (State != EGameplayState.P0_Config) return;
            if (!PlayerData.TryGet(info.Source, out var data) || data.StationIndex != 0) return;

            BoxContents.Set(0, box0);
            BoxContents.Set(1, box1);
            BoxContents.Set(2, box2);

            DineroEnJuego = 0; BombasEnJuego = 0;
            for (int i = 0; i < 3; i++)
            {
                if (BoxContents[i] == 1) DineroEnJuego++;
                else BombasEnJuego++;
            }

            Debug.Log($"<color=green>Cajas configuradas: [{box0},{box1},{box2}]</color>");

            // Mover cajas al belt del Observer en todos los clientes
            RPC_MoverCajasAObserver();
        }

        /// <summary>
        /// Llamado por el servidor: mueve las 3 cajas al belt del Observer con DOTween.
        /// Cuando terminan, el servidor avanza el estado.
        /// </summary>
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        public void RPC_MoverCajasAObserver()
        {
            if (boxParent == null || observerBeltTarget == null)
            {
                Debug.LogWarning("boxParent u observerBeltTarget no asignados.");
                // Avanzar estado de todas formas si falta config
                if (HasStateAuthority)
                {
                    State = EGameplayState.P1_Inspect;
                    PlayerTurnIndex = 1;
                }
                return;
            }

            boxParent.DOMove(observerBeltTarget.position, beltMoveDuration)
                .SetEase(beltMoveEase)
                .OnComplete(() =>
                {
                    if (HasStateAuthority)
                    {
                        State = EGameplayState.P1_Inspect;
                        PlayerTurnIndex = 1;
                    }
                });
        }

        // --- RPC: OBSERVADOR PASA LAS CAJAS ---
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_PasarCajas(RpcInfo info = default)
        {
            if (State != EGameplayState.P1_Pass) return;
            if (!PlayerData.TryGet(info.Source, out var data) || data.StationIndex != 1) return;

            for (int i = 0; i < 3; i++)
                BoxAssignments.Set(i, -1);

            JudgeTimer = judgeTimerDuration;
            State = EGameplayState.P2_Distribute;
            PlayerTurnIndex = 2;
        }

        // --- RPC: JUEZ ASIGNA UNA CAJA ---
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_AsignarCaja(int boxIndex, int stationIndex, RpcInfo info = default)
        {
            if (State != EGameplayState.P2_Distribute) return;
            if (!PlayerData.TryGet(info.Source, out var data) || data.StationIndex != 2) return;
            if (boxIndex < 0 || boxIndex >= 3 || stationIndex < 0 || stationIndex >= 3) return;

            BoxAssignments.Set(boxIndex, stationIndex);

            bool allAssigned = true;
            for (int i = 0; i < 3; i++)
            {
                if (BoxAssignments[i] == -1) { allAssigned = false; break; }
            }

            if (allAssigned)
                DoReveal();
        }

        private void MezclarCajas()
        {
            int[] temp = new int[3];
            int index = 0;
            for (int i = 0; i < DineroEnJuego; i++) { temp[index] = 1; index++; }
            for (int i = 0; i < BombasEnJuego; i++) { temp[index] = 2; index++; }

            for (int i = 0; i < temp.Length; i++)
            {
                int randomIdx = UnityEngine.Random.Range(0, temp.Length);
                int backup = temp[i];
                temp[i] = temp[randomIdx];
                temp[randomIdx] = backup;
            }

            for (int i = 0; i < 3; i++)
                BoxContents.Set(i, temp[i]);

            Debug.Log($"<color=green>Cajas mezcladas: [{BoxContents[0]},{BoxContents[1]},{BoxContents[2]}]</color>");
        }

        public override void Spawned()
        {
            if (HasStateAuthority)
            {
                State = EGameplayState.Lobby;
                for (int i = 0; i < 3; i++)
                {
                    BoxAssignments.Set(i, -1);
                    RoundScores.Set(i, 0);
                }
            }
        }

        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority) return;

            if (State == EGameplayState.P2_Distribute)
            {
                JudgeTimer -= Runner.DeltaTime;
                if (JudgeTimer <= 0f)
                {
                    AutoDistribute();
                    DoReveal();
                }
            }
        }

        public override void Render()
        {
            if (State != EGameplayState.Lobby && !_camerasAssigned)
                AssignLocalCamera();
        }

        private void AssignLocalCamera()
        {
            if (PlayerData.TryGet(Runner.LocalPlayer, out var myData))
            {
                for (int i = 0; i < StationCameras.Length; i++)
                {
                    if (StationCameras[i] != null)
                        StationCameras[i].gameObject.SetActive(i == myData.StationIndex);
                }
                _camerasAssigned = true;
            }
        }

        public void PlayerJoined(PlayerRef player)
        {
            if (!HasStateAuthority) return;

            if (!PlayerData.ContainsKey(player))
            {
                var data = new PlayerData
                {
                    PlayerRef = player,
                    IsConnected = true,
                    IsReady = false,
                    StationIndex = -1
                };
                PlayerData.Set(player, data);
            }
            else
            {
                var data = PlayerData.Get(player);
                data.IsConnected = true;
                PlayerData.Set(player, data);
            }
        }

        public void PlayerLeft(PlayerRef player)
        {
            if (!HasStateAuthority) return;
            if (PlayerData.TryGet(player, out var data))
            {
                data.IsConnected = false;
                data.IsReady = false;
                PlayerData.Set(player, data);
            }
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_ToggleReady(PlayerRef playerRef)
        {
            if (HasStateAuthority && PlayerData.TryGet(playerRef, out var data))
            {
                data.IsReady = !data.IsReady;
                PlayerData.Set(playerRef, data);
            }
        }

        public void StartMatchFromUI()
        {
            if (HasStateAuthority && State == EGameplayState.Lobby)
            {
                int currentIndex = 0;
                foreach (var pair in PlayerData)
                {
                    if (pair.Value.IsConnected)
                    {
                        var data = pair.Value;
                        data.StationIndex = currentIndex;
                        PlayerData.Set(pair.Key, data);
                        currentIndex++;
                        if (currentIndex >= 3) break;
                    }
                }

                State = EGameplayState.P0_Config;
                PlayerTurnIndex = 0;
            }
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_InspeccionarCaja(int cajaIndex, RpcInfo info = default)
        {
            if (State != EGameplayState.P1_Inspect) return;

            if (PlayerData.TryGet(info.Source, out var data) && data.StationIndex == 1)
            {
                OpenedBoxIndex = cajaIndex;
                int premioDentro = BoxContents[cajaIndex];

                Debug.Log($"El Jugador 1 inspeccionó la caja {cajaIndex}. Contenía: {premioDentro}");

                // Abrir la caja en la compu del Observer solamente
                RPC_MostrarAnimacionExclusiva(info.Source, cajaIndex, premioDentro);

                // Cambiamos a P1_Pass — el Observer debe clickear de nuevo para cerrar y pasar
                State = EGameplayState.P1_Pass;
                PlayerTurnIndex = 1;
            }
        }

        /// <summary>
        /// El Observer clickea la caja abierta para cerrarla y enviarla al Juez.
        /// </summary>
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_CerrarYPasarCajas(RpcInfo info = default)
        {
            if (State != EGameplayState.P1_Pass) return;
            if (!PlayerData.TryGet(info.Source, out var data) || data.StationIndex != 1) return;

            // Cerrar la caja y mover todas al belt del Juez
            RPC_AnimarCierreYSlide(OpenedBoxIndex);
        }

        /// <summary>
        /// Ejecuta en todos los clientes: cierra la caja abierta y mueve todas al belt del Juez.
        /// </summary>
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        public void RPC_AnimarCierreYSlide(int cajaAbierta)
        {
            // Cerrar la caja que estaba abierta
            GameObject cajaObj = GameObject.Find("Caja_" + cajaAbierta);
            if (cajaObj != null)
            {
                CajaVisual cv = cajaObj.GetComponent<CajaVisual>();
                if (cv != null) cv.CerrarCaja();
            }

            if (boxParent == null || judgeBeltTarget == null)
            {
                Debug.LogWarning("boxParent o judgeBeltTarget no asignados.");
                if (HasStateAuthority)
                {
                    for (int i = 0; i < 3; i++) BoxAssignments.Set(i, -1);
                    JudgeTimer = judgeTimerDuration;
                    State = EGameplayState.P2_Distribute;
                    PlayerTurnIndex = 2;
                }
                return;
            }

            // Esperar a que cierre la animación, luego mover parent al belt del Juez
            Sequence seq = DOTween.Sequence();
            seq.AppendInterval(0.8f);
            seq.Append(boxParent.DOMove(judgeBeltTarget.position, beltMoveDuration).SetEase(beltMoveEase));

            if (HasStateAuthority)
            {
                seq.OnComplete(() =>
                {
                    for (int i = 0; i < 3; i++)
                        BoxAssignments.Set(i, -1);

                    JudgeTimer = judgeTimerDuration;
                    State = EGameplayState.P2_Distribute;
                    PlayerTurnIndex = 2;
                });
            }
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        public void RPC_MostrarAnimacionExclusiva([RpcTarget] PlayerRef player, int cajaIndex, int tipoPremio)
        {
            Debug.Log($"<color=yellow>EJECUTANDO RPC EN EL CLIENTE PARA LA CAJA {cajaIndex}</color>");

            GameObject cajaObj = GameObject.Find("Caja_" + cajaIndex);
            if (cajaObj != null)
            {
                CajaVisual scriptCaja = cajaObj.GetComponent<CajaVisual>();
                if (scriptCaja != null)
                    scriptCaja.RevelarPremioExclusivo(tipoPremio);
                else
                    Debug.LogError($"Caja_{cajaIndex} no tiene CajaVisual.");
            }
            else
                Debug.LogError($"No se encontró 'Caja_{cajaIndex}' en la escena.");
        }

        private void AutoDistribute()
        {
            var unassignedBoxes = new List<int>();
            var unassignedStations = new List<int> { 0, 1, 2 };

            for (int i = 0; i < 3; i++)
            {
                int station = BoxAssignments[i];
                if (station == -1)
                    unassignedBoxes.Add(i);
                else
                    unassignedStations.Remove(station);
            }

            foreach (int box in unassignedBoxes)
            {
                if (unassignedStations.Count == 0) break;
                int rndIdx = UnityEngine.Random.Range(0, unassignedStations.Count);
                BoxAssignments.Set(box, unassignedStations[rndIdx]);
                unassignedStations.RemoveAt(rndIdx);
            }
        }

        private void DoReveal()
        {
            for (int i = 0; i < 3; i++)
            {
                int station = BoxAssignments[i];
                if (station < 0 || station >= 3) continue;
                int content = BoxContents[i];
                int points = (content == 1) ? 1 : -1;
                RoundScores.Set(station, RoundScores[station] + points);
            }

            foreach (var kvp in PlayerData)
            {
                if (kvp.Value.IsConnected && kvp.Value.StationIndex >= 0 && kvp.Value.StationIndex < 3)
                {
                    var d = kvp.Value;
                    d.Score += RoundScores[d.StationIndex];
                    PlayerData.Set(kvp.Key, d);
                }
            }

            State = EGameplayState.Reveal;
        }

        public int GetLocalStationIndex()
        {
            if (Runner == null) return -1;
            if (PlayerData.TryGet(Runner.LocalPlayer, out var data))
                return data.StationIndex;
            return -1;
        }
    }
}