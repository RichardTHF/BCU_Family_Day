using System.Collections;
using System.Collections.Generic;
using UnityEditor.PackageManager;
using UnityEngine;
using UnityEngine.Tilemaps;
using System.IO;

public class FishSpawner : MonoBehaviour
{

    [SerializeField] private GameObject[] fishprefabs;
    [SerializeField] private string[] fishNames;

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void SpawnFish(string fishName, string textureFilename)
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
}
