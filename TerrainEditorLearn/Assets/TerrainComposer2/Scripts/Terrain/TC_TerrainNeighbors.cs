using UnityEngine;
using System;


public class TC_TerrainNeighbors: MonoBehaviour {
    
    public Terrain left;
    public Terrain top;
    public Terrain right;
    public Terrain bottom;
    
    public void Start() 
    {
    	Terrain terrain = null;
    	terrain = GetComponent<Terrain>();
    	terrain.SetNeighbors(left,top,right,bottom);
    }
}