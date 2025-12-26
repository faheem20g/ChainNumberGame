using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    [SerializeField] private Button restartButton;
    [SerializeField] private GameObject hintPanel;
    [SerializeField] private GameObject winPanel;

    public static UIManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if (restartButton != null)
        {
            restartButton.onClick.AddListener(RestartGame);
        }
    }

    // Update is called once per frame
   public void ShowRestartButton()
   {
       restartButton.gameObject.SetActive(true);
   }

   public void HideHintPanel()
   {
       hintPanel.gameObject.SetActive(false);
   }

    public void RestartGame()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
    public void ShowWinPanel()
    {
        winPanel.gameObject.SetActive(true);
    }   
}
