using UnityEngine;

/// <summary>Starts a scene-authored exploration map without building the old demo course.</summary>
public sealed class NeoExplorationScene : MonoBehaviour
{
    public NeoGridBoard board;
    public Controller controller;

    void Start()
    {
        board.navigationTarget = controller.Active.transform;
        board.InitializeNavigation();
        DemoBootstrap.BeginExploration();
        controller.enabled = true;
    }
}
