using UnityEngine;
using Obi;
using System.Collections;

[RequireComponent(typeof(ObiRope))]
public class RopeAttach : MonoBehaviour
{
    private ObiRope rope;
    private ObiSolver solver;
    private bool isReady = false;

    [Header("Default Attachment (Optional)")]
    [Tooltip("If you set this, the rope will attach on Start.")]
    public Transform initialTarget;
    [Tooltip("The control point index to attach (0 = start, 1 = next, etc.)")]
    public int initialControlPointIndex = 0;

    void Start()
    {
        rope = GetComponent<ObiRope>();
        StartCoroutine(WaitForRopeInitialization());
    }

    private IEnumerator WaitForRopeInitialization()
    {
        while (rope.solver == null || !rope.isLoaded)
        {
            yield return null;
        }

        solver = rope.solver;
        isReady = true;

        if (initialTarget != null)
        {
            StartCoroutine(AttachAndSnap(initialTarget, initialControlPointIndex));
        }
    }

    public IEnumerator AttachAndSnap(Transform target, int controlPointIndex)
    {
        while (!isReady)
        {
            Debug.Log("Rope is not initialized yet. Waiting...");
            yield return null;
        }

        if (controlPointIndex < 0 || controlPointIndex >= rope.blueprint.groups.Count)
        {
            Debug.LogError($"Invalid control point index: {controlPointIndex}. Rope only has {rope.blueprint.groups.Count} control points.");
            yield break;
        }

        ObiParticleGroup groupToAttach = rope.blueprint.groups[controlPointIndex];

        ObiParticleAttachment attachment = FindOrCreateAttachmentForGroup(groupToAttach);

        attachment.enabled = false;

        attachment.attachmentType = ObiParticleAttachment.AttachmentType.Static;

        foreach (int particleIndex in groupToAttach.particleIndices)
        {
            int solverIndex = rope.solverIndices[particleIndex];

            if (solverIndex < 0 || solverIndex >= solver.positions.count) continue;

            Vector3 targetPositionInSolverSpace =
                solver.transform.InverseTransformPoint(target.position);

            solver.positions[solverIndex] = targetPositionInSolverSpace;

            solver.renderablePositions[solverIndex] = targetPositionInSolverSpace;
        }

        attachment.target = target;
        attachment.enabled = true;

        Debug.Log($"Rope attached control point {controlPointIndex} to {target.name}");
    }

    private ObiParticleAttachment FindOrCreateAttachmentForGroup(ObiParticleGroup group)
    {
        var allAttachments = GetComponents<ObiParticleAttachment>();

        foreach (var attachment in allAttachments)
        {
            if (attachment.particleGroup == group)
            {
                Debug.Log("Found existing attachment for this group. Re-using it.");
                return attachment;
            }
        }

        Debug.Log("Creating new attachment component for group.");
        var newAttachment = gameObject.AddComponent<ObiParticleAttachment>();
        newAttachment.particleGroup = group;
        return newAttachment;
    }
}