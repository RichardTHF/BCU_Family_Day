using System.Collections;
using System.Collections.Generic;
using UnityEditor.PackageManager;
using UnityEngine;
using UnityEngine.Tilemaps;
using System.IO;
using System;
using System.Runtime.CompilerServices;
using System.Linq;

public class FishSpawner : MonoBehaviour
{

    [SerializeField] private GameObject[] fishprefabs;
    [SerializeField] private string[] fishNames;

    public List<GameObject> fishlist;
    [SerializeField] private int maxFish = 50; 


    private List<Tuple<string, string>> spawnQueue;

    // Start is called before the first frame update
    void Start()
    {
        spawnQueue = new List<Tuple<string, string>>();
    }

    // Update is called once per frame
    void Update()
    {
        if (spawnQueue.Count > 0)
        {
            foreach (var item in spawnQueue)
            {
                SpawnFishInternal(item.Item1, item.Item2);
            }

            spawnQueue = new List<Tuple<string, string>>();
        }

        while(fishlist.Count > maxFish)
        {
            Destroy(fishlist[0]);
            fishlist.RemoveAt(0);
        }
    }

    void SpawnFishInternal(string fishName, string textureFilename)
    {
        int fishIndex = -1;
        for (int i = 0; i < fishNames.Length; i++)
        {
            if (fishNames[i] == fishName)
            {
                fishIndex = i;
            }
        }

        if (fishIndex == -1)
        {
            Debug.LogError("Failed to spawn fish" + fishName);
            return;
        }

        GameObject fish = Instantiate(fishprefabs[fishIndex]);
        fishlist.Add(fish);

        fish.transform.position = this.transform.position;

        Material fishMaterial = new Material(Shader.Find("Standard"));

        if (File.Exists(textureFilename))
        {
            byte[] fileData = File.ReadAllBytes(textureFilename);
            Texture2D tex = new Texture2D(2, 2);
            tex.LoadImage(fileData); //..this will auto-resize the texture dimensions.

            fishMaterial.mainTexture = tex;

            SkinnedMeshRenderer[] renderers = fish.GetComponentsInChildren<SkinnedMeshRenderer>();
            foreach(SkinnedMeshRenderer r in renderers)
            {
                r.material = fishMaterial;
            }
        }
        else
        {
            Debug.LogError("Unable to load image texture from " + textureFilename);
        }

    }

    public void SpawnFish(string fishName, string textureFilename)
    {
        spawnQueue.Add(new Tuple<string, string>(fishName, textureFilename));   
    }
}
