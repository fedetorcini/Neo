using UnityEngine;

/// <summary>Lower sprites draw in front: the sorting order follows the world Y of the feet.</summary>
[ExecuteAlways, RequireComponent(typeof(SpriteRenderer))]
public sealed class NeoYSort : MonoBehaviour
{
    [Tooltip("Orden cuando el pie está en Y = 0. Cada casilla hacia arriba resta 10.")]
    public int baseOrder = 5000;
    [Tooltip("Punto que define la profundidad. Vacío: este mismo objeto.")]
    public Transform feet;
    SpriteRenderer sprite;

    void OnEnable() { sprite = GetComponent<SpriteRenderer>(); }
    void LateUpdate()
    {
        float y = (feet != null ? feet : transform).position.y;
        int order = baseOrder - Mathf.RoundToInt(y * 10);
        if (sprite.sortingOrder != order) sprite.sortingOrder = order;
    }
}
