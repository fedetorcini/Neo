using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>Builds the first playable route around the existing Neo actors at runtime.</summary>
public sealed class DemoAdventureCourse : MonoBehaviour
{
    private const int RoomBottom = 0;
    private const int RoomTop = 8;
    private DemoBootstrap flow;
    private Controller controller;
    private PlayerAI human;
    private PartnerAI machop;
    private PartnerAI pikachuPartner;
    private DemoWorldInteractable ranger;
    private DemoWorldInteractable camp;
    private DemoBattleEnemy wildPikachu;
    private DemoBattleEnemy electrode;
    private DemoCourseGate[] gates;
    private Transform aimArrow;
    private Transform aimPoint;
    private Tilemap terrainTilemap;
    private TileBase grassTile;
    private TileBase pathTile;
    private Sprite[] treeSprites;
    private readonly List<DemoPuzzleRock> rocks = new List<DemoPuzzleRock>();
    private bool initialized;
    private bool savedPuzzle1;
    private bool savedPuzzle2;
    private bool savedPuzzle3;
    private bool savedCaptured;
    private bool savedBossDefeated;
    private string lastCheckpointSeen;

    public void Initialize(DemoBootstrap demoFlow, Controller sceneController)
    {
        if (initialized || demoFlow == null || sceneController == null) return;
        initialized = true;
        flow = demoFlow;
        controller = sceneController;
        human = FindHuman();
        machop = FindMachop();
        if (human == null || machop == null)
        {
            Debug.LogError("Neo Demo: no se encontraron el personaje y Machop en la escena.");
            return;
        }

        savedPuzzle1 = flow.IsPuzzleComplete("puzzle1");
        savedPuzzle2 = flow.IsPuzzleComplete("puzzle2");
        savedPuzzle3 = flow.IsPuzzleComplete("puzzle3");
        savedCaptured = flow.PikachuCaptured;
        savedBossDefeated = flow.BossDefeated;

        CachePrototypeArt();
        HidePrototypeStage();
        ConfigureStarter();
        BuildBoard();
        BuildActors();
        BuildPuzzles();
        BuildEncounters();
        ApplySavedProgress();
        PlaceAtCheckpoint(flow.Checkpoint);
        ConfigureCamera();
        CreateAimIndicator();
        lastCheckpointSeen = flow.Checkpoint;
        Physics2D.SyncTransforms();
    }

    private void Update()
    {
        if (!initialized || aimArrow == null || aimPoint == null) return;
        if (DemoBootstrap.IsPaused || controller == null || !(controller.Active is PartnerAI activePartner) ||
            Camera.main == null || (UnityEngine.EventSystems.EventSystem.current != null && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject()))
        {
            aimArrow.gameObject.SetActive(false);
            aimPoint.gameObject.SetActive(false);
            return;
        }

        Vector3 cursor = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        cursor.z = activePartner.transform.position.z;
        Vector2 direction = (cursor - activePartner.transform.position).normalized;
        if (direction.sqrMagnitude < .001f) direction = Vector2.right;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        aimArrow.gameObject.SetActive(true);
        aimPoint.gameObject.SetActive(true);
        aimArrow.position = activePartner.transform.position + (Vector3)(direction * .72f);
        aimArrow.rotation = Quaternion.Euler(0f, 0f, angle);
        aimPoint.position = activePartner.transform.position + (Vector3)(direction * 1.35f);
    }

    private PlayerAI FindHuman()
    {
        if (controller.controllablesManual != null)
            foreach (Controllable item in controller.controllablesManual)
                if (item is PlayerAI found) return found;
        return FindAnyObjectByType<PlayerAI>();
    }

    private PartnerAI FindMachop()
    {
        if (controller.controllablesManual != null)
            foreach (Controllable item in controller.controllablesManual)
                if (item is PartnerAI found) return found;
        return FindAnyObjectByType<PartnerAI>();
    }

