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
        P0_Config = 1,      // El jugador 0 elige las probabilidades
        P1_Inspect = 2,     // El jugador 1 abre una caja
        P1_Pass = 3,        // El jugador 1 pasa las cajas al juez
        P2_Distribute = 4,  // El jugador 2 reparte
        Reveal = 5,         // Se abren todas y se dan puntos
        Finished = 6 
    }

    public class Gameplay : NetworkBehaviour, IPlayerJoined, IPlayerLeft
    {
        [Header("Cámaras de las Estaciones")]
        [Tooltip("Arrastra aquí las 3 cámaras de la escena: [0]=Abre Caja, [1]=Saca Objetos, [2]=Envía")]
        public Camera[] StationCameras; 

        // AQUÍ VAN NUESTRAS VARIABLES DE RONDA
        [Networked] public int CurrentRound { get; set; } = 1;
        [Networked] public int PlayerTurnIndex { get; set; } = 0;
        // --- VARIABLES DE LA RONDA ACTUAL ---
        [Networked] public int DineroEnJuego { get; set; }
        [Networked] public int BombasEnJuego { get; set; }
        // --- VARIABLES DE LAS CAJAS ---
        // 1 = Dinero, 2 = Bomba
        [Networked, Capacity(3)] public NetworkArray<int> BoxContents { get; } 
        [Networked] public int OpenedBoxIndex { get; set; } = -1; // -1 significa que ninguna ha sido abierta

        // Asignaciones del Juez: BoxAssignments[boxIndex] = stationIndex del dueño (-1 = sin asignar)
        [Networked, Capacity(3)] public NetworkArray<int> BoxAssignments { get; }
        // Puntuación de la ronda por estación: RoundScores[stationIndex]
        [Networked, Capacity(3)] public NetworkArray<int> RoundScores { get; }
        // Timer del Juez (cuenta regresiva)
        [Networked] public float JudgeTimer { get; set; }
        public float judgeTimerDuration = 30f;

        // --- RPC: RECIBIR DECISIÓN DEL JUGADOR 0 (versión vieja con opción) ---
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_SeleccionarPaquete(int opcionSeleccionada, RpcInfo info = default)
        {
            if (State != EGameplayState.P0_Config) return;

            if (PlayerData.TryGet(info.Source, out var data) && data.StationIndex == 0)
            {
                if (opcionSeleccionada == 1) { DineroEnJuego = 2; BombasEnJuego = 1; }
                else if (opcionSeleccionada == 2) { DineroEnJuego = 1; BombasEnJuego = 2; }

                MezclarCajas();

                State = EGameplayState.P1_Inspect;
                PlayerTurnIndex = 1;
            }
        }

        // --- RPC: CONFIGURAR CAJAS DIRECTAMENTE (usado por ArchitectPanel y PlayerClicker) ---
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_ConfigurarCajas(int box0, int box1, int box2, RpcInfo info = default)
        {
            if (State != EGameplayState.P0_Config) return;
            if (!PlayerData.TryGet(info.Source, out var data) || data.StationIndex != 0) return;

            BoxContents.Set(0, box0);
            BoxContents.Set(1, box1);
            BoxContents.Set(2, box2);

            // Contar dinero/bombas
            DineroEnJuego = 0; BombasEnJuego = 0;
            for (int i = 0; i < 3; i++)
            {
                if (BoxContents[i] == 1) DineroEnJuego++;
                else BombasEnJuego++;
            }

            Debug.Log($"<color=green>Cajas configuradas: [{box0},{box1},{box2}]</color>");
            State = EGameplayState.P1_Inspect;
            PlayerTurnIndex = 1;
        }

        // --- RPC: OBSERVADOR PASA LAS CAJAS ---
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_PasarCajas(RpcInfo info = default)
        {
            if (State != EGameplayState.P1_Pass) return;
            if (!PlayerData.TryGet(info.Source, out var data) || data.StationIndex != 1) return;

            // Inicializar asignaciones del Juez
            for (int i = 0; i < 3; i++)
                BoxAssignments.Set(i, -1);

            JudgeTimer = judgeTimerDuration;
            State = EGameplayState.P2_Distribute;
            PlayerTurnIndex = 2;
        }

        // --- RPC: JUEZ ASIGNA UNA CAJA A UNA ESTACIÓN ---
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_AsignarCaja(int boxIndex, int stationIndex, RpcInfo info = default)
        {
            if (State != EGameplayState.P2_Distribute) return;
            if (!PlayerData.TryGet(info.Source, out var data) || data.StationIndex != 2) return;
            if (boxIndex < 0 || boxIndex >= 3 || stationIndex < 0 || stationIndex >= 3) return;

            BoxAssignments.Set(boxIndex, stationIndex);

            // Verificar si todas están asignadas
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
            // Llenamos un arreglo temporal con el contenido
            int[] temp = new int[3];
            int index = 0;
            for(int i=0; i<DineroEnJuego; i++) { temp[index] = 1; index++; } // 1 = Dinero
            for(int i=0; i<BombasEnJuego; i++) { temp[index] = 2; index++; } // 2 = Bomba

            // Mezclamos al azar (Shuffle)
            for (int i = 0; i < temp.Length; i++)
            {
                int randomIdx = UnityEngine.Random.Range(0, temp.Length);
                int backup = temp[i];
                temp[i] = temp[randomIdx];
                temp[randomIdx] = backup;
            }

            // Guardamos el resultado en la red
            for(int i=0; i<3; i++)
            {
                BoxContents.Set(i, temp[i]);
            }
            Debug.Log($"<color=green>Cajas mezcladas en el servidor. Caja 0: {BoxContents[0]}, Caja 1: {BoxContents[1]}, Caja 2: {BoxContents[2]}</color>");
        }

        [Networked, Capacity(3)] public NetworkDictionary<PlayerRef, PlayerData> PlayerData { get; }
        [Networked] public EGameplayState State { get; set; }

        private bool _camerasAssigned = false;

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

            // Timer del Juez
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

        /// <summary>
        /// Auto-asigna cajas que el Juez no asignó antes del timeout.
        /// </summary>
        private void AutoDistribute()
        {
            var unassignedBoxes = new List<int>();
            var unassignedStations = new List<int> { 0, 1, 2 };

            // Recopilar cajas sin asignar y estaciones ya usadas
            for (int i = 0; i < 3; i++)
            {
                int station = BoxAssignments[i];
                if (station == -1)
                    unassignedBoxes.Add(i);
                else
                    unassignedStations.Remove(station);
            }

            // Asignar al azar
            foreach (int box in unassignedBoxes)
            {
                if (unassignedStations.Count == 0) break;
                int rndIdx = UnityEngine.Random.Range(0, unassignedStations.Count);
                BoxAssignments.Set(box, unassignedStations[rndIdx]);
                unassignedStations.RemoveAt(rndIdx);
            }
        }

        /// <summary>
        /// Calcula puntuación y pasa a Reveal.
        /// </summary>
        private void DoReveal()
        {
            // Calcular puntos: Dinero(1)= +1, Bomba(2)= -1
            for (int i = 0; i < 3; i++)
            {
                int station = BoxAssignments[i];
                if (station < 0 || station >= 3) continue;
                int content = BoxContents[i];
                int points = (content == 1) ? 1 : -1;
                RoundScores.Set(station, RoundScores[station] + points);
            }

            // Sumar a Score del PlayerData
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

        /// <summary>
        /// Devuelve el StationIndex del jugador local (-1 si no está asignado).
        /// </summary>
        public int GetLocalStationIndex()
        {
            if (Runner == null) return -1;
            if (PlayerData.TryGet(Runner.LocalPlayer, out var data))
                return data.StationIndex;
            return -1;
        }

        public override void Render()
        {
            // Lógica local: Cuando el estado ya no es Lobby, asignamos las cámaras una sola vez
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
                    {
                        // Solo se activa la cámara cuyo índice coincida con el StationIndex del jugador local
                        StationCameras[i].gameObject.SetActive(i == myData.StationIndex);
                    }
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
                    StationIndex = -1 // Sin asignar aún
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

                // Le asignamos a cada jugador conectado su posición en la línea (0, 1, o 2)
                foreach (var pair in PlayerData)
                {
                    if (pair.Value.IsConnected)
                    {
                        var data = pair.Value;
                        data.StationIndex = currentIndex;
                        PlayerData.Set(pair.Key, data);
                        currentIndex++;

                        // Límite de 3 jugadores para las 3 estaciones
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

                // ¡LA MAGIA! Le decimos SOLO a la compu del Jugador 1 que reproduzca la animación
                RPC_MostrarAnimacionExclusiva(info.Source, cajaIndex, premioDentro);

                // Avanzamos a la fase de pasar cajas
                State = EGameplayState.P1_Pass;
                PlayerTurnIndex = 1;
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
                {
                    scriptCaja.RevelarPremioExclusivo(tipoPremio);
                }
                else 
                { 
                    Debug.LogError($"Encontré la Caja_{cajaIndex}, pero NO tiene el script CajaVisual asignado."); 
                }
            }
            else 
            { 
                Debug.LogError($"CRÍTICO: No se encontró ningún GameObject en la escena llamado exactamente 'Caja_{cajaIndex}'"); 
            }
        }
    }
}