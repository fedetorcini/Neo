using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Builds the demo's presentation and menu without requiring manual scene wiring.</summary>
public sealed class DemoBootstrap : MonoBehaviour
{
    private const string SaveFileName = "neo-demo-save.json";
    private const string OptionsPrefix = "NeoDemo.";
    private static DemoBootstrap instance;
    private Canvas canvas;
    private RectTransform content;
    private Text statusText;
    private Text hudText;
    private Text toastText;
    private Text partnerText;
    private Text energyText;
    private Image energyFill;
    private Image healthFill;
    private Text healthText;
    private Controller controller;
    private DemoAdventureCourse course;
    private NeoFirstRoom room;
    private string currentObjective = "Busca al guardabosques.";
    public int RoomTutorialStep { get; set; }
    public static float InputBlockedUntil { get; private set; }
    private string currentScreen = "";
    private string previousScreen = "menu";
    private string checkpoint = "inicio";
    private bool saveIsInvalid;
    private bool tutorialComplete;
    private bool pikachuCaptured;
    private bool bossDefeated;
    private readonly HashSet<string> completedPuzzles = new HashSet<string>();
    private float toastUntil;
    private float backgroundClock;
    private Image[] backgroundStars;
    private Sprite[] menuPikachuFrames;
    private Image menuPikachu;
    private float menuAnimationClock;
    private int menuFrame;

    public static bool IsPaused { get; private set; } = true;
    public bool TutorialComplete => tutorialComplete;
    public bool PikachuCaptured => pikachuCaptured;
    public bool BossDefeated => bossDefeated;
    public string Checkpoint => checkpoint;

    [Serializable]
    private sealed class DemoSave
    {
        public int version = 3;
        public int roomTutorialStep;
        public string checkpoint = "inicio";
        public bool tutorialComplete;
        public string[] completedPuzzles = Array.Empty<string>();
        public bool pikachuCaptured;
        public bool bossDefeated;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateOnSceneLoad()
    {
        if (FindAnyObjectByType<NeoExplorationScene>() != null) return;
        if (instance != null) return;
        GameObject root = new GameObject("Neo Demo Flow");
        instance = root.AddComponent<DemoBootstrap>();
    }

    internal static void BeginExploration()
    {
        if (instance != null) Destroy(instance.gameObject);
        IsPaused = false;
        InputBlockedUntil = 0;
        Time.timeScale = 1;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
        BuildCanvas();
    }

    private void Start()
    {
        AudioListener.volume = PlayerPrefs.GetFloat(OptionsPrefix + "volume", 1f);
        Screen.fullScreen = PlayerPrefs.GetInt(OptionsPrefix + "fullscreen", 1) == 1;
        if (File.Exists(SavePath)) TryReadSave(out _);
        SetPaused(true);
        SetControllerEnabled(false);
        StartCoroutine(ShowSplash());
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (currentScreen == "game") ShowPause();
            else if (currentScreen == "pause") Resume();
            else if (currentScreen == "controls" || currentScreen == "options") ReturnFromSubscreen();
        }

        if (currentScreen == "game") RefreshHud();
        if (toastText != null && Time.unscaledTime >= toastUntil) toastText.text = "";
        if (currentScreen == "menu" && backgroundStars != null)
        {
            backgroundClock += Time.unscaledDeltaTime;
            for (int i = 0; i < backgroundStars.Length; i++)
            {
                RectTransform star = backgroundStars[i].rectTransform;
                Vector2 position = star.anchoredPosition;
                position.y += (12f + i * 4f) * Time.unscaledDeltaTime;
                if (position.y > 550f) position.y = -550f;
                star.anchoredPosition = position;
                backgroundStars[i].color = Color.Lerp(new Color(.45f, .8f, .95f, .45f), Color.white, Mathf.PingPong(backgroundClock + i, 1f));
            }
            menuAnimationClock += Time.unscaledDeltaTime;
            if (menuPikachu != null && menuPikachuFrames != null && menuPikachuFrames.Length > 1 && menuAnimationClock >= .24f)
            {
                menuAnimationClock = 0f;
                menuFrame = (menuFrame + 1) % menuPikachuFrames.Length;
                menuPikachu.sprite = menuPikachuFrames[menuFrame];
            }
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (FindAnyObjectByType<NeoExplorationScene>() != null)
        {
            BeginExploration();
            return;
        }
        StartCoroutine(RefreshSceneBindings());
    }

