using System.Collections;
using UnityEngine;

namespace DotsRts.MonoBehaviours
{
    public class FogOfWarPersistent : MonoBehaviour
    {
        [SerializeField] private RenderTexture _fogOfWarRenderTexture;
        [SerializeField] private RenderTexture _fogOfWarPersistentRenderTexture;
        [SerializeField] private RenderTexture _fogOfWarPersistent2RenderTexture;
        [SerializeField] private Material _fogOfWarPersistentMaterial;

        private bool isInitialized = false;

        private IEnumerator Start()
        {
            yield return new WaitForEndOfFrame();

            Graphics.Blit(_fogOfWarRenderTexture, _fogOfWarPersistentRenderTexture);
            Graphics.Blit(_fogOfWarRenderTexture, _fogOfWarPersistent2RenderTexture);
            isInitialized = true;
        }

        private void Update()
        {
            if (!isInitialized)
            {
                return;
            }

            Graphics.Blit(_fogOfWarRenderTexture, _fogOfWarPersistentRenderTexture, _fogOfWarPersistentMaterial, 0);
            Graphics.Blit(_fogOfWarPersistentRenderTexture, _fogOfWarPersistent2RenderTexture);
        }
    }
}