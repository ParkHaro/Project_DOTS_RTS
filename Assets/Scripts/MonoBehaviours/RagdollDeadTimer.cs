using UnityEngine;

namespace DotsRts.MonoBehaviours
{
    public class RagdollDeadTimer : MonoBehaviour
    {
        private float _timer = 6f;
        private bool _hasColliders = true;

        private void Update()
        {
            _timer -= Time.deltaTime;

            if (_hasColliders && _timer <= 3f)
            {
                foreach (var characterJoint in GetComponentsInChildren<CharacterJoint>())
                {
                    Destroy(characterJoint);
                }

                foreach (var rigidbody in GetComponentsInChildren<Rigidbody>())
                {
                    Destroy(rigidbody);
                }

                foreach (var collider in GetComponentsInChildren<Collider>())
                {
                    Destroy(collider);
                }

                _hasColliders = false;
            }

            if (_timer <= 1f)
            {
                transform.position += Vector3.down * Time.deltaTime;
            }
            
            if (_timer <= 0f)
            {
                Destroy(gameObject);
            }
        }
    }
}