using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DotsRts.UI
{
    public class GameOverUI : MonoBehaviour
    {
        [SerializeField] private Button _mainMenuButton;

        private void Awake()
        {
            _mainMenuButton.onClick.AddListener(() =>
            {
                Time.timeScale = 1f;
                SceneManager.LoadScene(0);
            });
        }

        private void Start()
        {
            DOTSEventsManager.Instance.OnHQDead += DOTSEventsManager_OnHQDead;
            Hide();
        }

        private void DOTSEventsManager_OnHQDead(object sender, EventArgs e)
        {
            Show();
            Time.timeScale = 0;
        }

        private void Show()
        {
            gameObject.SetActive(true);
        }

        private void Hide()
        {
            gameObject.SetActive(false);
        }
    }
}