using UnityEngine;

namespace EarthOnline.Player
{
    /// <summary>
    /// 第三人称角色控制器。WASD移动 + 鼠标视角 + 滚轮缩放 + Space跳跃。
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        [Header("移动")]
        public float walkSpeed = 3f;
        public float runSpeed = 6f;
        public float jumpHeight = 1.5f;
        public float gravity = -20f;

        [Header("视角")]
        public float mouseSensitivity = 2f;
        public float minPitch = -80f;
        public float maxPitch = 60f;
        public float cameraDistance = 5f;
        public float minZoom = 2f;
        public float maxZoom = 10f;
        public float zoomSpeed = 2f;

        private CharacterController _controller;
        private Camera _mainCamera;
        private float _yaw = 0f;
        private float _pitch = 10f;
        private float _verticalVelocity = 0f;
        private bool _isGrounded;

        void Start()
        {
            _controller = GetComponent<CharacterController>();
            _mainCamera = Camera.main;
            if (_mainCamera == null)
            {
                Debug.LogError("[PlayerController] No MainCamera found in scene!");
                return;
            }
            _yaw = transform.eulerAngles.y;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        void Update()
        {
            if (_mainCamera == null) return;
            HandleMouseLook();
            HandleZoom();
            HandleMovement();
        }

        void HandleMouseLook()
        {
            float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
            float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;
            _yaw += mouseX;
            _pitch -= mouseY;
            _pitch = Mathf.Clamp(_pitch, minPitch, maxPitch);
            transform.rotation = Quaternion.Euler(0f, _yaw, 0f);
            UpdateCameraPosition();
        }

        void HandleZoom()
        {
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            cameraDistance -= scroll * zoomSpeed;
            cameraDistance = Mathf.Clamp(cameraDistance, minZoom, maxZoom);
        }

        void UpdateCameraPosition()
        {
            Vector3 cameraOffset = new Vector3(0f, 2f, -cameraDistance);
            Quaternion rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            Vector3 desiredPosition = transform.position + rotation * cameraOffset;

            RaycastHit hit;
            if (Physics.Linecast(transform.position + Vector3.up * 1.5f, desiredPosition, out hit))
                desiredPosition = hit.point + hit.normal * 0.3f;

            _mainCamera.transform.position = Vector3.Lerp(
                _mainCamera.transform.position, desiredPosition, Time.deltaTime * 10f);
            _mainCamera.transform.LookAt(transform.position + Vector3.up * 1.5f);
        }

        void HandleMovement()
        {
            _isGrounded = _controller.isGrounded;
            if (_isGrounded && _verticalVelocity < 0)
                _verticalVelocity = -2f;

            float horizontal = Input.GetAxis("Horizontal");
            float vertical = Input.GetAxis("Vertical");
            Vector3 moveDir = transform.right * horizontal + transform.forward * vertical;
            moveDir = moveDir.normalized;

            float speed = Input.GetKey(KeyCode.LeftShift) ? runSpeed : walkSpeed;
            _controller.Move(moveDir * speed * Time.deltaTime);

            if (Input.GetButtonDown("Jump") && _isGrounded)
                _verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);

            _verticalVelocity += gravity * Time.deltaTime;
            _controller.Move(Vector3.up * _verticalVelocity * Time.deltaTime);
        }

        void OnDestroy()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}
