using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using TMPro; // Import TextMeshPro namespace

public class NetworkManagerUI : MonoBehaviour
{
    [SerializeField] private Button hostBtn;
    [SerializeField] private Button clientBtn;
    [SerializeField] private TMP_InputField joinCodeInput;
    
    private void Awake()
    {
        hostBtn.onClick.AddListener(() => {
            NetworkManager.Singleton.StartHost();
            NetworkUIManager.Instance.ShowGameUI();
        });
        
        clientBtn.onClick.AddListener(() => {
            NetworkManager.Singleton.StartClient();
            NetworkUIManager.Instance.ShowGameUI();
        });
    }
}
