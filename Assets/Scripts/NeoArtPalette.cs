using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Neo/Paleta revisada")]
public sealed class NeoArtPalette : ScriptableObject
{
    [Serializable] public sealed class Entry
    {
        public string label;
        public string state;
        public Sprite sprite;
        public Vector2Int footprint;
    }
    public Entry[] entries;
}