    private void HidePrototypeStage()
    {
        foreach (SpriteRenderer renderer in FindObjectsByType<SpriteRenderer>())
        {
            if (IsActor(renderer.transform)) continue;
            renderer.enabled = false;
        }
        foreach (TilemapRenderer renderer in FindObjectsByType<TilemapRenderer>())
            renderer.enabled = renderer.gameObject.name == "Terrain";
        foreach (Collider2D collider in FindObjectsByType<Collider2D>())
        {
            if (IsActor(collider.transform)) continue;
            collider.enabled = false;
        }
    }

    private void CachePrototypeArt()
    {
        foreach (Tilemap tilemap in FindObjectsByType<Tilemap>())
        {
            if (tilemap.gameObject.name == "Terrain")
            {
                terrainTilemap = tilemap;
                break;
            }
        }

        // These Tile assets are copies of Neo's original palette entries, so they ship in the player build.
        grassTile = Resources.Load<TileBase>("Demo/Art/NeoGrassA");
        pathTile = Resources.Load<TileBase>("Demo/Art/NeoForestPath");
        treeSprites = Resources.LoadAll<Sprite>("Demo/Art/NeoTrees");
        if (terrainTilemap == null || grassTile == null || pathTile == null || treeSprites.Length == 0)
            Debug.LogWarning("Neo Demo: no se pudo cargar todo el arte de terreno; revisa Resources/Demo/Art.");
    }

    private bool IsActor(Transform candidate)
    {
        return candidate == human.transform || candidate.IsChildOf(human.transform) ||
               candidate == machop.transform || candidate.IsChildOf(machop.transform);
    }

    private void ConfigureStarter()
    {
        machop.master = human.transform;
        machop.HOVER_DISTANCE = 2;
        machop.energyDrainPerDistancePerSecond = 3f;
        machop.minimumEnergyDrainPerSecond = 1f;
        machop.energyRecoveryPerSecond = 10f;
        if (machop.movementScript != null) machop.movementScript.UseLocalFallback();
        DemoPartnerCombat combat = machop.GetComponent<DemoPartnerCombat>();
        if (combat == null) combat = machop.gameObject.AddComponent<DemoPartnerCombat>();
        combat.SetSpecies(DemoSpecies.Machop);
        BasicCombatScript health = machop.GetComponent<BasicCombatScript>();
        if (health == null) health = machop.gameObject.AddComponent<BasicCombatScript>();
        health.MAX_HEALTH = 100f;
        health.RestoreHealth();
    }

    private void BuildBoard()
    {
        GameObject board = new GameObject("Demo Nivel 1");
        board.transform.SetParent(transform, false);
        PaintNeoTerrain();

        for (int y = RoomBottom; y <= RoomTop; y++)
        {
            CreateWall(board.transform, new Vector3(-.5f, y + .5f, 0f));
            CreateWall(board.transform, new Vector3(52.5f, y + .5f, 0f));
        }
        for (int x = -1; x <= 52; x++)
        {
            CreateWall(board.transform, new Vector3(x + .5f, -.5f, 0f));
            CreateWall(board.transform, new Vector3(x + .5f, 9.5f, 0f));
        }

        CreateDivider(board.transform, 9.5f);
        CreateDivider(board.transform, 19.5f);
        CreateDivider(board.transform, 31.5f);
        CreateDivider(board.transform, 41.5f);

        CreateWall(board.transform, new Vector3(25.5f, 4.5f, 0f));
        CreateWall(board.transform, new Vector3(28.5f, 4.5f, 0f));
        CreateForestBorder(board.transform);
        AddWorldLabel(board.transform, "PASO DEL GUARDABOSQUES", new Vector3(4f, 7.45f, 0f));
        AddWorldLabel(board.transform, "PLACA DE PRESIÓN", new Vector3(14f, 7.45f, 0f));
        AddWorldLabel(board.transform, "CÁMARA DE DOS PLACAS", new Vector3(25f, 7.45f, 0f));
        AddWorldLabel(board.transform, "ENCUENTRO", new Vector3(36f, 7.45f, 0f));
        AddWorldLabel(board.transform, "CAMPAMENTO", new Vector3(47f, 7.45f, 0f));
    }

