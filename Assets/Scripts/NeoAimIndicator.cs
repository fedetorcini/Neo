using UnityEngine;
using UnityEngine.EventSystems;

public sealed class NeoAimIndicator : MonoBehaviour
{
    public Controller controller;
    LineRenderer line;
    Material material;
    void Awake()
    {
        line = new GameObject("Dirección de apuntado").AddComponent<LineRenderer>();
        line.transform.SetParent(transform);
        material = new Material(Shader.Find("Sprites/Default"));
        line.sharedMaterial = material;
        line.startColor = line.endColor = new Color(1,.85f,.25f);
        line.startWidth = line.endWidth = .045f;
        line.positionCount = 5;
        line.sortingOrder = 40;
    }
    void Update()
    {
        bool visible = !DemoBootstrap.IsPaused && controller.Active is PartnerAI && Camera.main != null && (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject());
        line.enabled = visible;
        if (!visible) return;
        Vector3 origin = controller.Active.transform.position;
        Vector3 cursor = Camera.main.ScreenToWorldPoint(Input.mousePosition); cursor.z = origin.z;
        Vector3 direction = (cursor-origin).normalized;
        if (direction.sqrMagnitude < .01f) direction = Vector3.right;
        Vector3 perpendicular = new Vector3(-direction.y,direction.x,0);
        Vector3 tip = origin+direction*1.1f;
        line.SetPosition(0,origin+direction*.65f);
        line.SetPosition(1,tip);
        line.SetPosition(2,tip-direction*.2f+perpendicular*.15f);
        line.SetPosition(3,tip);
        line.SetPosition(4,tip-direction*.2f-perpendicular*.15f);
    }
    void OnDestroy() { if(material != null) Destroy(material); }
}
