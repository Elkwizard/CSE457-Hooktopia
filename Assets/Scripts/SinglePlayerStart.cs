using UnityEngine;
using Unity.Netcode;

public class SinglePlayerStart : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        NetworkManager.Singleton.StartHost();
    }
}
