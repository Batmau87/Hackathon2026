using System.Collections.Generic;
using Fusion;
using UnityEngine;

[DisallowMultipleComponent]
public class GameRoundManager : NetworkBehaviour, INetworkRunnerCallbacks
{
    private const int BoxCount = 3;
    private const int PlayerCount = 3;

    [SerializeField] private float autoAdvanceDelaySeconds = 4f;
    [SerializeField] private bool autoAdvanceNextRound = true;

    [Networked] public RoundPhase Phase { get; private set; }
    [Networked] public RoundConfigType ChosenConfig { get; private set; }
    [Networked] public NetworkBool InspectorUsedInspection { get; private set; }
    [Networked] public NetworkBool RevealResultsApplied { get; private set; }
    [Networked] public int InspectedBoxIndex { get; private set; }
    [Networked] public int RoundSequence { get; private set; }
    [Networked] private TickTimer NextRoundTimer { get; set; }
    [Networked, Capacity(BoxCount)] public NetworkArray<NetworkBoxState> Boxes => default;

    private readonly List<PlayerRoundController> _playerControllers = new(PlayerCount);

    public int RequiredPlayerCount => PlayerCount;

    public override void Spawned()
    {
        Runner.AddCallbacks(this);
        CachePlayerControllers();

        if (Object.HasStateAuthority)
        {
            SyncAssignedPlayersWithRunner();
            ResetRoundState(resetScores: false);
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        runner.RemoveCallbacks(this);
    }

    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority)
            return;

        CachePlayerControllers();
        SyncAssignedPlayersWithRunner();

