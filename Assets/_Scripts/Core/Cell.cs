using UnityEngine;

public class Cell
{
    public Vector3 WorldPosition { get; private set; }
    public TerrainType Terrain { get; private set; }
    public NetworkUnit OccupyingUnit { get; set; }

    public Cell(Vector3 position, TerrainType terrain)
    {
        WorldPosition = position;
        Terrain = terrain;
        OccupyingUnit = null;
    }
}
