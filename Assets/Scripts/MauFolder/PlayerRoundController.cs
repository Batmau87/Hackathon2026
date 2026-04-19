using Fusion;
using UnityEngine;

[DisallowMultipleComponent]
public class PlayerRoundController : NetworkBehaviour
{
    [SerializeField] private int editorSlotIndex;
    [SerializeField] private PlayerRole editorRole;

    [Networked] public int SlotIndex { get; private set; }
    [Networked] public PlayerRole Role { get; private set; }
    [Networked] public int Score { get; private set; }
    [Networked] public PlayerRef AssignedPlayer { get; private set; }
    [Networked] public int FinalBoxIndex { get; private set; }

    private GameRoundManager _roundManager;
    private int _observedRoundSequence = -1;
    private bool _hasPrivateInspectionResult;
    private int _privateInspectedBoxIndex = -1;
    private BoxContentType _privateInspectedContent = BoxContentType.None;

    public int EditorSlotIndex => editorSlotIndex;
    public PlayerRole EditorRole => editorRole;
    public bool HasPrivateInspectionResult => _hasPrivateInspectionResult;
    public int PrivateInspectedBoxIndex => _privateInspectedBoxIndex;
    public BoxContentType PrivateInspectedContent => _privateInspectedContent;
    public bool IsControlledByLocalPlayer => Runner != null && AssignedPlayer == Runner.LocalPlayer;

    public override void Spawned()
    {
        if (Object.HasStateAuthority)
        {
            SlotIndex = editorSlotIndex;
            Role = editorRole;
            FinalBoxIndex = -1;
        }

        ResolveRoundManager();
        ClearPrivateInspectionData();
    }

    public override void Render()
    {
        ResolveRoundManager();

        if (_roundManager == null)
            return;

        if (_observedRoundSequence == _roundManager.RoundSequence)
            return;

        _observedRoundSequence = _roundManager.RoundSequence;
        ClearPrivateInspectionData();
    }

    public void ChooseRoundConfiguration(RoundConfigType configType)
    {
        ResolveRoundManager();
        _roundManager?.RequestChooseConfiguration(configType);
    }

    public void InspectBox(int boxIndex)
    {
        ResolveRoundManager();
        _roundManager?.RequestInspectBox(boxIndex);
    }

    public void SubmitDistribution(BoxAssignment assignmentA, BoxAssignment assignmentB, BoxAssignment assignmentC)
    {
        ResolveRoundManager();
        _roundManager?.RequestFinalDistribution(assignmentA, assignmentB, assignmentC);
    }

    public void ClearLocalInspectionForDebug()
    {
        ClearPrivateInspectionData();
    }

    public void AuthorityAssignPlayer(PlayerRef player)
    {
        AssignedPlayer = player;
    }

    public void AuthorityClearAssignedPlayer()
    {
        AssignedPlayer = PlayerRef.None;
    }

    public void AuthorityResetRoundState()
    {
        FinalBoxIndex = -1;
    }

    public void AuthorityAssignFinalBox(int boxIndex)
    {
        FinalBoxIndex = boxIndex;
    }

    public void AuthorityAddScore(int points)
    {
        Score += points;
    }

    public void ReceivePrivateInspectionResult(int boxIndex, BoxContentType content)
    {
        _hasPrivateInspectionResult = true;
        _privateInspectedBoxIndex = boxIndex;
        _privateInspectedContent = content;
    }

    private void ResolveRoundManager()
    {
        if (_roundManager != null)
            return;

        _roundManager = FindFirstObjectByType<GameRoundManager>();
    }

    private void ClearPrivateInspectionData()
    {
        _hasPrivateInspectionResult = false;
        _privateInspectedBoxIndex = -1;
        _privateInspectedContent = BoxContentType.None;
    }
}
