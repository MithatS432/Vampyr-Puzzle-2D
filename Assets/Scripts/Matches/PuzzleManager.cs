using UnityEngine;

public class PuzzleManager : MonoBehaviour
{
    [Header("Grid Settings")]
    public int width = 5;
    public int height = 5;
    public float tileSpacing = 1f;

    [Header("Normal Tile Prefabs")]
    public GameObject redPrefab;
    public GameObject yellowPrefab;
    public GameObject greenPrefab;
    public GameObject bluePrefab;

    Tile[,] grid;

    void Start()
    {
        CreateGrid();
    }

    void CreateGrid()
    {
        grid = new Tile[width, height];

        float offsetX = (width - 1) / 2f;
        float offsetY = (height - 1) / 2f;

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                TileType type = GetRandomNormalTile();
                GameObject prefab = GetPrefabByType(type);

                Vector3 spawnPos = new Vector3(
                    (x - offsetX) * tileSpacing,
                    (y - offsetY) * tileSpacing,
                    0f
                );

                GameObject tileObj = Instantiate(prefab, spawnPos, Quaternion.identity, transform);

                Tile tile = tileObj.GetComponent<Tile>();
                tile.x = x;
                tile.y = y;
                tile.tileType = type;

                grid[x, y] = tile;
            }
        }
    }


    TileType GetRandomNormalTile()
    {
        TileType[] normalTiles =
        {
            TileType.Red,
            TileType.Yellow,
            TileType.Green,
            TileType.Blue
        };

        return normalTiles[Random.Range(0, normalTiles.Length)];
    }

    GameObject GetPrefabByType(TileType type)
    {
        switch (type)
        {
            case TileType.Red:
                return redPrefab;
            case TileType.Yellow:
                return yellowPrefab;
            case TileType.Green:
                return greenPrefab;
            case TileType.Blue:
                return bluePrefab;
            default:
                return redPrefab;
        }
    }
}
