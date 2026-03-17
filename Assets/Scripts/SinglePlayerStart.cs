using UnityEngine;
using Unity.Netcode;

public class SinglePlayerStart : MonoBehaviour
{
    public GameObject mapPrefab;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        NetworkManager.Singleton.StartHost();
        GameObject map = Instantiate(mapPrefab, new Vector3(0,0,0), Quaternion.identity);
        map.GetComponent<NetworkObject>().Spawn();
        //NetworkObject[] networkObjectList = GetComponentsInChildren<NetworkObject>();
        //foreach (NetworkObject networkObject in networkObjectList) {
        //    networkObject.Spawn();
        //}
    }
}
