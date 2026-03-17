using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

public class PlayerManager : MonoBehaviour
{
    private List<PlayerInput> playerInputs = new List<PlayerInput>();
    [SerializeField] List<Transform> startingPoints;
    [SerializeField] List<LayerMask> playerLayers;
    [SerializeField] GameObject winText;


    private PlayerInputManager playerInputManager;

    private void Awake()
    {
        playerInputManager = FindFirstObjectByType<PlayerInputManager>();
        playerInputManager.onPlayerJoined += AddPlayer;
    }


    public void AddPlayer(PlayerInput playerInput)
    {
        playerInputs.Add(playerInput);
        playerInput.transform.position =  startingPoints[playerInputs.Count - 1].position;
        playerInput.transform.rotation =  startingPoints[playerInputs.Count - 1].rotation;
        Player player = playerInput.GetComponent<Player>();
        player.manager = this;
    }
    
    public void GameEnd(Player player) {
        PlayerInput input = player.GetComponent<PlayerInput>();
        winText.SetActive(true);
        if (input == playerInputs[0]) {
            winText.GetComponent<TextMeshProUGUI>().text  = "Player Two Wins";
        } else {
            winText.GetComponent<TextMeshProUGUI>().text  = "Player One Wins";
        }
    }
}
