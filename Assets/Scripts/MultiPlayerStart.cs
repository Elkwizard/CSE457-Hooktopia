using UnityEngine;
using Unity.Netcode;

public class MultiPlayerStart : MonoBehaviour
{
    [SerializeField] GameObject StartButton;
    [SerializeField] GameObject JoinButton;
    [SerializeField] GameObject MenuCamera;
    
    public void HostStart()
    {
        EnterGame();
        NetworkManager.Singleton.StartHost();
    }

    public void ClientStart()
    {
        EnterGame();
        NetworkManager.Singleton.StartClient();
    }

    void EnterGame()
    {
        StartButton.SetActive(false);
        JoinButton.SetActive(false);
        MenuCamera.SetActive(false);
    }
}
