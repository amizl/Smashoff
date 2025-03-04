using UnityEngine;
using System.Collections.Generic;
using Unity.Netcode;
using System;

public class GridManager : NetworkBehaviour
{
    private NetworkList<int> syncedGrid;
    private NetworkVariable<Vector3> networkOrigin = new NetworkVariable<Vector3>(Vector3.zero); // Synced origin

    private Vector2Int selectedCell = new Vector2Int(-1, -1); // Default: No cell selected
    public GameObject cellPrefab;
    public GameObject CellsDirectory;
    public Sprite[] normalTiles;  // Assign tiles_0 to tiles_9
    public Sprite attackTile;
    public Sprite defenseTile;
    public Sprite healingTile;
    public Sprite resourceTile;
    public static GridManager Instance { get; private set; }

    public int rows = 5;
    public int columns = 8;
    public float cellSize = 1f;

    // Use networkOrigin instead of a public Vector3 origin
    private Vector3 origin;  // Local reference, updated from networkOrigin

    private Cell[,] grid;

    private void Awake()
    {
        syncedGrid = new NetworkList<int>();

        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    [ServerRpc(RequireOwnership = false)]
    private void SyncGridWithClientsServerRpc()
    {
        syncedGrid.Clear();
        for (int x = 0; x < columns; x++)
        {
            for (int y = 0; y < rows; y++)
            {
                syncedGrid.Add((int)grid[x, y].Terrain); // Convert enum to int
            }
        }
    }

    private void InitializeGrid()
    {
        grid = new Cell[columns, rows];

        float gridWidth = columns * cellSize;
        float gridHeight = rows * cellSize;
        Vector3 startPosition = new Vector3(
            -gridWidth / 2 + cellSize / 2,
            -gridHeight / 2 + cellSize / 2,
            0
        );

        // Set the network origin on the server
        networkOrigin.Value = startPosition;
        origin = networkOrigin.Value;  // Update local origin

        for (int x = 0; x < columns; x++)
        {
            for (int y = 0; y < rows; y++)
            {
                Vector3 position = origin + new Vector3(x * cellSize, y * cellSize, 0);
                GameObject cellObj = Instantiate(cellPrefab, position, Quaternion.identity, CellsDirectory.transform);
                cellObj.transform.position = position;  // Ensure exact positioning
                Cell cellComponent = cellObj.GetComponent<Cell>();
                TerrainType terrain = GenerateRandomTerrain();
                cellComponent.Initialize(x, y, terrain);
                cellComponent.SetTileSprite(GetTileSpriteFromTerrain(terrain));
                grid[x, y] = cellComponent;
            }
        }
    }

    private TerrainType GenerateRandomTerrain()
    {
        float random = UnityEngine.Random.value;
        if (random < 0.1f) return TerrainType.DefenseBonus;
        if (random < 0.2f) return TerrainType.AttackBonus;
        if (random < 0.3f) return TerrainType.ResourceGen;
        if (random < 0.4f) return TerrainType.Healing;
        return TerrainType.Normal;
    }

    public Vector3 GetWorldPosition(int x, int y)
    {
        return origin + new Vector3(x * cellSize, y * cellSize, 0);
    }

    public bool IsValidPosition(int x, int y)
    {
        return x >= 0 && x < columns && y >= 0 && y < rows;
    }

    public Cell GetCell(int col, int row)
    {
        if (!IsValidPosition(col, row)) return null;
        return grid[col, row];
    }

    public void SetSelectedCell(int row, int col)
    {
        selectedCell = new Vector2Int(row, col);
    }

    public Vector2Int GetSelectedCell()
    {
        return selectedCell;
    }

    public Vector2Int GetGridPositionFromWorld(Vector3 worldPosition)
    {
        int col = Mathf.RoundToInt((worldPosition.x - origin.x) / cellSize);
        int row = Mathf.RoundToInt((worldPosition.y - origin.y) / cellSize);

        return IsValidPosition(col, row) ? new Vector2Int(col, row) : new Vector2Int(-1, -1);
    }

    public void DeselectAllCells()
    {
        for (int x = 0; x < columns; x++)
            for (int y = 0; y < rows; y++)
                if (grid[x, y] != null) grid[x, y].HighlightCell(false);
    }

    public void ResetBoard()
    {
        for (int x = 0; x < columns; x++)
            for (int y = 0; y < rows; y++)
                if (grid[x, y] != null) grid[x, y].ClearOccupant();
    }

    Sprite GetRandomTile()
    {
        int randomIndex = UnityEngine.Random.Range(0, normalTiles.Length);
        return normalTiles[randomIndex];
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            InitializeGrid();
            SyncGridWithClientsServerRpc();
        }
        else
        {
            origin = networkOrigin.Value;  // Sync origin from server on client
            ApplySyncedGrid();
        }
    }

    private void ApplySyncedGrid()
    {
        grid = new Cell[columns, rows];
        int index = 0;
        for (int x = 0; x < columns; x++)
        {
            for (int y = 0; y < rows; y++)
            {
                TerrainType terrain = (TerrainType)syncedGrid[index++];
                Vector3 position = origin + new Vector3(x * cellSize, y * cellSize, 0);
                if (grid[x, y] == null)
                {
                    GameObject cellObj = Instantiate(cellPrefab, position, Quaternion.identity, CellsDirectory.transform);
                    cellObj.transform.position = position;  // Ensure exact positioning
                    Cell cellComponent = cellObj.GetComponent<Cell>();
                    cellComponent.Initialize(x, y, terrain);
                    grid[x, y] = cellComponent;
                }
                grid[x, y].SetTileSprite(GetTileSpriteFromTerrain(terrain));
            }
        }
    }

    private Sprite GetTileSpriteFromTerrain(TerrainType terrain)
    {
        switch (terrain)
        {
            case TerrainType.AttackBonus: return attackTile;
            case TerrainType.DefenseBonus: return defenseTile;
            case TerrainType.Healing: return healingTile;
            case TerrainType.ResourceGen: return resourceTile;
            default: return normalTiles[UnityEngine.Random.Range(0, normalTiles.Length)];
        }
    }
}