    private void PaintNeoTerrain()
    {
        if (terrainTilemap == null || grassTile == null) return;

        // Rebuild this demo's ground so leftover prototype buildings and cliffs don't cut through the route.
        terrainTilemap.ClearAllTiles();

        // Use Neo's own two speckled grass tiles beneath the route and camera overscan.
        for (int x = -11; x <= 63; x++)
        {
            for (int y = -5; y <= 14; y++)
            {
                Vector3Int cell = new Vector3Int(x, y, 0);
                terrainTilemap.SetTile(cell, grassTile);
            }
        }

        // A connected dirt trail gives the rooms a clear route while retaining the game's tile language.
        if (pathTile != null)
        {
            for (int x = -1; x <= 52; x++)
                for (int y = 3; y <= 5; y++)
                    terrainTilemap.SetTile(new Vector3Int(x, y, 0), pathTile);
        }

        terrainTilemap.RefreshAllTiles();
    }

    private void CreateForestBorder(Transform parent)
    {
        if (treeSprites == null || treeSprites.Length == 0) return;

        int treeIndex = 0;
        for (int x = -11; x <= 62; x += 2)
        {
            CreateTree(parent, treeIndex++, new Vector3(x, 9f, 0f));
            CreateTree(parent, treeIndex++, new Vector3(x, -3f, 0f));
        }

        for (int y = 0; y <= 6; y += 3)
        {
            CreateTree(parent, treeIndex++, new Vector3(-3f, y, 0f));
            CreateTree(parent, treeIndex++, new Vector3(53f, y, 0f));
        }
    }

    private void CreateTree(Transform parent, int index, Vector3 position)
    {
        GameObject tree = new GameObject("Árbol de Neo");
        tree.transform.SetParent(parent, false);
        tree.transform.position = position;
        SpriteRenderer renderer = tree.AddComponent<SpriteRenderer>();
        renderer.sprite = treeSprites[index % treeSprites.Length];
        renderer.sortingLayerName = "Terrain";
        renderer.sortingOrder = -20;
    }

    private void BuildActors()
    {
        Teleportable playerMovement = human.movementScript;
        Teleportable partnerMovement = machop.movementScript;
        if (playerMovement != null) playerMovement.Teleport(new Vector3(1.5f, 4.5f, human.transform.position.z));
        else human.transform.position = new Vector3(1.5f, 4.5f, human.transform.position.z);
        if (partnerMovement != null) partnerMovement.Teleport(new Vector3(2.5f, 4.5f, machop.transform.position.z));
        else machop.transform.position = new Vector3(2.5f, 4.5f, machop.transform.position.z);

        ranger = CreatePerson("Guardabosques", new Vector3(1.5f, 6.5f, 0f),
            Resources.LoadAll<Sprite>("Demo/Art/Ranger"), DemoWorldInteractable.InteractionKind.Ranger);
    }

    private void BuildPuzzles()
    {
        gates = new DemoCourseGate[5];
        gates[0] = CreateGate("Puerta 1", new Vector3(9.5f, 4.5f, 0f));
        gates[1] = CreateGate("Puerta 2", new Vector3(19.5f, 4.5f, 0f));
        gates[2] = CreateGate("Puerta 3", new Vector3(31.5f, 4.5f, 0f));
        gates[3] = CreateGate("Puerta del encuentro", new Vector3(41.5f, 4.5f, 0f));
        gates[4] = CreateGate("Puerta del campamento", new Vector3(51.5f, 4.5f, 0f));

        CreatePlate("Placa 1", new Vector3(6.5f, 4.5f, 0f));
        CreatePlate("Placa 2", new Vector3(15.5f, 4.5f, 0f));
        CreatePlate("Placa superior", new Vector3(27.5f, 2.5f, 0f));
        CreatePlate("Placa inferior", new Vector3(27.5f, 6.5f, 0f));

        CreateRock("Roca 1", "puzzle1", new Vector3(5.5f, 4.5f, 0f), new Vector3(6.5f, 4.5f, 0f));
        CreateRock("Roca 2", "puzzle2", new Vector3(12.5f, 4.5f, 0f), new Vector3(15.5f, 4.5f, 0f));
        CreateRock("Roca 3 superior", "puzzle3", new Vector3(23.5f, 2.5f, 0f), new Vector3(27.5f, 2.5f, 0f));
        CreateRock("Roca 3 inferior", "puzzle3", new Vector3(23.5f, 6.5f, 0f), new Vector3(27.5f, 6.5f, 0f));
    }