        switch (Phase)
        {
            case RoundPhase.ConfigChosen:
                Phase = RoundPhase.Inspecting;
                break;

            case RoundPhase.PassingToDistributor:
                Phase = RoundPhase.Distributing;
                break;

            case RoundPhase.Reveal:
                ProcessReveal();
                break;

            case RoundPhase.RoundFinished:
                if (autoAdvanceNextRound && NextRoundTimer.ExpiredOrNotRunning(Runner))
                    ResetRoundState(resetScores: false);
                break;
        }
    }

    public void RequestChooseConfiguration(RoundConfigType configType)
    {
        if (Object.HasStateAuthority)
        {
            HandleChooseConfiguration(configType, Runner.LocalPlayer);
            return;
        }

        Rpc_RequestChooseConfiguration(configType);
    }

    public void RequestInspectBox(int boxIndex)
    {
        if (Object.HasStateAuthority)
        {
            HandleInspectBox(boxIndex, Runner.LocalPlayer);
            return;
        }

        Rpc_RequestInspectBox(boxIndex);
    }

    public void RequestFinalDistribution(BoxAssignment assignmentA, BoxAssignment assignmentB, BoxAssignment assignmentC)
    {
        if (Object.HasStateAuthority)
        {
            HandleFinalDistribution(assignmentA, assignmentB, assignmentC, Runner.LocalPlayer);
            return;
        }

        Rpc_RequestFinalDistribution(assignmentA, assignmentB, assignmentC);
    }

    public PlayerRoundController GetPlayerControllerBySlot(int slotIndex)
    {
        CachePlayerControllers();

        for (int i = 0; i < _playerControllers.Count; i++)
        {
            if (_playerControllers[i].SlotIndex == slotIndex || _playerControllers[i].EditorSlotIndex == slotIndex)
                return _playerControllers[i];
        }

        return null;
    }

    public PlayerRoundController GetPlayerControllerByPlayerRef(PlayerRef playerRef)
    {
        CachePlayerControllers();

        for (int i = 0; i < _playerControllers.Count; i++)
        {
            if (_playerControllers[i].AssignedPlayer == playerRef)
                return _playerControllers[i];
        }

        return null;
    }

    public int GetAssignedPlayerCount()
    {
        CachePlayerControllers();

        int assignedCount = 0;
        for (int i = 0; i < _playerControllers.Count; i++)
        {
            if (_playerControllers[i].AssignedPlayer != PlayerRef.None)
                assignedCount++;
        }

        return assignedCount;
    }

    public NetworkBoxState GetBoxState(int boxIndex)
    {
        if (boxIndex < 0 || boxIndex >= BoxCount)
            return default;

        return Boxes[boxIndex];
    }

    public BoxContentType GetRevealedContentForSlot(int slotIndex)
    {
        PlayerRoundController controller = GetPlayerControllerBySlot(slotIndex);

        if (controller == null || controller.FinalBoxIndex < 0 || controller.FinalBoxIndex >= BoxCount)
            return BoxContentType.None;

        NetworkBoxState box = Boxes[controller.FinalBoxIndex];
        return box.IsRevealed ? box.Content : BoxContentType.None;
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void Rpc_RequestChooseConfiguration(RoundConfigType configType, RpcInfo info = default)
    {
        HandleChooseConfiguration(configType, info.Source);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void Rpc_RequestInspectBox(int boxIndex, RpcInfo info = default)
    {
        HandleInspectBox(boxIndex, info.Source);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void Rpc_RequestFinalDistribution(BoxAssignment assignmentA, BoxAssignment assignmentB, BoxAssignment assignmentC, RpcInfo info = default)
    {
        HandleFinalDistribution(assignmentA, assignmentB, assignmentC, info.Source);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void Rpc_DeliverPrivateInspectionResult(PlayerRef recipient, int boxIndex, BoxContentType content)
    {
        // El contenido real vive en el estado autoritativo compartido, pero esta RPC
        // controla la presentacion: solo el inspector destinatario lo muestra en UI.
        if (Runner.LocalPlayer != recipient)
            return;

        PlayerRoundController inspectorController = GetPlayerControllerByPlayerRef(recipient);
        inspectorController?.ReceivePrivateInspectionResult(boxIndex, content);
    }

    private void HandleChooseConfiguration(RoundConfigType configType, PlayerRef requestingPlayer)
    {
        if (!Object.HasStateAuthority || !HasAllPlayersAssigned())
            return;

        if (Phase != RoundPhase.WaitingForConfig)
            return;

        if (!IsPlayerInRole(requestingPlayer, PlayerRole.Configurator))
            return;

        ChosenConfig = configType;
        GenerateAndShuffleBoxes(configType);
        InspectorUsedInspection = false;
        RevealResultsApplied = false;
        InspectedBoxIndex = -1;
        NextRoundTimer = TickTimer.None;
        Phase = RoundPhase.ConfigChosen;
    }

    private void HandleInspectBox(int boxIndex, PlayerRef requestingPlayer)
    {
        if (!Object.HasStateAuthority)
            return;

        if (Phase != RoundPhase.Inspecting)
            return;

        if (InspectorUsedInspection)
            return;

        if (!IsPlayerInRole(requestingPlayer, PlayerRole.Inspector))
            return;

        if (boxIndex < 0 || boxIndex >= BoxCount)
            return;

        NetworkBoxState box = Boxes[boxIndex];
        if (box.CurrentOwnerSlot != (int)PlayerRole.Inspector)
            return;

        box.WasInspected = true;
        Boxes.Set(boxIndex, box);

        InspectorUsedInspection = true;
        InspectedBoxIndex = boxIndex;

        Rpc_DeliverPrivateInspectionResult(requestingPlayer, boxIndex, box.Content);
        Phase = RoundPhase.PassingToDistributor;
    }

    private void HandleFinalDistribution(BoxAssignment assignmentA, BoxAssignment assignmentB, BoxAssignment assignmentC, PlayerRef requestingPlayer)
    {
        if (!Object.HasStateAuthority)
            return;

        if (Phase != RoundPhase.Distributing)
            return;

        if (!IsPlayerInRole(requestingPlayer, PlayerRole.Distributor))
            return;

        if (!IsValidDistribution(assignmentA, assignmentB, assignmentC))
            return;

        CachePlayerControllers();

        for (int i = 0; i < _playerControllers.Count; i++)
            _playerControllers[i].AuthorityResetRoundState();

        ApplyAssignment(assignmentA);
        ApplyAssignment(assignmentB);
        ApplyAssignment(assignmentC);

        Phase = RoundPhase.Reveal;
    }

    private void ApplyAssignment(BoxAssignment assignment)
    {
        NetworkBoxState box = Boxes[assignment.BoxIndex];
        box.CurrentOwnerSlot = assignment.TargetPlayerSlot;
        box.IsFinallyAssigned = true;
        Boxes.Set(assignment.BoxIndex, box);

        PlayerRoundController recipient = GetPlayerControllerBySlot(assignment.TargetPlayerSlot);
        recipient?.AuthorityAssignFinalBox(assignment.BoxIndex);
    }

    private bool IsValidDistribution(BoxAssignment assignmentA, BoxAssignment assignmentB, BoxAssignment assignmentC)
    {
        BoxAssignment[] assignments = { assignmentA, assignmentB, assignmentC };
        bool[] usedBoxes = new bool[BoxCount];
        bool[] usedTargets = new bool[PlayerCount];

        for (int i = 0; i < assignments.Length; i++)
        {
            BoxAssignment assignment = assignments[i];

            if (assignment.BoxIndex < 0 || assignment.BoxIndex >= BoxCount)
                return false;

            if (assignment.TargetPlayerSlot < 0 || assignment.TargetPlayerSlot >= PlayerCount)
                return false;

            if (usedBoxes[assignment.BoxIndex] || usedTargets[assignment.TargetPlayerSlot])
                return false;

            usedBoxes[assignment.BoxIndex] = true;
            usedTargets[assignment.TargetPlayerSlot] = true;
        }

        return true;
    }

    private void ProcessReveal()
    {
        if (RevealResultsApplied)
            return;

        CachePlayerControllers();

        for (int i = 0; i < BoxCount; i++)
        {
            NetworkBoxState box = Boxes[i];
            box.IsRevealed = true;
            Boxes.Set(i, box);
        }

        for (int i = 0; i < _playerControllers.Count; i++)
        {
            PlayerRoundController controller = _playerControllers[i];

            if (controller.FinalBoxIndex < 0 || controller.FinalBoxIndex >= BoxCount)
                continue;

            BoxContentType assignedContent = Boxes[controller.FinalBoxIndex].Content;
            if (assignedContent == BoxContentType.Money)
                controller.AuthorityAddScore(1);
        }

        RevealResultsApplied = true;
        NextRoundTimer = TickTimer.CreateFromSeconds(Runner, autoAdvanceDelaySeconds);
        Phase = RoundPhase.RoundFinished;
    }

    private void GenerateAndShuffleBoxes(RoundConfigType configType)
    {
        List<BoxContentType> contents = new(BoxCount);

        if (configType == RoundConfigType.TwoMoneyOneBomb)
        {
            contents.Add(BoxContentType.Money);
            contents.Add(BoxContentType.Money);
            contents.Add(BoxContentType.Bomb);
        }
        else
        {
            contents.Add(BoxContentType.Money);
            contents.Add(BoxContentType.Bomb);
            contents.Add(BoxContentType.Bomb);
        }

        for (int i = 0; i < contents.Count; i++)
        {
            int randomIndex = Random.Range(i, contents.Count);
            (contents[i], contents[randomIndex]) = (contents[randomIndex], contents[i]);
        }

        for (int i = 0; i < BoxCount; i++)
        {
            NetworkBoxState boxState = new NetworkBoxState
            {
                BoxIndex = i,
                Content = contents[i],
                CurrentOwnerSlot = (int)PlayerRole.Inspector,
                WasInspected = false,
                IsFinallyAssigned = false,
                IsRevealed = false
            };

            Boxes.Set(i, boxState);
        }
    }

    private void ResetRoundState(bool resetScores)
    {
        CachePlayerControllers();

        RoundSequence += 1;
        ChosenConfig = default;
        InspectorUsedInspection = false;
        RevealResultsApplied = false;
        InspectedBoxIndex = -1;
        NextRoundTimer = TickTimer.None;
        Phase = RoundPhase.WaitingForConfig;

        for (int i = 0; i < BoxCount; i++)
        {
            NetworkBoxState emptyBox = new NetworkBoxState
            {
                BoxIndex = i,
                Content = BoxContentType.None,
                CurrentOwnerSlot = -1,
                WasInspected = false,
                IsFinallyAssigned = false,
                IsRevealed = false
            };

            Boxes.Set(i, emptyBox);
        }

        for (int i = 0; i < _playerControllers.Count; i++)
        {
            _playerControllers[i].AuthorityResetRoundState();

            if (resetScores)
            {
                while (_playerControllers[i].Score > 0)
                    _playerControllers[i].AuthorityAddScore(-1);
            }
        }
    }

    private bool HasAllPlayersAssigned()
    {
        return GetAssignedPlayerCount() == PlayerCount;
    }

    private bool IsPlayerInRole(PlayerRef player, PlayerRole role)
    {
        PlayerRoundController controller = GetPlayerControllerByPlayerRef(player);
        return controller != null && controller.Role == role;
    }

    private void CachePlayerControllers()
    {
        if (_playerControllers.Count == PlayerCount)
            return;

        _playerControllers.Clear();

        PlayerRoundController[] foundControllers = FindObjectsByType<PlayerRoundController>(FindObjectsSortMode.None);
        System.Array.Sort(foundControllers, (a, b) => a.EditorSlotIndex.CompareTo(b.EditorSlotIndex));

        for (int i = 0; i < foundControllers.Length; i++)
            _playerControllers.Add(foundControllers[i]);
    }

    private void SyncAssignedPlayersWithRunner()
    {
        CachePlayerControllers();

        if (_playerControllers.Count == 0)
            return;

        List<PlayerRef> activePlayers = new(PlayerCount);
        foreach (PlayerRef player in Runner.ActivePlayers)
            activePlayers.Add(player);

        activePlayers.Sort((left, right) => left.RawEncoded.CompareTo(right.RawEncoded));

        bool rosterChanged = false;

        for (int i = 0; i < _playerControllers.Count; i++)
        {
            PlayerRoundController controller = _playerControllers[i];

            if (controller.AssignedPlayer == PlayerRef.None)
                continue;

            if (activePlayers.Contains(controller.AssignedPlayer))
                continue;

            controller.AuthorityClearAssignedPlayer();
            controller.AuthorityResetRoundState();
            rosterChanged = true;
        }

        for (int i = 0; i < activePlayers.Count; i++)
        {
            PlayerRef activePlayer = activePlayers[i];
            if (GetPlayerControllerByPlayerRef(activePlayer) != null)
                continue;

            AssignPlayerToFirstFreeSlot(activePlayer);
            rosterChanged = true;
        }

        if (rosterChanged && Phase != RoundPhase.WaitingForConfig)
            ResetRoundState(resetScores: false);
    }

    private void AssignPlayerToFirstFreeSlot(PlayerRef player)
    {
        CachePlayerControllers();

        for (int i = 0; i < _playerControllers.Count; i++)
        {
            if (_playerControllers[i].AssignedPlayer != PlayerRef.None)
                continue;

            _playerControllers[i].AuthorityAssignPlayer(player);
            return;
        }
    }

    private void RemovePlayerFromAssignedSlot(PlayerRef player)
    {
        CachePlayerControllers();

        for (int i = 0; i < _playerControllers.Count; i++)
        {
            if (_playerControllers[i].AssignedPlayer != player)
                continue;

            _playerControllers[i].AuthorityClearAssignedPlayer();
            _playerControllers[i].AuthorityResetRoundState();

            if (Phase != RoundPhase.WaitingForConfig)
                ResetRoundState(resetScores: false);

            return;
        }
    }

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        if (!Object.HasStateAuthority)
            return;

        AssignPlayerToFirstFreeSlot(player);
    }

    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        if (!Object.HasStateAuthority)
            return;

        RemovePlayerFromAssignedSlot(player);
    }

    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnInput(NetworkRunner runner, NetworkInput input) { }
    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    void INetworkRunnerCallbacks.OnConnectedToServer(NetworkRunner runner) { }
    void INetworkRunnerCallbacks.OnDisconnectedFromServer(NetworkRunner runner, Fusion.Sockets.NetDisconnectReason reason) { }
    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
    public void OnConnectFailed(NetworkRunner runner, Fusion.Sockets.NetAddress remoteAddress, Fusion.Sockets.NetConnectFailedReason reason) { }
    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, Fusion.Sockets.ReliableKey key, System.ArraySegment<byte> data) { }
    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, Fusion.Sockets.ReliableKey key, float progress) { }
    public void OnSceneLoadStart(NetworkRunner runner) { }
    public void OnSceneLoadDone(NetworkRunner runner) { }
    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) { }
}
