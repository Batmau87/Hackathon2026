using HackathonJuego;
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

    private Gameplay _gameplay;
    private Quaternion _rotationOffset;

    private void Awake()
    {
        _rotationOffset = Quaternion.Euler(rotationOffsetEuler);
        ResolveGameplay();
    }

    private void Update()
    {
        ResolveGameplay();

        if (_gameplay == null || !_gameplay.isActiveAndEnabled)
            return;

        for (int boxIndex = 0; boxIndex < boxTransforms.Length; boxIndex++)
        {
            Transform boxTransform = boxTransforms[boxIndex];
            if (boxTransform == null)
                continue;

            Transform target = GetTargetForBox(boxIndex);

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

    private Transform GetTargetForBox(int boxIndex)
    {
        switch (_gameplay.State)
        {
            case EGameplayState.Lobby:
            case EGameplayState.P0_Config:
                return GetSlot(configuratorSlots, boxIndex);

            case EGameplayState.P1_Inspect:
            case EGameplayState.P1_Pass:
                return GetSlot(inspectorSlots, boxIndex);

            case EGameplayState.P2_Distribute:
                int assignedStation = _gameplay.BoxAssignments[boxIndex];
                if (assignedStation >= 0)
                    return GetSlotByOwner(assignedStation, boxIndex);

                return GetSlot(distributorSlots, boxIndex);

            case EGameplayState.Reveal:
            case EGameplayState.Finished:
                int ownerSlot = _gameplay.BoxAssignments[boxIndex];
                if (ownerSlot < 0)
                    return GetSlot(distributorSlots, boxIndex);

                return GetSlotByOwner(ownerSlot, boxIndex);

            default:
                return GetSlot(configuratorSlots, boxIndex);
        }
    }

    private static Transform GetSlot(Transform[] slots, int boxIndex)
    {
        if (slots == null || boxIndex < 0 || boxIndex >= slots.Length)
            return null;

        return slots[boxIndex];
    }

    private void ResolveGameplay()
    {
        if (_gameplay != null)
            return;

        _gameplay = FindFirstObjectByType<Gameplay>();
    }
}
