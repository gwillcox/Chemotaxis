using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class RobotController : MonoBehaviour
{
    [Header("Robot Settings")]
    public float sensorDistance = 0.05f;
    public float turnSpeed = 0.05f;
    public float movementSpeed;
    public float noiseScale = 0f;
    public bool logSensing = true;
    [Range(0f,1f)]
    public float cRemovalRate=0f;
    public float wallAvoidanceRate=1f;

    [Header("Readouts")]
    public Vector3 position;
    public Vector3Int posInt;
    public Vector3 observedGradient;
       public Vector3Int[] neighbors = new Vector3Int[4];
       public float[] neighborConcentrations = new float[4];
    public float turnDirection;
    SimController simController;

    // Start is called before the first frame update
    void Start()
    {
        simController = GetComponentInParent<SimController>();
        position = transform.position;

        transform.Rotate(new Vector3(0, 0, Random.Range(0, 360)));
    }

    Vector2 GetObservedGradient()
    {
        observedGradient = Vector3.zero;

        // Find four closest neighbors. 
        posInt = simController.t_map.WorldToCell(position);
        Vector3Int[] neighbors = new Vector3Int[4];
        neighbors[0] = posInt;
        neighbors[1] = posInt + new Vector3Int(1, 0, 0);
        neighbors[2] = posInt + new Vector3Int(0, 1, 0);
        neighbors[3] = posInt + new Vector3Int(1, 1, 0);

        float[,] concentration = simController.concentration;

        // Calculate the gradient using SOBEL
        observedGradient = new Vector3(
            GetConcentration(neighbors[1]) - GetConcentration(neighbors[0]), 
            GetConcentration(neighbors[2]) - GetConcentration(neighbors[0]), 
            0);

        observedGradient += Random.insideUnitSphere * noiseScale ;

        return observedGradient;
    }

    float GetConcentration(Vector3Int location)
    {
        try
        {
            return simController.concentration[simController.GetXIndexFromLocation(location), simController.GetYIndexFromLocation(location)];
        }
        catch
        {
            return -1f; // Handle outside-of-array cases
        }
    }

    void MoveAlongGradient()
    {
        transform.position += observedGradient * movementSpeed;
        position = transform.position;
    }

    float InterpolateConcentrationAtPoint(Vector3 point)
    {
        // Find four closest neighbors. 
        posInt = simController.t_map.WorldToCell(point);

        int y1 = posInt.y;
        int y2 = (point.y > posInt.y) ? posInt.y + 1 : posInt.y - 1;
        int x1 = posInt.x;
        int x2 = (point.x > posInt.x) ? posInt.x + 1 : posInt.x - 1;

        neighbors[0] = posInt;
        neighbors[1] = new Vector3Int(x1, y2, 0);
        neighbors[2] = new Vector3Int(x2, y1, 0);
        neighbors[3] = new Vector3Int(x2, y2, 0);

          neighborConcentrations[0]  = GetConcentration(neighbors[0]);
          neighborConcentrations[1]  = GetConcentration(neighbors[1]);
          neighborConcentrations[2]  = GetConcentration(neighbors[2]);
        neighborConcentrations[3]  = GetConcentration(neighbors[3]);

        float f_x_y1 = (x2 - point.x) / (x2 - x1) *  neighborConcentrations[0]  + (point.x - x1) / (x2 - x1) *  neighborConcentrations[2] ;
        float f_x_y2 = (x2 - point.x) / (x2 - x1) *  neighborConcentrations[1]  + (point.x - x1) / (x2 - x1) *  neighborConcentrations[3] ;
        float f_x_y = (y2 - point.y) / (y2 - y1) * f_x_y1 + (point.y - y1) / (y2 - y1) * f_x_y2;

        TileBase t;
        // Push away from walls: bias the concentration AWAY from walls. 
        foreach (Vector3Int n in neighbors)
        {
            t = simController.t_map.GetTile(n);
            if (t!=null && t.name == "Wall") { 
                f_x_y -= wallAvoidanceRate / (Mathf.Abs(point.x - n.x) + Mathf.Abs(point.y - n.y));
            }
        }

        return f_x_y;
    }

    float GetNoise()
    {
        return noiseScale*Random.Range(-1f, 1f);
    }

    float GetTurnDirection()
    {
        float c_right = InterpolateConcentrationAtPoint(transform.position + sensorDistance * (transform.up + transform.right));
        float c_left = InterpolateConcentrationAtPoint(transform.position + sensorDistance * (transform.up - transform.right));
        observedGradient = new Vector3(c_left, c_right, 0);

        if (logSensing)
        {
            // Add some noise logarithmically or linearly
            return Mathf.Sign(Mathf.Log10(c_left)- Mathf.Log10(c_right) + GetNoise() );
        }
        else
        {
            return Mathf.Sign(c_left - c_right + GetNoise());
        }
    }

    void Step()
    {
        // Sense which direction has the highest potential, with some noise
        turnDirection = GetTurnDirection();

        // Turn towards that direction
        transform.Rotate(new Vector3(0, 0, turnSpeed * turnDirection));

        // Move forwards
        transform.position += transform.up * movementSpeed;

        // Remove a small amount of chemical from the current position
        Vector3Int loc = simController.t_map.WorldToCell(transform.position);
        int x = simController.GetXIndexFromLocation(loc);
        int y = simController.GetYIndexFromLocation(loc);
        simController.concentration[x, y] -= simController.concentration[x, y] * cRemovalRate;

        /*
        observedGradient = GetObservedGradient();
        MoveAlongGradient();*/
    }

    // Update is called once per frame
    void Update()
    {
        Step();
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(transform.position, transform.position + sensorDistance * (transform.up + transform.right));
        Gizmos.DrawLine(transform.position, transform.position + sensorDistance * (transform.up - transform.right));
        Gizmos.DrawSphere(transform.position + sensorDistance * (transform.up + transform.right), 0.02f);
        Gizmos.DrawSphere(transform.position + sensorDistance * (transform.up - transform.right), 0.02f);

        for (int i=0; i<neighbors.Length; i++)
        {
            Gizmos.color = Color.Lerp(Color.red, Color.blue, neighborConcentrations[i]);
            Gizmos.DrawSphere(neighbors[i], 0.1f);
        }
    }
}
