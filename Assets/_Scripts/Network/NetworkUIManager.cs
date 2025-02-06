using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
public class NetworkUIManager : NetworkBehaviour
{
    public static NetworkUIManager Instance { get; private set; }
    
    [SerializeField] private GameObject lobbyUI;
    [SerializeField] private GameObject gameUI;
    [SerializeField] private GameObject ConnectionPanelUI;
    [SerializeField] private TextMeshProUGUI resourceText;
    [SerializeField] private TextMeshProUGUI turnText;
    [SerializeField] private Button endTurnButton;

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }
    
    public void ShowGameUI()
    {
        lobbyUI.SetActive(false);
        gameUI.SetActive(true);
        ConnectionPanelUI.SetActive(false);
        //ToDo Ami change later to where the game start 
        //(maybe after player 1 place the first Unit )
        TurnManager.Instance.StartGame();
    }
    
    public void UpdateResourceText(int resources)
    {
        resourceText.text = $"Resources: {resources}";
    }
    
    public void UpdateTurnText(bool isMyTurn)
    {
        turnText.text = isMyTurn ? "Your Turn" : "Opponent's Turn";
        endTurnButton.interactable = isMyTurn;
    }
}
