using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
public class NetworkUIManager : NetworkBehaviour
{
    public static NetworkUIManager Instance { get; private set; }
    [SerializeField] private SpawnMenuUI spawnMenuUI;
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
    private void Start()
    {
        Debug.Log($"NetworkUIManager started on {NetworkManager.Singleton.LocalClientId}");
        ShowGameUI();
    }
    private void OnEnable()
    {
        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnectedCallback;
    }

    private void OnDisable()
    {
        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnectedCallback;
    }
    private void OnClientConnectedCallback(ulong clientId)
    {
        Debug.Log($"Client {clientId} connected to the host!");
    }

    public void ShowGameUI()
    {
        Debug.Log("Switching to Game UI...");

        lobbyUI.SetActive(false);
        gameUI.SetActive(true);
        ConnectionPanelUI.SetActive(false);

        TurnManager.Instance.StartGame();

        if (!IsServer)
        {
            Debug.Log("Client: Setting spawn buttons inactive.");
            UpdateTurnText(false);
            spawnMenuUI.SetSpawnButtonsInteractable(false);
        }
        else
        {
            Debug.Log("Host: Setting spawn buttons active.");
            UpdateTurnText(true);
            spawnMenuUI.SetSpawnButtonsInteractable(true);
        }
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