    private IEnumerator RefreshSceneBindings()
    {
        yield return null;
        controller = FindAnyObjectByType<Controller>();
        if (controller != null)
        {
            controller.ActiveChanged -= OnActiveChanged;
            controller.ActiveChanged += OnActiveChanged;
        }
        if (currentScreen == "loading") StartCoroutine(PrepareGameplay());
        else if (currentScreen == "game") SetControllerEnabled(true);
    }

    private IEnumerator PrepareGameplay()
    {
        yield return null;
        controller = FindAnyObjectByType<Controller>();
        if (controller == null)
        {
            ShowScreen("error");
            AddText(content, "No se encontró el controlador del nivel.", 28, new Vector2(0, 40), new Vector2(900, 100), Color.white);
            AddButton(content, "VOLVER AL MENÚ", new Vector2(0, -80), ShowMainMenu);
            yield break;
        }

        controller.ActiveChanged -= OnActiveChanged;
        controller.ActiveChanged += OnActiveChanged;
        room = controller.GetComponent<NeoFirstRoom>();
        if (room != null)
        {
            course = null;
            ShowGameHud();
            room.Initialize(this);
            SetControllerEnabled(true);
            SetPaused(false);
            yield break;
        }
        course = controller.GetComponent<DemoAdventureCourse>();
        if (course == null) course = controller.gameObject.AddComponent<DemoAdventureCourse>();
        course.Initialize(this, controller);
        ShowGameHud();
        if (checkpoint == "final")
        {
            FinishDemo();
            yield break;
        }
        SetControllerEnabled(true);
        SetPaused(false);
        if (!tutorialComplete) SetObjective("Habla con el guardabosques para recibir a Machop.");
        else SetObjective(ObjectiveForSavedProgress());
    }

    private IEnumerator ShowSplash()
    {
        ShowScreen("splash");
        AddText(content, "NEO", 82, new Vector2(0, 35), new Vector2(800, 130), new Color(.95f, .8f, .22f));
        AddText(content, "UNA AVENTURA POKÉMON", 24, new Vector2(0, -55), new Vector2(800, 60), Color.white);
        AddText(content, "Pulsa una tecla o haz clic para continuar", 20, new Vector2(0, -210), new Vector2(800, 50), new Color(.76f, .88f, .92f));
        float end = Time.unscaledTime + 2f;
        while (Time.unscaledTime < end && !Input.anyKeyDown && !Input.GetMouseButtonDown(0)) yield return null;
        if (currentScreen == "splash") ShowMainMenu();
    }

