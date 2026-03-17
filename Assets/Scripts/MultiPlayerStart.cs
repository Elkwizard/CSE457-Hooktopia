using UnityEngine;
using Unity.Netcode;

public class MultiPlayerStart : MonoBehaviour
{
    [SerializeField] GameObject StartButton;
    [SerializeField] GameObject JoinButton;
    [SerializeField] GameObject MenuCamera;
    public GameObject mapPrefab;
    
    public void HostStart()
    {
        EnterGame();
        NetworkManager.Singleton.StartHost();
        GameObject map = Instantiate(mapPrefab, new Vector3(0,0,0), Quaternion.identity);
        map.GetComponent<NetworkObject>().Spawn();
        //NetworkObject[] networkObjectList = GetComponentsInChildren<NetworkObject>();
        //foreach (NetworkObject networkObject in networkObjectList) {
        //    networkObject.Spawn();
        //}
    }

    public void ClientStart()
    {
        EnterGame();
        NetworkManager.Singleton.StartClient();
        print("client started");
    }

    void EnterGame()
    {
        StartButton.SetActive(false);
        JoinButton.SetActive(false);
        MenuCamera.SetActive(false);
    }
}
