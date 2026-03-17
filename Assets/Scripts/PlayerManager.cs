using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

public class PlayerManager : MonoBehaviour
{
    private List<PlayerInput> playerInputs = new List<PlayerInput>();
    [SerializeField] List<Transform> startingPoints;
    [SerializeField] List<string> playerLayers;
    [SerializeField] GameObject winText;
    [SerializeField] GameObject startingCam;
    [SerializeField] GameObject joinText;


    private PlayerInputManager playerInputManager;

    private void Awake()
    {
        playerInputManager = FindFirstObjectByType<PlayerInputManager>();
        playerInputManager.onPlayerJoined += AddPlayer;
    }


    public void AddPlayer(PlayerInput playerInput)
    {
        startingCam.SetActive(false);
        joinText.SetActive(false);
        playerInputs.Add(playerInput);
        playerInput.transform.position =  startingPoints[playerInputs.Count - 1].position;
        playerInput.transform.rotation =  startingPoints[playerInputs.Count - 1].rotation;
        int layer = LayerMask.NameToLayer(playerLayers[playerInputs.Count - 1]);
        GameObject playerRed = playerInput.transform.Find("player_red").gameObject;
        playerRed.layer = layer;
        foreach (Transform child in playerRed.GetComponentsInChildren<Transform>()) {
            child.gameObject.layer = layer;
        }
        playerInput.transform.Find("Camera").GetComponent<Camera>().cullingMask &= ~(1<<layer);
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
