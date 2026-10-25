using UnityEngine;

public sealed class DemoCheckpoint : MonoBehaviour
{
    [SerializeField] private string checkpointId = "checkpoint";

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        DemoBootstrap flow = FindAnyObjectByType<DemoBootstrap>();
        if (flow != null) flow.CheckpointReached(checkpointId);
    }
}