    private void BuildCanvas()
    {
        GameObject canvasObject = new GameObject("Demo Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);
        canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0f;

        if (FindAnyObjectByType<EventSystem>() == null)
        {
            GameObject events = new GameObject("Demo Event System", typeof(EventSystem), typeof(StandaloneInputModule));
            events.transform.SetParent(transform, false);
        }
    }

    private void ShowScreen(string name)
    {
        currentScreen = name;
        if (content != null) Destroy(content.gameObject);
        GameObject root = new GameObject(name + " Screen", typeof(RectTransform), typeof(Image));
        root.transform.SetParent(canvas.transform, false);
        content = root.GetComponent<RectTransform>();
        content.anchorMin = Vector2.zero;
        content.anchorMax = Vector2.one;
        content.offsetMin = Vector2.zero;
        content.offsetMax = Vector2.zero;
        Image background = root.GetComponent<Image>();
        background.color = name == "game" ? new Color(0, 0, 0, 0) : new Color(.035f, .09f, .13f, name == "menu" ? .78f : .92f);
        background.raycastTarget = name != "game";
        backgroundStars = null;
        menuPikachu = null;
        menuPikachuFrames = null;
        if (name == "menu") AddAnimatedBackdrop();
    }

    private void AddAnimatedBackdrop()
    {
        backgroundStars = new Image[18];
        for (int i = 0; i < backgroundStars.Length; i++)
        {
            Image star = AddPanel(content, new Vector2((i % 6 - 2.5f) * 310f, -400f + (i / 6) * 350f), new Vector2(5f, 16f), new Color(.45f, .8f, .95f, .45f));
            star.raycastTarget = false;
            backgroundStars[i] = star;
        }
        AddText(content, "◆", 175, new Vector2(-600, -180), new Vector2(220, 220), new Color(.25f, .62f, .47f, .72f));
        AddText(content, "●", 128, new Vector2(610, 160), new Vector2(180, 180), new Color(.95f, .55f, .19f, .72f));
        menuPikachuFrames = Resources.LoadAll<Sprite>("Demo/Art/PikachuIdle");
        if (menuPikachuFrames.Length > 0)
        {
            menuPikachu = AddSpriteImage(content, menuPikachuFrames[0], new Vector2(665f, -185f), new Vector2(250f, 250f));
            menuPikachu.color = new Color(1f, 1f, 1f, .9f);
        }
        Sprite[] rangerFrames = Resources.LoadAll<Sprite>("Demo/Art/Ranger");
        if (rangerFrames.Length > 0)
        {
            Image ranger = AddSpriteImage(content, rangerFrames[0], new Vector2(-660f, -175f), new Vector2(230f, 260f));
            ranger.color = new Color(1f, 1f, 1f, .88f);
        }
    }

    private void ShowMainMenu()
    {
        SetPaused(true);
        SetControllerEnabled(false);
        ShowScreen("menu");
        AddText(content, "NEO", 90, new Vector2(0, 330), new Vector2(700, 120), new Color(.95f, .8f, .22f));
        AddText(content, "La aventura comienza en el paso de la montaña", 23, new Vector2(0, 250), new Vector2(900, 60), Color.white);
        Button playButton = AddButton(content, "JUGAR", new Vector2(0, 110), () =>
        {
            if (File.Exists(SavePath)) ShowNewGameConfirmation();
            else StartNewGame();
        });
        Button continueButton = AddButton(content, "CONTINUAR", new Vector2(0, 25), ContinueGame);
        continueButton.interactable = HasSave() && !saveIsInvalid;
        AddButton(content, "OPCIONES", new Vector2(0, -60), ShowOptions);
        AddButton(content, "SALIR", new Vector2(0, -145), () => Application.Quit());
        statusText = AddText(content, saveIsInvalid ? "Guardado incompatible o dañado. Se conserva; Jugar permite reemplazarlo." : "WASD Mover   ·   ESPACIO Cambiar   ·   E Interactuar", 17, new Vector2(0, -285), new Vector2(1100, 60), new Color(.76f, .88f, .92f));
        EventSystem.current?.SetSelectedGameObject(playButton.gameObject);
    }

    private void ShowNewGameConfirmation()
    {
        ShowScreen("new game confirmation");
        AddText(content, "¿Empezar una partida nueva?", 34, new Vector2(0, 95), new Vector2(900, 100), Color.white);
        AddText(content, saveIsInvalid ? "El guardado existente no se pudo leer. ¿Quieres reemplazarlo?" : "Se reemplazará el progreso guardado.", 21, new Vector2(0, 20), new Vector2(900, 60), new Color(.8f, .85f, .88f));
        AddButton(content, "SÍ, EMPEZAR", new Vector2(0, -80), StartNewGame);
        AddButton(content, "VOLVER", new Vector2(0, -165), ShowMainMenu);
    }

    private void StartNewGame()
    {
        checkpoint = "inicio";
        RoomTutorialStep = 0;
        saveIsInvalid = false;
        tutorialComplete = false;
        pikachuCaptured = false;
        bossDefeated = false;
        completedPuzzles.Clear();
        WriteSave(false);
        LoadGameplay();
    }

    private void ContinueGame()
    {
        if (!TryReadSave(out DemoSave save))
        {
            saveIsInvalid = true;
            ShowMainMenu();
            return;
        }
        ApplySave(save);
        LoadGameplay();
    }

    private void LoadGameplay()
    {
        SetPaused(true);
        SetControllerEnabled(false);
        ShowScreen("loading");
        AddText(content, "Cargando la aventura…", 30, Vector2.zero, new Vector2(700, 100), Color.white);
        StartCoroutine(LoadGameplayScene());
    }

    private IEnumerator LoadGameplayScene()
    {
        yield return null;
        if (Application.CanStreamedLevelBeLoaded("DemoLevel1"))
        {
            AsyncOperation operation = SceneManager.LoadSceneAsync("DemoLevel1");
            while (operation != null && !operation.isDone) yield return null;
            yield break;
        }

        string scenePath = SceneManager.GetActiveScene().path;
        if (string.IsNullOrEmpty(scenePath))
        {
            StartCoroutine(PrepareGameplay());
            yield break;
        }
        AsyncOperation fallbackOperation = SceneManager.LoadSceneAsync(scenePath);
        while (fallbackOperation != null && !fallbackOperation.isDone) yield return null;
    }

    private void ApplySave(DemoSave save)
    {
        checkpoint = save.checkpoint;
        RoomTutorialStep = save.roomTutorialStep;
        tutorialComplete = save.tutorialComplete;
        pikachuCaptured = save.pikachuCaptured;
        bossDefeated = save.bossDefeated;
        completedPuzzles.Clear();
        if (save.completedPuzzles != null)
            foreach (string id in save.completedPuzzles)
                if (!string.IsNullOrWhiteSpace(id)) completedPuzzles.Add(id);
    }

    private IEnumerator ShowDialogue(string[] lines, Action completed)
    {
        ShowScreen("dialogue");
        SetPaused(true);
        Text speaker = AddText(content, "GUARDABOSQUES", 21, new Vector2(0, 175), new Vector2(900, 60), new Color(.98f, .79f, .31f));
        Text line = AddText(content, "", 27, new Vector2(0, 55), new Vector2(1250, 170), Color.white);
        AddText(content, "E o clic para continuar", 17, new Vector2(0, -140), new Vector2(700, 55), new Color(.76f, .88f, .92f));
        foreach (string message in lines)
        {
            line.text = message;
            yield return null;
            while (Input.GetKey(KeyCode.E) || Input.GetKey(KeyCode.Space) || Input.GetMouseButton(0)) yield return null;
            bool advanced = false;
            while (!advanced)
            {
                if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0)) advanced = true;
                yield return null;
            }
            yield return null;
        }
        completed?.Invoke();
    }

