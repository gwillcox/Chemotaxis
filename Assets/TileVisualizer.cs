using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class TileVisualizer : MonoBehaviour
{
    public bool show;
    public List<TMPro.TMP_Text> texts;
    public SimController simController;
    public Tilemap tmap;

    // Start is called before the first frame update
    void Start()
    {
        InitTexts();
    }


    void InitTexts()
    {
        texts = new List<TMPro.TMP_Text>();
        /*foreach (Tile t in tmap.cellBounds.allPositionsWithin)
        {
            GameObject newText = 
        }*/
    }
    // Update is called once per frame
    void Update()
    {
        
    }
}
