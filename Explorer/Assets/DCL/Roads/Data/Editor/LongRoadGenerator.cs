using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LongRoadGenerator : MonoBehaviour
{
    public int roadLength = 12;

    //Long road prefab has a length of 5 roads
    public GameObject longRoadPrefab;

    private void Start()
    {
        for (int i = 0; i < roadLength; i++)
        {
            Instantiate(longRoadPrefab, new Vector3(-80 * i, 0, 0), Quaternion.identity, transform);
            Instantiate(longRoadPrefab, new Vector3(-64 + -80 * i, 0, 16), Quaternion.Euler(0, 180, 0), transform);
        }
    }
}