    private void ShowGameHud()
    {
        SetPaused(false);
        ShowScreen("game");
        AddPanel(content, new Vector2(-510, 430), new Vector2(440, 200), new Color(.025f, .055f, .075f, .94f));
        AddPanel(content, new Vector2(450, 480), new Vector2(900, 85), new Color(.025f, .055f, .075f, .94f));
        AddPanel(content, new Vector2(0, -465), new Vector2(1500, 125), new Color(.025f, .055f, .075f, .94f));
        hudText = AddText(content, "OBJETIVO  ·  " + currentObjective, 24, new Vector2(0, -435), new Vector2(1350, 60), Color.white);
        healthText = AddText(content, "VIDA", 19, new Vector2(-510, 495), new Vector2(360, 35), new Color(.98f, .83f, .75f));
        healthFill = AddPanel(content, new Vector2(-690, 465), new Vector2(360, 18), new Color(.92f, .25f, .22f));
        healthFill.rectTransform.pivot = new Vector2(0, .5f);
        partnerText = AddText(content, "COMPAÑERO  MACHOP", 19, new Vector2(-510, 425), new Vector2(360, 35), new Color(.95f, .88f, .65f));
        energyText = AddText(content, "ENERGÍA", 19, new Vector2(-510, 395), new Vector2(360, 35), new Color(.95f, .88f, .65f));
        energyFill = AddPanel(content, new Vector2(-690, 365), new Vector2(360, 18), new Color(.3f, .87f, .39f));
        energyFill.rectTransform.pivot = new Vector2(0, .5f);
        AddText(content, "WASD Mover  ·  ESPACIO Controlar  ·  1 / 2 Equipo\nClic Atacar  ·  E Interactuar  ·  ESC Pausa", 24, new Vector2(450, 480), new Vector2(900, 76), new Color(.85f, .9f, .9f));
        toastText = AddText(content, "", 20, new Vector2(0, -330), new Vector2(1280, 60), new Color(1f, .88f, .45f));
        RefreshHud();
        room?.RefreshObjective();
        Canvas.ForceUpdateCanvases();
    }

    private void RefreshHud()
    {
        if (energyText == null || energyFill == null) return;
        if (controller == null) controller = FindAnyObjectByType<Controller>();
        PartnerAI partner = FindDisplayPartner();
        if (partner == null)
        {
            partnerText.text = "CONTROLANDO  HUMANO";
            energyText.text = "Habla con el guardabosques";
            healthText.text = "EQUIPO SIN COMPAÑERO";
            energyFill.rectTransform.sizeDelta = Vector2.zero;
            healthFill.rectTransform.sizeDelta = Vector2.zero;
            return;
        }
        float ratio = partner.MAX_TIRERNESS <= 0f ? 0f : Mathf.Clamp01(partner.GetTirerness() / partner.MAX_TIRERNESS);
        energyFill.rectTransform.sizeDelta = new Vector2(360f * ratio, 18f);
        energyFill.color = ratio < .25f ? new Color(.95f, .34f, .24f) : new Color(.3f, .87f, .39f);
        DemoPartnerCombat combat = partner.GetComponent<DemoPartnerCombat>();
        string name = combat != null ? combat.DisplayName.ToUpperInvariant() : partner.name.ToUpperInvariant();
        bool controlled = controller != null && controller.Active == partner;
        partnerText.text = (controlled ? "CONTROLANDO  " : "COMPAÑERO  ") + name;
        energyText.text = "ENERGÍA  " + Mathf.CeilToInt(partner.GetTirerness()) + "%";
        BasicCombatScript health = partner.GetComponent<BasicCombatScript>();
        if (health != null)
        {
            healthFill.rectTransform.sizeDelta = new Vector2(360f * Mathf.Clamp01(health.CurrentHealth / health.MaximumHealth), 18f);
            healthText.text = "VIDA  " + Mathf.CeilToInt(health.CurrentHealth) + "%";
        }
    }