    private void BuildEncounters()
    {
        Sprite[] pikachuFrames = Resources.LoadAll<Sprite>("Demo/Art/PikachuIdle");
        if (!savedCaptured)
        {
            GameObject pikachuObject = CreateEnemyObject("Pikachu", new Vector3(36.5f, 4.5f, 0f),
                FirstFrame(pikachuFrames), new Color(1f, 1f, 1f));
            wildPikachu = pikachuObject.AddComponent<DemoBattleEnemy>();
            wildPikachu.Configure(this, false, "Pikachu", 100f, pikachuFrames);
        }

        Sprite[] electrodeFrames = Resources.LoadAll<Sprite>("Demo/Art/Electrode");
        GameObject bossObject = CreateEnemyObject("Electrode alterado", new Vector3(47.5f, 4.5f, 0f),
            FirstFrame(electrodeFrames), new Color(1f, 1f, 1f));
        electrode = bossObject.AddComponent<DemoBattleEnemy>();
        electrode.Configure(this, true, "Electrode", 180f, electrodeFrames);
        if (savedBossDefeated) bossObject.SetActive(false);

        camp = CreatePerson("Campamento", new Vector3(50.5f, 4.5f, 0f), null, DemoWorldInteractable.InteractionKind.Camp);
    }

    private void ApplySavedProgress()
    {
        gates[0].SetOpen(savedPuzzle1);
        gates[1].SetOpen(savedPuzzle2);
        gates[2].SetOpen(savedPuzzle3);
        gates[3].SetOpen(savedCaptured);
        gates[4].SetOpen(savedBossDefeated);
        if (savedCaptured) AddPikachuToTeam();
        if ((savedPuzzle1 || flow.TutorialComplete) && ranger != null) ranger.gameObject.SetActive(false);
        if (savedBossDefeated && camp != null) camp.gameObject.SetActive(true);
    }

    public void TalkToRanger()
    {
        if (flow.TutorialComplete)
        {
            ShowHint("Machop está listo para despejar el sendero. Acércate a la roca y pulsa E.");
            return;
        }
        flow.StartRangerDialogue();
    }

    public void TutorialFinished()
    {
        if (ranger != null) ranger.gameObject.SetActive(false);
        flow.SetObjective("Controla a Machop y coloca la roca sobre la primera placa.");
        flow.CheckpointReached("puzzle1");
    }

    public void OnPuzzleRockSettled(DemoPuzzleRock rock)
    {
        if (rock.PuzzleId == "puzzle1" && IsAtTarget(rock)) CompletePuzzle(1);
        else if (rock.PuzzleId == "puzzle2" && IsAtTarget(rock)) CompletePuzzle(2);
        else if (rock.PuzzleId == "puzzle3")
        {
            int rocksOnPlates = 0;
            foreach (DemoPuzzleRock puzzleRock in rocks)
                if (puzzleRock.PuzzleId == "puzzle3" && IsAtTarget(puzzleRock)) rocksOnPlates++;
            if (rocksOnPlates >= 2) CompletePuzzle(3);
        }
    }

    private bool IsAtTarget(DemoPuzzleRock rock)
    {
        return (rock.transform.position - rock.TargetPosition).sqrMagnitude < .001f;
    }

