using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// A RuleTile whose This / Not This rules also accept other tiles as if they were this one.
/// Mountain lists the stairs carved into it, so the rock runs on up to the stairs instead of
/// closing in rounded ends; PathTile lists them too, so the trail reaches the steps.
/// </summary>
[CreateAssetMenu(menuName = "Neo/Tiles/Rule Tile con amigos", fileName = "NeoFriendRuleTile")]
public class NeoFriendRuleTile : RuleTile
{
    [Tooltip("Tiles que cuentan como este en las reglas This / Not This (por ejemplo, la escalera tallada en la roca).")]
    public TileBase[] friends = new TileBase[0];

    public override bool RuleMatch(int neighbor, TileBase other)
    {
        if (other is RuleOverrideTile overrideTile) other = overrideTile.m_InstanceTile;
        bool same = other == this || (other != null && System.Array.IndexOf(friends, other) >= 0);
        switch (neighbor)
        {
            case TilingRuleOutput.Neighbor.This: return same;
            case TilingRuleOutput.Neighbor.NotThis: return !same;
        }
        return base.RuleMatch(neighbor, other);
    }
}
