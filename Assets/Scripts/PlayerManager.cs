using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;
using UnityEngine.SceneManagement;

public class PlayerManager : MonoBehaviour
{
    private readonly List<PlayerInput> playerInputs = new ();
    [SerializeField] List<Transform> startingPoints;
    [SerializeField] List<string> playerLayers;
    [SerializeField] GameObject winText;
    [SerializeField] GameObject restartButton;
    [SerializeField] GameObject startingCam;
    [SerializeField] GameObject joinText;
    private bool isGameOver;


    private PlayerInputManager playerInputManager;

    private void Awake()
    {
        playerInputManager = FindFirstObjectByType<PlayerInputManager>();
        playerInputManager.onPlayerJoined += AddPlayer;
        isGameOver = false;
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

    public bool IsGameOver()
    {
        return isGameOver;
    }
    
    public void GameEnd(Player loser) {
        isGameOver = true;
        foreach (var player in playerInputs) {
            player.gameObject.GetComponent<Rigidbody>().isKinematic = true;
        }

        PlayerInput input = loser.GetComponent<PlayerInput>();
        winText.SetActive(true);
        restartButton.SetActive(true);
        string winnerName = input == playerInputs[0] ? "Player Two" : "Player One";
        winText.GetComponent<TextMeshProUGUI>().text = $"{winnerName} Wins!";
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    public void Restart()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        
    }
}
