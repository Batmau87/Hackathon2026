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
        
        // El índice de la estación en la línea de proceso (0, 1 o 2)
        public int StationIndex; 
    }

    public enum EGameplayState { Lobby = 0, Running = 1, Finished = 2 }

    public class Gameplay : NetworkBehaviour, IPlayerJoined, IPlayerLeft
    {
        [Header("Cámaras de las Estaciones")]
        [Tooltip("Arrastra aquí las 3 cámaras de la escena: [0]=Abre Caja, [1]=Saca Objetos, [2]=Envía")]
        public Camera[] StationCameras; 

        [Networked, Capacity(3)] public NetworkDictionary<PlayerRef, PlayerData> PlayerData { get; }
        [Networked] public EGameplayState State { get; set; }

        private bool _camerasAssigned = false;

        public override void Spawned()
        {
            if (HasStateAuthority) State = EGameplayState.Lobby;
        }

        public override void Render()
        {
            // Lógica local: Cuando el estado cambia a Running, asignamos las cámaras una sola vez
            if (State == EGameplayState.Running && !_camerasAssigned)
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

                State = EGameplayState.Running;
            }
        }
    }
}