    private void CompletePuzzle(int number)
    {
        string id = "puzzle" + number;
        flow.MarkPuzzleComplete(id);
        if (number == 1)
        {
            savedPuzzle1 = true;
            gates[0].SetOpen(true);
            flow.SetObjective("Cruza la puerta y coloca la roca de la segunda cámara sobre la placa.");
            flow.CheckpointReached("puzzle2");
        }
        else if (number == 2)
        {
            savedPuzzle2 = true;
            gates[1].SetOpen(true);
            flow.SetObjective("En la próxima cámara, coloca las dos rocas sobre sus placas.");
            flow.CheckpointReached("puzzle3");
        }
        else
        {
            savedPuzzle3 = true;
            gates[2].SetOpen(true);
            flow.SetObjective("Avanza hasta Pikachu. Debilítalo y luego acércate para capturarlo.");
            flow.CheckpointReached("pikachu");
        }
    }

    public void CapturePikachu(DemoBattleEnemy wild)
    {
        if (savedCaptured) return;
        savedCaptured = true;
        if (wild != null) wild.gameObject.SetActive(false);
        AddPikachuToTeam();
        gates[3].SetOpen(true);
        flow.MarkPikachuCaptured();
        flow.SetObjective("Elige a Machop o Pikachu y derrota a Electrode.");
        flow.CheckpointReached("jefe");
    }