    private PartnerAI FindDisplayPartner()
    {
        if (controller != null && controller.Active is PartnerAI activePartner) return activePartner;
        PartnerAI[] partners = FindObjectsByType<PartnerAI>();
        foreach (PartnerAI partner in partners)
        {
            DemoPartnerCombat combat = partner.GetComponent<DemoPartnerCombat>();
            if (combat == null || combat.Species == DemoSpecies.Machop) return partner;
        }
        return partners.Length > 0 ? partners[0] : null;
    }

    private void ShowPause()
    {
        SetPaused(true);
        ShowScreen("pause");
        AddText(content, "PAUSA", 48, new Vector2(0, 245), new Vector2(800, 100), new Color(.98f, .79f, .31f));
        AddButton(content, "REANUDAR", new Vector2(0, 140), Resume);
        AddButton(content, "CONTROLES", new Vector2(0, 55), ShowControls);
        AddButton(content, "OPCIONES", new Vector2(0, -30), ShowOptions);
        Button reset = AddButton(content, "REINICIAR PUZZLE", new Vector2(0, -115), ResetPuzzleAndResume);
        reset.interactable = room != null || (course != null && course.CanResetCurrentPuzzle());
        AddButton(content, "VOLVER AL MENÚ", new Vector2(0, -200), ShowMainMenu);
    }

    private void ResetPuzzleAndResume()
    {
        if (room != null) room.ResetRoom();
        else if (course != null) course.ResetCurrentPuzzle();
        Resume();
    }

    private void ShowControls()
    {
        previousScreen = currentScreen;
        ShowScreen("controls");
        AddText(content, "CONTROLES", 42, new Vector2(0, 215), new Vector2(800, 90), Color.white);
        AddText(content, "WASD   Moverse\nESPACIO   Controlar / llamar al compañero\nClic izquierdo   Atacar hacia el cursor\nE   Interactuar\n1 / 2   Elegir compañero\nESC   Pausa", 24, new Vector2(0, 20), new Vector2(850, 320), new Color(.88f, .93f, .93f));
        AddButton(content, "VOLVER", new Vector2(0, -210), ReturnFromSubscreen);
    }

    private void ShowOptions()
    {
        previousScreen = currentScreen;
        ShowScreen("options");
        AddText(content, "OPCIONES", 42, new Vector2(0, 220), new Vector2(800, 90), Color.white);
        AddText(content, "Volumen general", 23, new Vector2(-250, 90), new Vector2(400, 60), Color.white);
        Slider volume = AddSlider(content, new Vector2(150, 90));
        volume.value = PlayerPrefs.GetFloat(OptionsPrefix + "volume", 1f);
        volume.onValueChanged.AddListener(value => { AudioListener.volume = value; PlayerPrefs.SetFloat(OptionsPrefix + "volume", value); });
        Toggle fullscreen = AddToggle(content, "Pantalla completa", new Vector2(0, -15));
        fullscreen.isOn = PlayerPrefs.GetInt(OptionsPrefix + "fullscreen", 1) == 1;
        fullscreen.onValueChanged.AddListener(value =>
        {
            Screen.fullScreen = value;
            PlayerPrefs.SetInt(OptionsPrefix + "fullscreen", value ? 1 : 0);
        });
        AddButton(content, "VOLVER", new Vector2(0, -145), ReturnFromSubscreen);
    }

    private void ReturnFromSubscreen()
    {
        if (previousScreen == "pause") ShowPause();
        else ShowMainMenu();
    }

    private void Resume()
    {
        InputBlockedUntil = Time.unscaledTime + .2f;
        ShowGameHud();
        SetControllerEnabled(true);
        SetPaused(false);
    }

