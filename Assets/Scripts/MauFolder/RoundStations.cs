using UnityEngine;

[DisallowMultipleComponent]
public class RoundStations : MonoBehaviour
{
    [Header("3 puntos frente al Configurator")]
    [SerializeField] private Transform[] configuratorSlots = new Transform[3];

    [Header("3 puntos frente al Inspector")]
    [SerializeField] private Transform[] inspectorSlots = new Transform[3];

    [Header("3 puntos frente al Distributor")]
    [SerializeField] private Transform[] distributorSlots = new Transform[3];

    [Header("Cajas de la ronda en orden Box1, Box2, Box3")]
    [SerializeField] private Transform[] boxTransforms = new Transform[3];

    [Header("Movimiento visual")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float rotateSpeed = 8f;
    [SerializeField] private Vector3 positionOffset;
    [SerializeField] private Vector3 rotationOffsetEuler;

    private GameRoundManager _roundManager;
    private Quaternion _rotationOffset;

    private void Awake()
    {
        _rotationOffset = Quaternion.Euler(rotationOffsetEuler);
        ResolveRoundManager();
    }

    private void Update()
    {
        ResolveRoundManager();

        if (_roundManager == null)
            return;

        for (int boxIndex = 0; boxIndex < boxTransforms.Length; boxIndex++)
        {
            Transform boxTransform = boxTransforms[boxIndex];
            if (boxTransform == null)
                continue;

            NetworkBoxState boxState = _roundManager.GetBoxState(boxIndex);
            Transform target = GetTargetForBox(boxIndex, _roundManager.Phase, boxState.CurrentOwnerSlot);

            if (target == null)
                continue;

            Vector3 targetPosition = target.position + positionOffset;
            Quaternion targetRotation = target.rotation * _rotationOffset;

            boxTransform.position = Vector3.MoveTowards(
                boxTransform.position,
                targetPosition,
                moveSpeed * Time.deltaTime);

            boxTransform.rotation = Quaternion.Slerp(
                boxTransform.rotation,
                targetRotation,
                rotateSpeed * Time.deltaTime);
        }
    }

    public Transform GetTargetForBox(int boxIndex, RoundPhase phase, int finalOwnerSlot)
    {
        switch (phase)
        {
            case RoundPhase.WaitingForConfig:
                return GetSlot(configuratorSlots, boxIndex);

            case RoundPhase.ConfigChosen:
            case RoundPhase.Inspecting:
                return GetSlot(inspectorSlots, boxIndex);

            case RoundPhase.PassingToDistributor:
            case RoundPhase.Distributing:
                return GetSlot(distributorSlots, boxIndex);

            case RoundPhase.Reveal:
            case RoundPhase.RoundFinished:
                return GetSlotByOwner(finalOwnerSlot, boxIndex);

            default:
                return GetSlot(configuratorSlots, boxIndex);
        }
    }

    private Transform GetSlotByOwner(int ownerSlot, int boxIndex)
    {
        switch (ownerSlot)
        {
            case 0: return GetSlot(configuratorSlots, boxIndex);
            case 1: return GetSlot(inspectorSlots, boxIndex);
            case 2: return GetSlot(distributorSlots, boxIndex);
            default: return GetSlot(configuratorSlots, boxIndex);
        }
    }

    private static Transform GetSlot(Transform[] slots, int boxIndex)
    {
        if (slots == null || boxIndex < 0 || boxIndex >= slots.Length)
            return null;

        return slots[boxIndex];
    }

    private void ResolveRoundManager()
    {
        if (_roundManager != null)
            return;

        _roundManager = FindFirstObjectByType<GameRoundManager>();
    }
}