    private void AddPikachuToTeam()
    {
        if (pikachuPartner != null || machop == null || controller == null) return;
        GameObject clone = Instantiate(machop.gameObject, new Vector3(33.5f, 4.5f, machop.transform.position.z), Quaternion.identity);
        clone.name = "Pikachu";
        clone.transform.position = new Vector3(33.5f, 4.5f, machop.transform.position.z);
        pikachuPartner = clone.GetComponent<PartnerAI>();
        if (pikachuPartner == null) pikachuPartner = clone.AddComponent<PartnerAI>();
        pikachuPartner.master = human.transform;
        pikachuPartner.energyDrainPerDistancePerSecond = 3f;
        pikachuPartner.minimumEnergyDrainPerSecond = 1f;
        pikachuPartner.energyRecoveryPerSecond = 10f;
        if (pikachuPartner.movementScript != null) pikachuPartner.movementScript.UseLocalFallback();
        pikachuPartner.RestoreEnergy();
        DemoPartnerCombat combat = clone.GetComponent<DemoPartnerCombat>();
        if (combat == null) combat = clone.AddComponent<DemoPartnerCombat>();
        combat.SetSpecies(DemoSpecies.Pikachu);
        BasicCombatScript health = clone.GetComponent<BasicCombatScript>();
        if (health == null) health = clone.AddComponent<BasicCombatScript>();
        health.MAX_HEALTH = 100f;
        health.RestoreHealth();
        Sprite[] frames = Resources.LoadAll<Sprite>("Demo/Art/PikachuIdle");
        foreach (Animator animator in clone.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
        SpriteRenderer[] renderers = clone.GetComponentsInChildren<SpriteRenderer>(true);
        if (renderers.Length > 0)
        {
            renderers[0].sortingOrder = 0;
            DemoSpriteAnimator spriteAnimator = renderers[0].gameObject.GetComponent<DemoSpriteAnimator>();
            if (spriteAnimator == null) spriteAnimator = renderers[0].gameObject.AddComponent<DemoSpriteAnimator>();
            spriteAnimator.Configure(renderers[0], frames);
        }
        controller.RegisterPartner(pikachuPartner);
    }

    public void BossDefeated(DemoBattleEnemy defeatedBoss)
    {
        savedBossDefeated = true;
        if (gates != null && gates.Length > 4) gates[4].SetOpen(true);
        flow.MarkBossDefeated();
        flow.SetObjective("Llega al campamento y habla con el guardabosques para terminar la demo.");
        flow.CheckpointReached("campamento");
    }

    public void TalkAtCamp()
    {
        if (!savedBossDefeated)
        {
            ShowHint("El camino al campamento sigue bloqueado. Derrota a Electrode primero.");
            return;
        }
        flow.FinishDemo();
    }

    public PartnerAI GetEnemyTarget()
    {
        if (controller != null && controller.Active is PartnerAI activePartner) return activePartner;
        return machop;
    }

    public void ShowHint(string message)
    {
        if (flow != null) flow.ShowToast(message);
    }

    public bool CanResetCurrentPuzzle()
    {
        if (human == null || rocks.Count == 0) return false;
        Transform currentActor = controller != null && controller.Active != null ? controller.Active.transform : human.transform;
        float x = currentActor.position.x;
        if (x < 9.5f) return !savedPuzzle1;
        if (x < 19.5f) return !savedPuzzle2;
        if (x < 31.5f) return !savedPuzzle3;
        return false;
    }

    public void ResetCurrentPuzzle()
    {
        if (!CanResetCurrentPuzzle()) return;
        Transform currentActor = controller != null && controller.Active != null ? controller.Active.transform : human.transform;
        string id = currentActor.position.x < 9.5f ? "puzzle1" : currentActor.position.x < 19.5f ? "puzzle2" : "puzzle3";
        foreach (DemoPuzzleRock rock in rocks)
            if (rock.PuzzleId == id) rock.ResetPuzzle();
        ShowHint("El puzzle volvió a su estado inicial.");
    }

    public void RestoreCheckpoint()
    {
        if (human == null) return;
        if (controller != null) controller.ReturnControlToHuman();
        PlaceAtCheckpoint(flow.Checkpoint);
        foreach (PartnerAI partner in FindObjectsByType<PartnerAI>())
        {
            partner.RestoreEnergy();
            BasicCombatScript health = partner.GetComponent<BasicCombatScript>();
            if (health != null) health.RestoreHealth();
        }
        if (electrode != null && !savedBossDefeated) electrode.ResetEncounter();
        if (wildPikachu != null && !savedCaptured) wildPikachu.ResetEncounter();
        flow.SetObjective(ObjectiveForCheckpoint(flow.Checkpoint));
        Physics2D.SyncTransforms();
    }

    private void PlaceAtCheckpoint(string checkpoint)
    {
        Vector3 playerPosition;
        switch (checkpoint)
        {
            case "puzzle2": playerPosition = new Vector3(10.5f, 4.5f, human.transform.position.z); break;
            case "puzzle3": playerPosition = new Vector3(20.5f, 4.5f, human.transform.position.z); break;
            case "pikachu": playerPosition = new Vector3(32.5f, 4.5f, human.transform.position.z); break;
            case "jefe": playerPosition = new Vector3(42.5f, 4.5f, human.transform.position.z); break;
            case "campamento":
            case "final": playerPosition = new Vector3(46.5f, 4.5f, human.transform.position.z); break;
            default: playerPosition = new Vector3(1.5f, 4.5f, human.transform.position.z); break;
        }
        if (human.movementScript != null) human.movementScript.Teleport(playerPosition);
        else human.transform.position = playerPosition;
        Vector3 companionPosition = playerPosition + Vector3.right;
        if (machop.movementScript != null) machop.movementScript.Teleport(companionPosition);
        else machop.transform.position = companionPosition;
        machop.master = human.transform;
        if (pikachuPartner != null)
        {
            pikachuPartner.master = human.transform;
            if (pikachuPartner.movementScript != null) pikachuPartner.movementScript.Teleport(playerPosition + Vector3.up);
        }
    }

    private string ObjectiveForCheckpoint(string checkpoint)
    {
        if (!flow.TutorialComplete) return "Habla con el guardabosques para recibir a Machop.";
        if (!savedPuzzle1) return "Controla a Machop y coloca la roca sobre la primera placa.";
        if (!savedPuzzle2) return "Coloca la roca de la segunda cámara sobre la placa.";
        if (!savedPuzzle3) return "Coloca las dos rocas sobre sus placas.";
        if (!savedCaptured) return "Debilita a Pikachu y acércate para capturarlo.";
        if (!savedBossDefeated) return "Derrota a Electrode con Machop o Pikachu.";
        return "Llega al campamento y habla con el guardabosques.";
    }

    private void ConfigureCamera()
    {
        CameraScript cameraScript = FindAnyObjectByType<CameraScript>();
        if (cameraScript != null) cameraScript.SetTarget(human.transform);
        Camera camera = Camera.main;
        if (camera != null)
        {
            camera.orthographic = true;
            camera.orthographicSize = 5.2f;
            camera.transform.position = new Vector3(human.transform.position.x, human.transform.position.y, camera.transform.position.z);
        }
    }

    private void CreateAimIndicator()
    {
        GameObject arrow = new GameObject("Indicador de apuntado");
        arrow.transform.SetParent(transform, false);
        SpriteRenderer arrowRenderer = arrow.AddComponent<SpriteRenderer>();
        arrowRenderer.sprite = DemoPixelArt.Square;
        arrowRenderer.color = new Color(1f, .9f, .4f, .9f);
        arrowRenderer.sortingOrder = 48;
        arrow.transform.localScale = new Vector3(.6f, .1f, 1f);
        aimArrow = arrow.transform;

        GameObject point = new GameObject("Punto de mira");
        point.transform.SetParent(transform, false);
        SpriteRenderer pointRenderer = point.AddComponent<SpriteRenderer>();
        pointRenderer.sprite = DemoPixelArt.Circle;
        pointRenderer.color = new Color(1f, .86f, .28f, .85f);
        pointRenderer.sortingOrder = 49;
        point.transform.localScale = new Vector3(.2f, .2f, 1f);
        aimPoint = point.transform;
    }

    private void CreateDivider(Transform parent, float x)
    {
        for (int y = RoomBottom; y <= RoomTop; y++)
        {
            if (y == 4) continue;
            CreateWall(parent, new Vector3(x, y + .5f, 0f));
        }
    }

    private void CreateWall(Transform parent, Vector3 position)
    {
        GameObject wall = new GameObject("Muro de piedra");
        wall.transform.SetParent(parent, false);
        wall.transform.position = position;
        wall.layer = LayerMask.NameToLayer("Collider");
        wall.transform.localScale = Vector3.one;
        SpriteRenderer renderer = wall.AddComponent<SpriteRenderer>();
        renderer.sprite = LoadRockSprite();
        if (renderer.sprite == null) renderer.sprite = DemoPixelArt.Square;
        renderer.color = Color.white;
        renderer.sortingLayerName = "Terrain";
        renderer.sortingOrder = -10;
        if (Mathf.Abs(position.y - 9.5f) < .01f || Mathf.Abs(position.y + .5f) < .01f)
            renderer.enabled = false;
        BoxCollider2D collider = wall.AddComponent<BoxCollider2D>();
        collider.size = new Vector2(.98f, .98f);
    }

    private DemoCourseGate CreateGate(string name, Vector3 position)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(transform, false);
        obj.transform.position = position;
        obj.layer = LayerMask.NameToLayer("Collider");
        obj.transform.localScale = Vector3.one;
        SpriteRenderer renderer = obj.AddComponent<SpriteRenderer>();
        renderer.sprite = DemoPixelArt.Square;
        renderer.sortingOrder = -8;
        BoxCollider2D collider = obj.AddComponent<BoxCollider2D>();
        collider.size = new Vector2(.95f, .95f);
        DemoCourseGate gate = obj.AddComponent<DemoCourseGate>();
        gate.Configure(renderer, collider);
        return gate;
    }