    public void CheckpointReached(string id)
    {
        checkpoint = string.IsNullOrWhiteSpace(id) ? "checkpoint" : id;
        WriteSave(tutorialComplete);
        ShowToast("Punto de control guardado.");
    }

    public bool IsPuzzleComplete(string id) => completedPuzzles.Contains(id);

    public void MarkPuzzleComplete(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return;
        completedPuzzles.Add(id);
        WriteSave(tutorialComplete);
    }

    public void MarkPikachuCaptured()
    {
        pikachuCaptured = true;
        WriteSave(tutorialComplete);
    }

    public void MarkBossDefeated()
    {
        bossDefeated = true;
        WriteSave(tutorialComplete);
    }

    public void StartRangerDialogue()
    {
        if (currentScreen != "game" || tutorialComplete) return;
        SetPaused(true);
        SetControllerEnabled(false);
        string[] lines = {
            "Un desprendimiento bloqueó el paso al campamento. Al otro lado, un Electrode alterado mantiene peligrosa la zona.",
            "Te presto a Machop. Controlalo directamente para mover las rocas y despejar el sendero.",
            "Usa WASD para caminar y ESPACIO para alternar entre tu personaje y Machop. El personaje queda a salvo mientras lo controlas.",
            "La energía de Machop baja más rápido cuanto más lejos esté de ti. Al llegar a cero vuelve; recupera energía cerca tuyo y podrás elegirlo otra vez al 25 %.",
            "En combate, apunta con el mouse y haz clic para atacar. Pulsa E para interactuar con objetos y personas."
        };
        StartCoroutine(ShowDialogue(lines, () =>
        {
            tutorialComplete = true;
            saveIsInvalid = false;
            WriteSave(true);
            ShowGameHud();
            SetControllerEnabled(true);
            SetPaused(false);
            if (course != null) course.TutorialFinished();
        }));
    }

    public void ReceiveRoomCompanion(int step)
    {
        tutorialComplete = true;
        RoomTutorialStep = step;
        CheckpointReached("machop_recibido");
    }

    public void ShowRoomDialogue(string[] lines, Action completed)
    {
        SetControllerEnabled(false);
        StartCoroutine(ShowDialogue(lines, () => {
            ShowGameHud();
            completed?.Invoke();
            InputBlockedUntil = Time.unscaledTime + .2f;
            SetControllerEnabled(true);
            SetPaused(false);
        }));
    }

    public void ShowToast(string message)
    {
        if (toastText == null || currentScreen != "game") return;
        toastText.text = message;
        toastUntil = Time.unscaledTime + 2.6f;
    }

    public void ShowDefeat()
    {
        if (currentScreen != "game") return;
        SetPaused(true);
        SetControllerEnabled(false);
        ShowScreen("defeat");
        AddText(content, "MACHOP NECESITA DESCANSAR", 42, new Vector2(0, 145), new Vector2(1200, 90), new Color(.98f, .79f, .31f));
        AddText(content, "El punto de control conserva el avance de la aventura.", 22, new Vector2(0, 60), new Vector2(1000, 60), Color.white);
        AddButton(content, "REINTENTAR", new Vector2(0, -35), RestartAtCheckpoint);
        AddButton(content, "MENÚ PRINCIPAL", new Vector2(0, -120), ShowMainMenu);
    }

    private void RestartAtCheckpoint()
    {
        if (room != null) room.ResetRoom();
        else if (course != null) course.RestoreCheckpoint();
        ShowGameHud();
        SetControllerEnabled(true);
        SetPaused(false);
    }

    public void SetObjective(string objective)
    {
        currentObjective = objective;
        if (hudText != null) hudText.text = "OBJETIVO  ·  " + objective;
    }

    public void FinishDemo()
    {
        CheckpointReached("final");
        SetPaused(true);
        ShowScreen("ending");
        AddText(content, "¡CAMINO DESPEJADO!", 48, new Vector2(0, 200), new Vector2(1100, 100), new Color(.98f, .79f, .31f));
        AddText(content, "El campamento vuelve a estar a salvo. Gracias por jugar la demo.", 24, new Vector2(0, 100), new Vector2(1200, 75), Color.white);
        AddButton(content, "VOLVER A JUGAR", new Vector2(0, -15), StartNewGame);
        AddButton(content, "MENÚ PRINCIPAL", new Vector2(0, -100), ShowMainMenu);
    }

