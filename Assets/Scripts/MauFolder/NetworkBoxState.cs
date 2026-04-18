using Fusion;

public struct NetworkBoxState : INetworkStruct
{
    public int BoxIndex;
    public BoxContentType Content;
    public int CurrentOwnerSlot;
    public NetworkBool WasInspected;
    public NetworkBool IsFinallyAssigned;
    public NetworkBool IsRevealed;
}