    private void CreatePlate(string name, Vector3 position)
    {
        AddWorldSprite(transform, name, position + Vector3.back * .01f, new Vector3(.72f, .72f, 1f), new Color(.93f, .72f, .24f), -48);
        AddWorldSprite(transform, name + " · centro", position + Vector3.back * .02f, new Vector3(.4f, .4f, 1f), new Color(.99f, .87f, .38f), -47);
    }

    private void CreateRock(string name, string puzzleId, Vector3 position, Vector3 target)
    {
        GameObject rock = new GameObject(name);
        rock.transform.SetParent(transform, false);
        rock.transform.position = position;
        rock.layer = LayerMask.NameToLayer("Movable");
        rock.transform.localScale = new Vector3(.9f, .9f, 1f);
        SpriteRenderer renderer = rock.AddComponent<SpriteRenderer>();
        renderer.sprite = LoadRockSprite();
        if (renderer.sprite == null) renderer.sprite = DemoPixelArt.Square;
        renderer.color = Color.white;
        renderer.sortingOrder = 2;
        BoxCollider2D collider = rock.AddComponent<BoxCollider2D>();
        collider.size = Vector2.one;
        DemoPuzzleRock puzzleRock = rock.AddComponent<DemoPuzzleRock>();
        puzzleRock.Configure(this, puzzleId, target);
        rocks.Add(puzzleRock);
    }

