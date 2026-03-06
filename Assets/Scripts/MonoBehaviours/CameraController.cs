using System;
using Unity.Cinemachine;
using UnityEngine;

namespace DotsRts.MonoBehaviours
{
    public class CameraController : MonoBehaviour
    {
        [SerializeField] private CinemachineCamera _cinemachineCamera;

        [SerializeField] private float _fieldOfViewMin;
        [SerializeField] private float _fieldOfViewMax;

        private float _targetFieldOfView;

        private void Awake()
        {
            _targetFieldOfView = _cinemachineCamera.Lens.FieldOfView;
        }

        private void Update()
        {
            var moveDir = Vector3.zero;

            if (Input.GetKey(KeyCode.W))
            {
                moveDir.z = +1;
            }

            if (Input.GetKey(KeyCode.S))
            {
                moveDir.z = -1;
            }

            if (Input.GetKey(KeyCode.A))
            {
                moveDir.x = -1;
            }

            if (Input.GetKey(KeyCode.D))
            {
                moveDir.x = +1;
            }

            var mainTransform = Camera.main.transform;
            moveDir = mainTransform.forward * moveDir.z + mainTransform.right * moveDir.x;
            moveDir.y = 0f;
            moveDir.Normalize();

            var moveSpeed = 30f;
            transform.position += moveSpeed * Time.deltaTime * moveDir;

            var rotationAmount = 0f;
            if (Input.GetKey(KeyCode.Q))
            {
                rotationAmount = +1f;
            }

            if (Input.GetKey(KeyCode.E))
            {
                rotationAmount = -1f;
            }

            var rotationSpeed = 200f;
            transform.eulerAngles += new Vector3(0, rotationAmount * rotationSpeed * Time.deltaTime, 0);

            var zoomAmount = 4f;
            if (Input.mouseScrollDelta.y > 0)
            {
                _targetFieldOfView -= zoomAmount;
            }

            if (Input.mouseScrollDelta.y < 0)
            {
                _targetFieldOfView += zoomAmount;
            }

            
            _targetFieldOfView = Mathf.Clamp(_targetFieldOfView, _fieldOfViewMin, _fieldOfViewMax);
            var zoomSpeed = 10f;
            _cinemachineCamera.Lens.FieldOfView = Mathf.Lerp(_cinemachineCamera.Lens.FieldOfView,
                _targetFieldOfView, zoomSpeed * Time.deltaTime);
        }
    }
}