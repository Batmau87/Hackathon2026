using System.Collections.Generic;
using UnityEngine;
using Fusion;

namespace HackathonJuego
{
    // ... (Mantén tus structs y enums anteriores)

    public class Gameplay : NetworkBehaviour, IPlayerJoined, IPlayerLeft
    {
        // ... (Variables de cámaras y rondas anteriores)

        [Networked, Capacity(3)] public NetworkArray<int> BoxContents { get; } 
        [Networked] public int OpenedBoxIndex { get; set; } = -1;
        
        // Guardamos a qué PlayerRef se le asigna cada caja (índice 0, 1, 2)
        [Networked, Capacity(3)] public NetworkArray<PlayerRef> BoxAssignments { get; }

        // --- FASE 0: CONFIGURADOR ---
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_SeleccionarPaquete(int opcionSeleccionada, RpcInfo info = default)
        {
            if (State != EGameplayState.P0_Config) return;

            if (opcionSeleccionada == 1) { DineroEnJuego = 2; BombasEnJuego = 1; }
            else { DineroEnJuego = 1; BombasEnJuego = 2; }

            MezclarCajas();
            State = EGameplayState.P1_Inspect;
            PlayerTurnIndex = 1; // Turno del Informado
        }

        // --- FASE 1: INFORMADO ---
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_InspeccionarCaja(int cajaIndex, RpcInfo info = default)
        {
            if (State != EGameplayState.P1_Inspect) return;

            OpenedBoxIndex = cajaIndex;
            int contenido = BoxContents[cajaIndex];

            // Mandamos el Debug exclusivo
            RPC_MandarInformacionPrivada(info.Source, cajaIndex, contenido);

            State = EGameplayState.P2_Distribute;
            PlayerTurnIndex = 2; // Turno del Repartidor
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        public void RPC_MandarInformacionPrivada([RpcTarget] PlayerRef player, int index, int contenido)
        {
            string queEs = contenido == 1 ? "💰 DINERO" : "💣 BOMBA";
            Debug.Log($"<color=cyan><b>SISTEMA:</b> La Caja {index} tiene {queEs}. ¡No le digas a nadie!</color>");
        }

        // --- FASE 2: REPARTIDOR (Lo que te faltaba) ---
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_FinalizarDistribucion(PlayerRef p0, PlayerRef p1, PlayerRef p2)
        {
            if (State != EGameplayState.P2_Distribute) return;

            // Asignamos las cajas a los jugadores
            BoxAssignments.Set(0, p0);
            BoxAssignments.Set(1, p1);
            BoxAssignments.Set(2, p2);

            State = EGameplayState.Reveal;
            Debug.Log("Distribución terminada. ¡Abran las cajas!");
            
            // Aquí puedes llamar a una función que sume los puntos
            CalcularPuntajes();
        }

        private void CalcularPuntajes()
        {
            for (int i = 0; i < 3; i++)
            {
                PlayerRef duenoDeLaCaja = BoxAssignments[i];
                if (BoxContents[i] == 1) // Es dinero
                {
                    if (PlayerData.TryGet(duenoDeLaCaja, out var data))
                    {
                        data.Score += 1000;
                        PlayerData.Set(duenoDeLaCaja, data);
                    }
                }
            }
            // Después de unos segundos, podrías rotar turnos o reiniciar ronda
        }
    }
using System.Collections.Generic;
using UnityEngine;
using Fusion;

namespace HackathonJuego
{
    // ... (Mantén tus structs y enums anteriores)

    public class Gameplay : NetworkBehaviour, IPlayerJoined, IPlayerLeft
    {
        // ... (Variables de cámaras y rondas anteriores)

        [Networked, Capacity(3)] public NetworkArray<int> BoxContents { get; } 
        [Networked] public int OpenedBoxIndex { get; set; } = -1;
        
        // Guardamos a qué PlayerRef se le asigna cada caja (índice 0, 1, 2)
        [Networked, Capacity(3)] public NetworkArray<PlayerRef> BoxAssignments { get; }

        // --- FASE 0: CONFIGURADOR ---
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_SeleccionarPaquete(int opcionSeleccionada, RpcInfo info = default)
        {
            if (State != EGameplayState.P0_Config) return;

            if (opcionSeleccionada == 1) { DineroEnJuego = 2; BombasEnJuego = 1; }
            else { DineroEnJuego = 1; BombasEnJuego = 2; }

            MezclarCajas();
            State = EGameplayState.P1_Inspect;
            PlayerTurnIndex = 1; // Turno del Informado
        }

        // --- FASE 1: INFORMADO ---
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_InspeccionarCaja(int cajaIndex, RpcInfo info = default)
        {
            if (State != EGameplayState.P1_Inspect) return;

            OpenedBoxIndex = cajaIndex;
            int contenido = BoxContents[cajaIndex];

            // Mandamos el Debug exclusivo
            RPC_MandarInformacionPrivada(info.Source, cajaIndex, contenido);

            State = EGameplayState.P2_Distribute;
            PlayerTurnIndex = 2; // Turno del Repartidor
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        public void RPC_MandarInformacionPrivada([RpcTarget] PlayerRef player, int index, int contenido)
        {
            string queEs = contenido == 1 ? "💰 DINERO" : "💣 BOMBA";
            Debug.Log($"<color=cyan><b>SISTEMA:</b> La Caja {index} tiene {queEs}. ¡No le digas a nadie!</color>");
        }

        // --- FASE 2: REPARTIDOR (Lo que te faltaba) ---
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_FinalizarDistribucion(PlayerRef p0, PlayerRef p1, PlayerRef p2)
        {
            if (State != EGameplayState.P2_Distribute) return;

            // Asignamos las cajas a los jugadores
            BoxAssignments.Set(0, p0);
            BoxAssignments.Set(1, p1);
            BoxAssignments.Set(2, p2);

            State = EGameplayState.Reveal;
            Debug.Log("Distribución terminada. ¡Abran las cajas!");
            
            // Aquí puedes llamar a una función que sume los puntos
            CalcularPuntajes();
        }

        private void CalcularPuntajes()
        {
            for (int i = 0; i < 3; i++)
            {
                PlayerRef duenoDeLaCaja = BoxAssignments[i];
                if (BoxContents[i] == 1) // Es dinero
                {
                    if (PlayerData.TryGet(duenoDeLaCaja, out var data))
                    {
                        data.Score += 1000;
                        PlayerData.Set(duenoDeLaCaja, data);
                    }
                }
            }
            // Después de unos segundos, podrías rotar turnos o reiniciar ronda
        }
    }
}