    private DemoWorldInteractable CreatePerson(string name, Vector3 position, Sprite[] sprites, DemoWorldInteractable.InteractionKind kind)
    {
        GameObject person = new GameObject(name);
        person.transform.SetParent(transform, false);
        person.transform.position = position;
        person.layer = LayerMask.NameToLayer("Characters");
        SpriteRenderer renderer = person.AddComponent<SpriteRenderer>();
        renderer.sprite = FirstFrame(sprites);
        renderer.sortingOrder = 3;
        BoxCollider2D collider = person.AddComponent<BoxCollider2D>();
        collider.size = new Vector2(.8f, .8f);
        DemoWorldInteractable interaction = person.AddComponent<DemoWorldInteractable>();
        interaction.Configure(this, kind);
        return interaction;
    }

    private GameObject CreateEnemyObject(string name, Vector3 position, Sprite sprite, Color color)
    {
        GameObject enemy = new GameObject(name);
        enemy.transform.SetParent(transform, false);
        enemy.transform.position = position;
        enemy.layer = LayerMask.NameToLayer("Enemy");
        SpriteRenderer renderer = enemy.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite != null ? sprite : DemoPixelArt.Square;
        renderer.color = color;
        renderer.sortingOrder = 4;
        CircleCollider2D collider = enemy.AddComponent<CircleCollider2D>();
        collider.radius = .6f;
        return enemy;
    }

    private void AddWorldLabel(Transform parent, string label, Vector3 position)
    {
        GameObject obj = new GameObject("Rótulo · " + label);
        obj.transform.SetParent(parent, false);
        obj.transform.position = position;
        TextMesh text = obj.AddComponent<TextMesh>();
        text.text = label;
        text.anchor = TextAnchor.MiddleCenter;
        text.alignment = TextAlignment.Center;
        text.characterSize = .06f;
        text.fontSize = 44;
        text.color = new Color(.94f, .97f, .77f);
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        MeshRenderer meshRenderer = obj.GetComponent<MeshRenderer>();
        meshRenderer.sortingOrder = -5;
    }

    private void AddWorldSprite(Transform parent, string name, Vector3 position, Vector3 scale, Color color, int order)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        obj.transform.position = position;
        obj.transform.localScale = scale;
        SpriteRenderer renderer = obj.AddComponent<SpriteRenderer>();
        renderer.sprite = DemoPixelArt.Square;
        renderer.color = color;
        renderer.sortingOrder = order;
    }

    private static Sprite FirstFrame(Sprite[] sprites)
    {
        return sprites != null && sprites.Length > 0 ? sprites[0] : null;
    }

    private static Sprite LoadRockSprite()
    {
        Sprite[] sprites = Resources.LoadAll<Sprite>("Demo/Art/MovableRock");
        return FirstFrame(sprites);
    }
}
