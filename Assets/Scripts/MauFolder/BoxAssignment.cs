using Fusion;

public struct BoxAssignment : INetworkStruct
{
    public int BoxIndex;
    public int TargetPlayerSlot;

    public BoxAssignment(int boxIndex, int targetPlayerSlot)
    {
        BoxIndex = boxIndex;
        TargetPlayerSlot = targetPlayerSlot;
    }
}
