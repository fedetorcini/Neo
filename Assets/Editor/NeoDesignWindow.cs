using UnityEditor;
using UnityEngine;

public sealed class NeoDesignWindow : EditorWindow
{
    NeoArtPalette palette;
    NeoGridBoard board;
    NeoGridMotor actor;
    Vector2 scroll;
    bool overlay = true;
    bool physics = true;
    [MenuItem("Neo/Diseño Neo")]
    public static void Open() => GetWindow<NeoDesignWindow>("Diseño Neo");
    void OnEnable()
    {
        palette = AssetDatabase.LoadAssetAtPath<NeoArtPalette>("Assets/Demo/NeoPalette.asset");
        SceneView.duringSceneGui += DrawScene;
    }
    void OnDisable() => SceneView.duringSceneGui -= DrawScene;
    void OnInspectorUpdate() { Repaint(); if (overlay) SceneView.RepaintAll(); }
    void OnGUI()
    {
        EditorGUILayout.LabelField("Ocupación, física y rutas", EditorStyles.boldLabel);
        board = (NeoGridBoard)EditorGUILayout.ObjectField("Tablero", board, typeof(NeoGridBoard), true);
        if (board == null && GUILayout.Button("Usar tablero de la escena")) board = FindAnyObjectByType<NeoGridBoard>();
        overlay = EditorGUILayout.Toggle("Mostrar celdas", overlay);
        physics = EditorGUILayout.Toggle("Comparar colliders", physics);
        actor = (NeoGridMotor)EditorGUILayout.ObjectField("Actor / ruta", actor, typeof(NeoGridMotor), true);
        if (board != null) EditorGUILayout.LabelField($"Revisión {board.Revision} · Navegación {(board.Ready ? "lista" : "sin iniciar")}");
        if (actor != null) EditorGUILayout.LabelField($"Destino {actor.TargetCell} · solicitudes {actor.PathRequests}");
        EditorGUILayout.HelpBox("Verde: paso libre. Rojo: bloqueado. Naranja: reserva. Magenta: diferencia con física. Cian: ruta A*. La navegación se inicializa en Play.", MessageType.Info);
        palette = (NeoArtPalette)EditorGUILayout.ObjectField("Paleta revisada", palette, typeof(NeoArtPalette), false);
        scroll = EditorGUILayout.BeginScrollView(scroll);
        if (palette != null && palette.entries != null)
            foreach (var entry in palette.entries)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField(entry.label + " · " + entry.state, EditorStyles.boldLabel);
                if (entry.sprite != null)
                {
                    var rect = GUILayoutUtility.GetRect(90, 100);
                    var sprite = entry.sprite;
                    var uv = new Rect(sprite.rect.x / sprite.texture.width, sprite.rect.y / sprite.texture.height, sprite.rect.width / sprite.texture.width, sprite.rect.height / sprite.texture.height);
                    float width = 80 * sprite.rect.width / sprite.rect.height;
                    GUI.DrawTextureWithTexCoords(new Rect(rect.x, rect.y, width, 80), sprite.texture, uv, true);
                    EditorGUILayout.LabelField($"{sprite.name} · {sprite.rect.width}×{sprite.rect.height}px · PPU {sprite.pixelsPerUnit}");
                    EditorGUILayout.LabelField($"Pivote {sprite.pivot} · huella {entry.footprint}");
                    EditorGUILayout.SelectableLabel(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(sprite)), GUILayout.Height(18));
                    if (GUILayout.Button("Seleccionar recurso")) Selection.activeObject = sprite;
                }
                EditorGUILayout.EndVertical();
            }
        EditorGUILayout.EndScrollView();
    }
    void DrawScene(SceneView view)
    {
        if (!overlay || board == null) return;
        Physics2D.SyncTransforms();
        int mask = LayerMask.GetMask("Collider", "Movable");
        foreach (var c in board.TerrainBounds.allPositionsWithin)
        {
            var cell = new Vector2Int(c.x, c.y);
            var center = NeoGridBoard.Center(cell);
            bool blocked = board.Blocked(cell);
            bool solid = Physics2D.OverlapBox(center, Vector2.one * .6f, 0, mask) != null;
            Handles.color = board.Reserved(cell) ? new Color(1,.6f,0,.55f) : physics && blocked != solid ? new Color(1,0,1,.55f) : blocked ? new Color(1,0,0,.22f) : new Color(0,1,0,.1f);
            Handles.DrawSolidRectangleWithOutline(new Rect(center.x-.48f, center.y-.48f, .96f, .96f), Handles.color, new Color(1,1,1,.12f));
        }
        if (actor != null)
        {
            Handles.color = Color.cyan;
            var previous = actor.transform.position;
            foreach (var point in actor.Route) { Handles.DrawLine(previous, point); previous = point; }
        }
    }
}
