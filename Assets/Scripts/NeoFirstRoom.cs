using UnityEngine;

/// <summary>Scene-authored introduction and first puzzle. No runtime map construction.</summary>
public sealed class NeoFirstRoom : MonoBehaviour
{
    public NeoGridBoard board;
    public Controller controller;
    public PlayerAI human;
    public PartnerAI machop;
    public NeoPushRock rock;
    public Vector2Int humanStart = new Vector2Int(4, 5);
    public Vector2Int companionStart = new Vector2Int(5, 6);
    public Vector2Int safeExit = new Vector2Int(20, 9);
    public int tutorialStep;
    DemoBootstrap flow;
    bool complete;
    bool initialized;
    bool warned;
    public bool Complete => complete;

    public void Initialize(DemoBootstrap owner)
    {
        if (initialized) return;
        initialized = true;
        flow = owner;
        board.navigationTarget = controller.Active != null ? controller.Active.transform : human.transform;
        board.InitializeNavigation();
        complete = flow.IsPuzzleComplete("paso01");
        tutorialStep = flow.RoomTutorialStep;
        human.GetComponent<NeoGridMotor>().Teleport(NeoGridBoard.Center(complete ? safeExit : humanStart));
        if (flow.TutorialComplete) GrantMachop(false);
        else machop.gameObject.SetActive(false);
        if (complete) rock.ResetRock(true);
        rock.Settled += OnRockSettled;
        controller.ActiveChanged += OnControlChanged;
        RefreshObjective();
    }
    void OnDestroy()
    {
        if (rock != null) rock.Settled -= OnRockSettled;
        if (controller != null) controller.ActiveChanged -= OnControlChanged;
    }
    void Update()
    {
        if (!initialized || DemoBootstrap.IsPaused) return;
        if (tutorialStep == 0 && human.GetComponent<NeoGridMotor>().Cell != humanStart)
        { tutorialStep = 1; flow.RoomTutorialStep = 1; RefreshObjective(); }
        var motor = machop.GetComponent<NeoGridMotor>();
        if (machop.gameObject.activeSelf && motor.ReturnBlocked && !warned)
        { warned = true; flow.ShowToast("Machop no encuentra paso. Puedes reiniciar el sector desde Pausa."); }
        if (!motor.ReturnBlocked) warned = false;
        if (!complete && tutorialStep >= 3 && controller.Active == human && human.transform.position.x >= safeExit.x)
        {
            complete = true;
            tutorialStep = 4;
            flow.RoomTutorialStep = 4;
            flow.MarkPuzzleComplete("paso01");
            flow.CheckpointReached("paso01_salida");
            RefreshObjective();
            flow.ShowRoomDialogue(new[] { "¡El paso está despejado! Machop y tú ya pueden continuar hacia el campamento.", "Completaste la primera sala. Tu avance está guardado; puedes volver al menú y usar Continuar." }, RefreshObjective);
        }
    }
    public void TalkToRanger()
    {
        if (flow == null) return;
        if (flow.TutorialComplete)
        { flow.ShowRoomDialogue(new[] { "La roca se mueve de una casilla por vez. Busca el hueco junto al paso; puedes reiniciar el puzzle desde Pausa." }, RefreshObjective); return; }
        flow.ShowRoomDialogue(new[] {
            "Un desprendimiento bloqueó el sendero al campamento. Necesito ayuda para despejarlo.",
            "Te presto a Machop. Pulsa ESPACIO para controlarlo; tu personaje esperará a salvo." }, () => GrantMachop(true));
    }
    void GrantMachop(bool save)
    {
        machop.gameObject.SetActive(true);
        machop.master = human.transform;
        machop.GetComponent<NeoGridMotor>().Teleport(NeoGridBoard.Center(complete ? safeExit + Vector2Int.left : companionStart));
        controller.RegisterPartner(machop);
        tutorialStep = Mathf.Max(tutorialStep, 2);
        if (save) flow.ReceiveRoomCompanion(tutorialStep);
        RefreshObjective();
    }
    void OnControlChanged(Controllable actor)
    {
        if (actor == machop && tutorialStep == 2)
        { tutorialStep = 3; flow.RoomTutorialStep = 3; }
        RefreshObjective();
    }
    void OnRockSettled()
    {
        if (rock.Solved) { flow.ShowToast("Paso despejado. ESPACIO para volver a tu personaje."); }
        RefreshObjective();
    }
    public void RefreshObjective()
    {
        if (flow == null) return;
        if (complete) flow.SetObjective("Primera sala completada. ESC abre pausa y permite volver al menú.");
        else if (!flow.TutorialComplete) flow.SetObjective(tutorialStep == 0 ? "WASD: camina hacia el guardabosques." : "Acércate al guardabosques y pulsa E para hablar.");
        else if (tutorialStep == 2) flow.SetObjective("ESPACIO: toma el control de Machop.");
        else if (!rock.Solved) flow.SetObjective("Con Machop, acércate a la roca y pulsa E para empujarla al hueco superior.");
        else flow.SetObjective("ESPACIO: vuelve a tu personaje y cruza el paso hacia la derecha.");
    }
    public void ResetRoom()
    {
        controller.ReturnControlToHuman();
        human.GetComponent<NeoGridMotor>().Teleport(NeoGridBoard.Center(complete ? safeExit : humanStart));
        machop.GetComponent<NeoGridMotor>().Teleport(NeoGridBoard.Center(complete ? safeExit + Vector2Int.left : companionStart));
        rock.ResetRock(complete);
        machop.RestoreEnergy();
        machop.GetComponent<BasicCombatScript>().RestoreHealth();
        RefreshObjective();
    }
}
