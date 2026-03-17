using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
//using Cinemachine;

public class PlayerManager : MonoBehaviour
{
    private List<PlayerInput> playerInputs = new List<PlayerInput>();
    [SerializeField] List<Transform> startingPoints;
    [SerializeField] List<LayerMask> playerLayers;

    private PlayerInputManager playerInputManager;

    private void Awake()
    {
        playerInputManager = FindFirstObjectByType<PlayerInputManager>();
        playerInputManager.onPlayerJoined += AddPlayer;
    }


    public void AddPlayer(PlayerInput playerInput)
    {
        print("added");
        playerInputs.Add(playerInput);
        playerInput.transform.position =  startingPoints[playerInputs.Count - 1].position;
        playerInput.transform.rotation =  startingPoints[playerInputs.Count - 1].rotation;
    }
}
