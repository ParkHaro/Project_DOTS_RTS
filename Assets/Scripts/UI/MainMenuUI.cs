using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DotsRts.UI
{
    public class MainMenuUI : MonoBehaviour
    {
        [SerializeField] private Button _playButton;
        [SerializeField] private Button _quitButton;

        private void Awake()
        {
            _playButton.onClick.AddListener(() =>
            {
                SceneManager.LoadScene(1);
            });
            
            _quitButton.onClick.AddListener(() =>
            {
                Application.Quit();
            });
        }
    }
}