using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

public enum SpaceType { Wall, Open, Source, Sink }

public class SimController : MonoBehaviour
{
    [Range(0.000001f, 1f)]
    public float diffusionRate;
    public int numDiffusionSteps;
    public Tilemap t_map;
    public List<Vector3Int>[] neighbors;
    public SpaceType[] t_map_types;
    public Vector3Int[] positions;
    public float[,] concentration;
    public int nActiveTiles;


    Vector3Int[] possibleNeighbors = new Vector3Int[4] { new Vector3Int(1, 0, 0), new Vector3Int(0, 1, 0), new Vector3Int(-1, 0, 0), new Vector3Int(0, -1, 0) };
    Vector3Int gridOffset;

    void InitializeConcentration()
    {
        Vector3Int neighborLoc;
        SpaceType neighborType;
        int x, y, i = 0;

        gridOffset = new Vector3Int(t_map.cellBounds.xMin, t_map.cellBounds.yMin, 0);
        concentration = new float[t_map.size[0], t_map.size[1]];

        nActiveTiles = 0;
        List<Vector3Int> tilePositions = new List<Vector3Int>();
        foreach (Vector3Int location in t_map.cellBounds.allPositionsWithin)
        {
            TileBase tile = t_map.GetTile(location);
            if (tile != null && tile.name != "Wall")
            {
                nActiveTiles++;
                tilePositions.Add(location);
            }
        }

        t_map_types = new SpaceType[nActiveTiles];
        neighbors = new List<Vector3Int>[nActiveTiles];
        positions = new Vector3Int[nActiveTiles];
        foreach (Vector3Int location in tilePositions)
        {
            x = GetXIndexFromLocation(location);
            y = GetYIndexFromLocation(location);
            TileBase tile = t_map.GetTile(location);
            positions[i] = location - gridOffset;
            neighbors[i] = new List<Vector3Int>();

            SpaceType tileType;
            if (tile.name == "Wall") { concentration[x, y] = 0; tileType = SpaceType.Wall; }
            else if (tile.name == "Full") { concentration[x, y] = 1; tileType = SpaceType.Open; }
            else if (tile.name == "Empty") { concentration[x, y] = 0; tileType = SpaceType.Open; }
            else if (tile.name == "Sink") { concentration[x, y] = 0; tileType = SpaceType.Sink; }
            else { concentration[x, y] = 1; tileType = SpaceType.Source; } // Source
            t_map_types[i] = tileType;


            foreach (Vector3Int neighbor in possibleNeighbors)
            {
                neighborLoc = positions[i] + neighbor;
                neighborType = GetSpaceType(neighborLoc+gridOffset);
                if (neighborType != SpaceType.Wall)
                {
                    neighbors[i].Add(neighborLoc);
                }
            }
            i++;
        }
    }


    // Start is called before the first frame update
    void Start()
    {
        t_map = GetComponentInChildren<Tilemap>();
        Vector3Int t_loc = new Vector3Int(1, 1, 0);

        InitializeConcentration();
    }

    SpaceType GetSpaceType(int id)
    {
        try
        {
            return t_map_types[id];
        }
        catch
        {
            return SpaceType.Wall;
        }
    }

    SpaceType GetSpaceType(Vector3Int location) { 
        TileBase tile = t_map.GetTile(location);
        if (tile == null)
        {
            return SpaceType.Wall;
        }
        else if (tile.name == "Empty" || tile.name == "Full")
        {
            return SpaceType.Open;
        }
        else if (tile.name == "Source")
        {
            return SpaceType.Source;
        }
        else if (tile.name == "Sink")
        {
            return SpaceType.Sink;
        }
        else { 
            return SpaceType.Wall; 
        } 
    }


    bool IsPositionInBounds(Vector3Int loc)
    {
        if (loc.x <= t_map.cellBounds.xMax & 
            loc.x >= t_map.cellBounds.xMin & 
            loc.y < t_map.cellBounds.yMax & 
            loc.y >= t_map.cellBounds.yMin)
        {
            return true;
        }
        return false;
    }

    public int GetXIndexFromLocation(Vector3Int location)
    {
        return location.x - t_map.cellBounds.xMin;
    }

    public int GetYIndexFromLocation(Vector3Int location)
    {
        return location.y - t_map.cellBounds.yMin;
    }

    void DiffuseConcentration()
    {
        float[,] new_concentration = concentration;
        SpaceType locType;
        float transferredAmount;
        int x, y;

        for (int i = 0; i < positions.Length; i++)
        {
            // Search over each neighboring tile in an 4-neighbor radius.
            // If the neighbor is not a wall, transfer chemicals from between the two cells proportionally. 
            x = positions[i].x;
            y = positions[i].y;
            locType = GetSpaceType(i);

            if (locType != SpaceType.Wall)
            {
                if (locType == SpaceType.Open)
                {
                    foreach (Vector3Int neighbor in neighbors[i])
                    {
                        transferredAmount = (diffusionRate * (concentration[x, y] - concentration[neighbor.x, neighbor.y])) / (neighbors[i].Count + 1f); // What 
                        new_concentration[x, y] -= transferredAmount;
                        new_concentration[neighbor.x, neighbor.y] += transferredAmount;
                    }
                }
                else if (locType == SpaceType.Sink) { 
                    new_concentration[x, y] = 0; 
                }
                else if (locType == SpaceType.Source) { 
                    new_concentration[x, y] = 1; 
                }
            }
            else
            {
                new_concentration[x, y] = -1;
            }
        }
        concentration = new_concentration;

    }

    void UpdateColors()
    {

        Color newColor;

        foreach (Vector3Int location in t_map.cellBounds.allPositionsWithin)
        {
            SpaceType spaceType = GetSpaceType(location);

            if (spaceType == SpaceType.Wall)
            {
                newColor = Color.white;
            }
            else
            {
                newColor = Color.Lerp(Color.blue, Color.red, concentration[GetXIndexFromLocation(location), GetYIndexFromLocation(location)]);
            }
            SetTileColour(newColor, location, t_map);
        }
    }

    private void SetTileColour(Color colour, Vector3Int position, Tilemap tilemap)
    {
        // Flag the tile, inidicating that it can change colour.
        // By default it's set to "Lock Colour".
        tilemap.SetTileFlags(position, TileFlags.None);

        // Set the colour.
        tilemap.SetColor(position, colour);
    }

    // Update is called once per frame
    void Update()
    {
        for (int i=0; i<numDiffusionSteps; i++) { DiffuseConcentration(); }
        UpdateColors();
    }

    private struct MyJob : IJob
    {
        [ReadOnly]
        public NativeArray<float> Input;

        [WriteOnly]
        public NativeArray<float> Output;

        public void Execute()
        {
            float result = 0.0f;
            for (int i = 0; i < Input.Length; i++)
            {
                result += Input[i];
            }
            Output[0] = result;
        }
    }

    private void OnDrawGizmosSelected()
    {
        for (int i=0; i<positions.Length; i++)
        {
            Gizmos.color = Color.Lerp(Color.green, Color.red, concentration[positions[i].x, positions[i].y]);
            Gizmos.DrawCube(positions[i], Vector3.one * 1f);
        }
    }
}