    private void WriteSave(bool tutorialComplete)
    {
        if (tutorialComplete) this.tutorialComplete = true;
        DemoSave save = new DemoSave
        {
            checkpoint = checkpoint,
            roomTutorialStep = RoomTutorialStep,
            tutorialComplete = this.tutorialComplete,
            completedPuzzles = new List<string>(completedPuzzles).ToArray(),
            pikachuCaptured = pikachuCaptured,
            bossDefeated = bossDefeated
        };
        string path = SavePath;
        string temporary = path + ".tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(temporary, JsonUtility.ToJson(save, true));
            if (File.Exists(path)) File.Replace(temporary, path, null);
            else File.Move(temporary, path);
            saveIsInvalid = false;
        }
        catch (Exception exception)
        {
            Debug.LogError("No se pudo guardar la partida de la demo: " + exception.Message);
        }
    }

    private bool TryReadSave(out DemoSave save)
    {
        save = null;
        if (saveIsInvalid || !File.Exists(SavePath)) return false;
        try
        {
            save = JsonUtility.FromJson<DemoSave>(File.ReadAllText(SavePath));
            if (save == null || save.version != 3 || string.IsNullOrWhiteSpace(save.checkpoint)) throw new InvalidDataException("El guardado anterior no es compatible con este mapa o está dañado.");
            if (save.checkpoint != "inicio" && save.checkpoint != "machop_recibido" && save.checkpoint != "paso01_salida") throw new InvalidDataException("Punto de control incompatible con este mapa.");
            if (save.completedPuzzles == null) save.completedPuzzles = Array.Empty<string>();
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogWarning("El guardado de la demo no se pudo leer; se conserva para revisión. " + exception.Message);
            saveIsInvalid = true;
            return false;
        }
    }

    private bool HasSave() => File.Exists(SavePath) && !saveIsInvalid;
    private static string SavePath => Environment.GetEnvironmentVariable("NEO_DEMO_SAVE_PATH") ?? Path.Combine(Application.persistentDataPath, SaveFileName);

    private void SetPaused(bool paused)
    {
        IsPaused = paused;
        Time.timeScale = paused ? 0f : 1f;
    }

    private void SetControllerEnabled(bool enabled)
    {
        if (controller == null) controller = FindAnyObjectByType<Controller>();
        if (controller == null) return;
        controller.enabled = enabled;
        if (enabled) OnActiveChanged(controller.Active);
    }

    private string ObjectiveForSavedProgress()
    {
        if (!completedPuzzles.Contains("puzzle1")) return "Controla a Machop y coloca la primera roca sobre la placa.";
        if (!completedPuzzles.Contains("puzzle2")) return "Coloca la roca de la segunda cámara sobre la placa.";
        if (!completedPuzzles.Contains("puzzle3")) return "Coloca las dos rocas sobre sus placas.";
        if (!pikachuCaptured) return "Debilita a Pikachu y acércate para capturarlo.";
        if (!bossDefeated) return "Derrota a Electrode con Machop o Pikachu.";
        return "Llega al campamento y habla con el guardabosques.";
    }

    private void OnActiveChanged(Controllable active)
    {
        if (energyText != null) RefreshHud();
    }

    private Text AddText(Transform parent, string value, int size, Vector2 position, Vector2 dimensions, Color color)
    {
        GameObject obj = new GameObject("Text", typeof(RectTransform), typeof(Text));
        obj.transform.SetParent(parent, false);
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(.5f, .5f);
        rect.anchorMax = new Vector2(.5f, .5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = dimensions;
        Text text = obj.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.text = value;
        text.fontSize = size;
        text.color = color;
        text.alignment = TextAnchor.MiddleCenter;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        return text;
    }

    private Image AddPanel(Transform parent, Vector2 position, Vector2 dimensions, Color color)
    {
        GameObject obj = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        obj.transform.SetParent(parent, false);
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(.5f, .5f);
        rect.anchorMax = new Vector2(.5f, .5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = dimensions;
        Image image = obj.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private Button AddButton(Transform parent, string label, Vector2 position, Action onClick)
    {
        GameObject obj = new GameObject(label + " Button", typeof(RectTransform), typeof(Image), typeof(Button));
        obj.transform.SetParent(parent, false);
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(.5f, .5f);
        rect.anchorMax = new Vector2(.5f, .5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(430, 68);
        Image image = obj.GetComponent<Image>();
        image.color = new Color(.09f, .2f, .25f, .98f);
        Button button = obj.GetComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = new Color(.09f, .2f, .25f, .98f);
        colors.highlightedColor = new Color(.18f, .38f, .39f, 1f);
        colors.selectedColor = colors.highlightedColor;
        colors.pressedColor = new Color(.91f, .64f, .22f, 1f);
        button.colors = colors;
        button.targetGraphic = image;
        button.onClick.AddListener(() => onClick());
        Text text = AddText(obj.transform, label, 22, Vector2.zero, new Vector2(410, 62), Color.white);
        text.fontStyle = FontStyle.Bold;
        Navigation navigation = button.navigation;
        navigation.mode = Navigation.Mode.Automatic;
        button.navigation = navigation;
        if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == null)
            EventSystem.current.SetSelectedGameObject(obj);
        return button;
    }

    private Image AddSpriteImage(Transform parent, Sprite sprite, Vector2 position, Vector2 dimensions)
    {
        GameObject obj = new GameObject("Artwork", typeof(RectTransform), typeof(Image));
        obj.transform.SetParent(parent, false);
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(.5f, .5f);
        rect.anchorMax = new Vector2(.5f, .5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = dimensions;
        Image image = obj.GetComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = true;
        image.raycastTarget = false;
        return image;
    }

    private Slider AddSlider(Transform parent, Vector2 position)
    {
        GameObject obj = new GameObject("Volume Slider", typeof(RectTransform), typeof(Slider));
        obj.transform.SetParent(parent, false);
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(.5f, .5f); rect.anchorMax = new Vector2(.5f, .5f);
        rect.anchoredPosition = position; rect.sizeDelta = new Vector2(420, 40);
        Image track = AddPanel(obj.transform, Vector2.zero, new Vector2(420, 14), new Color(.2f, .3f, .32f));
        RectTransform fillArea = new GameObject("Fill Area", typeof(RectTransform)).GetComponent<RectTransform>();
        fillArea.SetParent(obj.transform, false); fillArea.anchorMin = new Vector2(0, .25f); fillArea.anchorMax = new Vector2(1, .75f); fillArea.offsetMin = new Vector2(5, 0); fillArea.offsetMax = new Vector2(-5, 0);
        Image fill = new GameObject("Fill", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        fill.transform.SetParent(fillArea, false); fill.color = new Color(.35f, .83f, .45f);
        RectTransform fillRect = fill.rectTransform; fillRect.anchorMin = Vector2.zero; fillRect.anchorMax = Vector2.one; fillRect.offsetMin = fillRect.offsetMax = Vector2.zero;
        RectTransform handle = new GameObject("Handle", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
        handle.SetParent(obj.transform, false); handle.sizeDelta = new Vector2(28, 34); handle.GetComponent<Image>().color = new Color(.98f, .79f, .31f);
        Slider slider = obj.GetComponent<Slider>(); slider.fillRect = fillRect; slider.handleRect = handle; slider.targetGraphic = handle.GetComponent<Image>();
        slider.minValue = 0; slider.maxValue = 1; slider.wholeNumbers = false; slider.direction = Slider.Direction.LeftToRight;
        return slider;
    }

    private Toggle AddToggle(Transform parent, string label, Vector2 position)
    {
        GameObject obj = new GameObject("Fullscreen Toggle", typeof(RectTransform), typeof(Toggle));
        obj.transform.SetParent(parent, false);
        RectTransform rect = obj.GetComponent<RectTransform>(); rect.anchorMin = new Vector2(.5f, .5f); rect.anchorMax = new Vector2(.5f, .5f); rect.anchoredPosition = position; rect.sizeDelta = new Vector2(460, 56);
        Image background = new GameObject("Box", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        background.transform.SetParent(obj.transform, false); background.rectTransform.anchorMin = new Vector2(0, .5f); background.rectTransform.anchorMax = new Vector2(0, .5f); background.rectTransform.sizeDelta = new Vector2(35, 35); background.color = new Color(.2f, .3f, .32f);
        Image check = new GameObject("Check", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        check.transform.SetParent(background.transform, false); check.rectTransform.anchorMin = Vector2.zero; check.rectTransform.anchorMax = Vector2.one; check.rectTransform.offsetMin = new Vector2(6, 6); check.rectTransform.offsetMax = new Vector2(-6, -6); check.color = new Color(.35f, .83f, .45f);
        Text text = AddText(obj.transform, label, 22, new Vector2(125, 0), new Vector2(300, 45), Color.white); text.alignment = TextAnchor.MiddleLeft;
        Toggle toggle = obj.GetComponent<Toggle>(); toggle.targetGraphic = background; toggle.graphic = check;
        return toggle;
    }
}
