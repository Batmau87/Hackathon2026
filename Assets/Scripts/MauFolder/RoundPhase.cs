public enum RoundPhase : byte
{
    WaitingForConfig = 0,
    ConfigChosen = 1,
    Inspecting = 2,
    PassingToDistributor = 3,
    Distributing = 4,
    Reveal = 5,
    RoundFinished = 6
}
