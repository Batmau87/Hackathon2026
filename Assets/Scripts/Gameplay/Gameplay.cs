using System.Collections.Generic;
using UnityEngine;
using Fusion;

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
        P0_Config = 1,      // Arquitecto elige el contenido de las cajas
        P1_Inspect = 2,     // Observador abre una caja
        P1_Pass = 3,        // Observador pasa las cajas (swipe)
        P2_Distribute = 4,  // Juez reparte las cajas
        Reveal = 5,         // Se abren todas y se dan puntos
        Finished = 6 
    }

    public class Gameplay : NetworkBehaviour, IPlayerJoined, IPlayerLeft
    {
        [Header("Cámaras de las Estaciones")]
        [Tooltip("Arrastra aquí las 3 cámaras de la escena: [0]=Arquitecto, [1]=Observador, [2]=Juez")]
        public Camera[] StationCameras; 

        [Header("Timer del Juez")]
        [Tooltip("Segundos que tiene el Juez para repartir")]
        public float judgeTimerDuration = 30f;

        // --- Variables de ronda ---
        [Networked] public int CurrentRound { get; set; } = 1;
        [Networked] public int PlayerTurnIndex { get; set; } = 0;
        [Networked] public int DineroEnJuego { get; set; }
        [Networked] public int BombasEnJuego { get; set; }

        // --- Cajas: 0=vacío, 1=Dinero, 2=Bomba ---
        [Networked, Capacity(3)] public NetworkArray<int> BoxContents { get; } 
        [Networked] public int OpenedBoxIndex { get; set; } = -1;

        // --- Distribución del Juez: a quién va cada caja (0=Arquitecto, 1=Observador, 2=Juez, -1=sin asignar) ---
        [Networked, Capacity(3)] public NetworkArray<int> BoxAssignments { get; }

        // --- Timer del Juez ---
        [Networked] public float JudgeTimer { get; set; }
        [Networked] public NetworkBool JudgeTimerActive { get; set; }

        // --- Puntuación de la ronda ---
        [Networked, Capacity(3)] public NetworkArray<int> RoundScores { get; }

        [Networked, Capacity(3)] public NetworkDictionary<PlayerRef, PlayerData> PlayerData { get; }
        [Networked] public EGameplayState State { get; set; }

        private bool _camerasAssigned = false;

        public override void Spawned()
        {
            if (HasStateAuthority)
            {
                State = EGameplayState.Lobby;
                ResetRound();
            }
        }

        private void ResetRound()
        {
            DineroEnJuego = 0;
            BombasEnJuego = 0;
            OpenedBoxIndex = -1;
            JudgeTimer = 0;
            JudgeTimerActive = false;
            for (int i = 0; i < 3; i++)
            {
                BoxContents.Set(i, 0);
                BoxAssignments.Set(i, -1);
                RoundScores.Set(i, 0);
            }
        }

        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority) return;

            // Timer del Juez
            if (JudgeTimerActive && State == EGameplayState.P2_Distribute)
            {
                JudgeTimer -= Runner.DeltaTime;
                if (JudgeTimer <= 0f)
                {
                    JudgeTimerActive = false;
                    AutoDistribute();
                }
            }
        }

        public override void Render()
        {
            if (State != EGameplayState.Lobby && !_camerasAssigned)
            {
                AssignLocalCamera();
            }
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

        // ===== HELPERS =====

        public int GetLocalStationIndex()
        {
            if (Runner == null) return -1;
            if (PlayerData.TryGet(Runner.LocalPlayer, out var data))
                return data.StationIndex;
            return -1;
        }

        // ===== ARQUITECTO (Station 0) =====

        /// <summary>
        /// El Arquitecto elige el contenido de cada caja individualmente.
        /// boxConfig es un array de 3 ints: 1=Dinero, 2=Bomba para cada caja.
        /// </summary>
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_ConfigurarCajas(int box0, int box1, int box2, RpcInfo info = default)
        {
            if (State != EGameplayState.P0_Config) return;
            if (!PlayerData.TryGet(info.Source, out var data) || data.StationIndex != 0) return;

            BoxContents.Set(0, box0);
            BoxContents.Set(1, box1);
            BoxContents.Set(2, box2);

            int dinero = 0, bombas = 0;
            for (int i = 0; i < 3; i++)
            {
                if (BoxContents[i] == 1) dinero++;
                else if (BoxContents[i] == 2) bombas++;
            }
            DineroEnJuego = dinero;
            BombasEnJuego = bombas;

            // Mezclar el orden
            ShuffleBoxes();

            Debug.Log($"<color=green>Arquitecto configuró cajas: [{BoxContents[0]}, {BoxContents[1]}, {BoxContents[2]}]</color>");

            State = EGameplayState.P1_Inspect;
            PlayerTurnIndex = 1;

            // Notificar a todos que las cajas pasaron
            RPC_OnBoxesSent();
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        public void RPC_OnBoxesSent()
        {
            // Los clientes pueden disparar animaciones de cinta transportadora aquí
            Debug.Log("Las cajas han sido enviadas por la cinta.");
        }

        private void ShuffleBoxes()
        {
            for (int i = 2; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                int temp = BoxContents[i];
                BoxContents.Set(i, BoxContents[j]);
                BoxContents.Set(j, temp);
            }
        }

        // ===== OBSERVADOR (Station 1) =====

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_InspeccionarCaja(int cajaIndex, RpcInfo info = default)
        {
            if (State != EGameplayState.P1_Inspect) return;
            if (!PlayerData.TryGet(info.Source, out var data) || data.StationIndex != 1) return;
            if (cajaIndex < 0 || cajaIndex >= 3) return;

            OpenedBoxIndex = cajaIndex;
            int premioDentro = BoxContents[cajaIndex];
            
            Debug.Log($"Observador inspeccionó caja {cajaIndex}. Contenido: {(premioDentro == 1 ? "Dinero" : "Bomba")}");

            // Mostrar solo al Observador
            RPC_MostrarAnimacionExclusiva(info.Source, cajaIndex, premioDentro);

            // Cambiar a fase de pasar
            State = EGameplayState.P1_Pass;
        }

        /// <summary>
        /// El Observador confirma que pasó las cajas al Juez
        /// </summary>
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_PasarCajas(RpcInfo info = default)
        {
            if (State != EGameplayState.P1_Pass) return;
            if (!PlayerData.TryGet(info.Source, out var data) || data.StationIndex != 1) return;

            State = EGameplayState.P2_Distribute;
            PlayerTurnIndex = 2;
            JudgeTimer = judgeTimerDuration;
            JudgeTimerActive = true;

            RPC_OnBoxesPassedToJudge();
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        public void RPC_OnBoxesPassedToJudge()
        {
            Debug.Log("Las cajas llegaron al Juez.");
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        public void RPC_MostrarAnimacionExclusiva([RpcTarget] PlayerRef player, int cajaIndex, int tipoPremio)
        {
            GameObject cajaObj = GameObject.Find("Caja_" + cajaIndex);
            if (cajaObj != null)
            {
                CajaVisual scriptCaja = cajaObj.GetComponent<CajaVisual>();
                if (scriptCaja != null)
                    scriptCaja.RevelarPremioExclusivo(tipoPremio);
            }
        }

        // ===== JUEZ (Station 2) =====

        /// <summary>
        /// El Juez asigna una caja a un jugador. 
        /// targetStation: 0=Arquitecto, 1=Observador, 2=Juez
        /// </summary>
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_AsignarCaja(int cajaIndex, int targetStation, RpcInfo info = default)
        {
            if (State != EGameplayState.P2_Distribute) return;
            if (!PlayerData.TryGet(info.Source, out var data) || data.StationIndex != 2) return;
            if (cajaIndex < 0 || cajaIndex >= 3) return;
            if (targetStation < 0 || targetStation > 2) return;

            BoxAssignments.Set(cajaIndex, targetStation);

            Debug.Log($"Juez asignó caja {cajaIndex} a estación {targetStation}");

            // Verificar si todas las cajas están asignadas
            bool todasAsignadas = true;
            for (int i = 0; i < 3; i++)
            {
                if (BoxAssignments[i] == -1) { todasAsignadas = false; break; }
            }

            if (todasAsignadas)
            {
                JudgeTimerActive = false;
                FinalizarDistribucion();
            }
        }

        /// <summary>
        /// Si el timer se acaba, asignación aleatoria de las cajas no asignadas
        /// </summary>
        private void AutoDistribute()
        {
            List<int> unassigned = new List<int>();
            List<int> availableStations = new List<int> { 0, 1, 2 };

            // Quitar estaciones ya asignadas
            for (int i = 0; i < 3; i++)
            {
                if (BoxAssignments[i] != -1)
                    availableStations.Remove(BoxAssignments[i]);
                else
                    unassigned.Add(i);
            }

            // Asignar aleatoriamente
            foreach (int boxIdx in unassigned)
            {
                if (availableStations.Count == 0) break;
                int randIdx = UnityEngine.Random.Range(0, availableStations.Count);
                BoxAssignments.Set(boxIdx, availableStations[randIdx]);
                availableStations.RemoveAt(randIdx);
            }

            FinalizarDistribucion();
        }

        private void FinalizarDistribucion()
        {
            State = EGameplayState.Reveal;

            // Calcular puntos: Dinero = +1, Bomba = -1
            for (int i = 0; i < 3; i++)
            {
                int station = BoxAssignments[i];
                int content = BoxContents[i];
                int points = (content == 1) ? 1 : -1;
                RoundScores.Set(station, RoundScores[station] + points);
            }

            // Aplicar puntos a PlayerData
            foreach (var kvp in PlayerData)
            {
                var pd = kvp.Value;
                if (pd.StationIndex >= 0 && pd.StationIndex < 3)
                {
                    pd.Score += RoundScores[pd.StationIndex];
                    PlayerData.Set(kvp.Key, pd);
                }
            }

            RPC_OnReveal();
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        public void RPC_OnReveal()
        {
            Debug.Log($"¡REVELACIÓN! Caja0→Estación{BoxAssignments[0]}, Caja1→Estación{BoxAssignments[1]}, Caja2→Estación{BoxAssignments[2]}");
            // Aquí cada cliente puede mostrar animaciones de revelación
        }

        // ===== LOBBY =====

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
                var d = PlayerData.Get(player);
                d.IsConnected = true;
                PlayerData.Set(player, d);
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

                ResetRound();
                State = EGameplayState.P0_Config;
                PlayerTurnIndex = 0;
            }
        }
    }
}