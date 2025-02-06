using UnityEngine;
using System.Collections.Generic;
using static UnityEditor.PlayerSettings;

public class GridManager : MonoBehaviour
{
    public GameObject cellPrefab;
    public GameObject CellsDirectory;
    public static GridManager Instance { get; private set; }
    
    public int rows = 5;
    public int columns = 8;
    public float cellSize = 1f;
    
    private Cell[,] grid;
    
    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
            
        InitializeGrid();
    }

    private void InitializeGrid()
    {
        grid = new Cell[rows, columns];

        // Calculate the starting position to center the grid
        float gridWidth = columns * cellSize;
        float gridHeight = rows * cellSize;
        Vector3 startPosition = new Vector3(-gridWidth / 2 + cellSize / 2, -gridHeight / 2 + cellSize / 2, 0);

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < columns; c++)
            {
                Vector3 position = new Vector3(c * cellSize, r * cellSize, 0) + startPosition;
                grid[r, c] = new Cell(position, GenerateRandomTerrain());
                var cellObj = Instantiate(cellPrefab, position, Quaternion.identity, CellsDirectory.transform);
            }
        }
    }


    public Vector3 GetWorldPosition(int row, int col)
    {
        return grid[row, col].WorldPosition;
    }
    
    public bool IsValidPosition(int row, int col)
    {
        return row >= 0 && row < rows && col >= 0 && col < columns;
    }
    
    private TerrainType GenerateRandomTerrain()
    {
        float random = Random.value;
        if (random < 0.2f) return TerrainType.DefenseBonus;
        if (random < 0.4f) return TerrainType.AttackBonus;
        if (random < 0.6f) return TerrainType.ResourceGen;
        if (random < 0.8f) return TerrainType.Healing;
        return TerrainType.Normal;
    }
}
