using FishNet.Object;
using FishNet.Object.Prediction;
using Game.Presentation.Combat;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Presentation.Player
{
    /// <summary>
    /// Movimiento FPS predicho/reconciliado con Fish-Net Prediction v2 sobre CharacterController.
    /// CharacterController no es un rigidbody, así que NO se usa PredictionRigidbody: el estado
    /// se reconcilia manualmente (posición, velocidad vertical, rotación).
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerMovementController : NetworkBehaviour
    {
        [Header("Movimiento")]
        [SerializeField] private PlayerStats _stats;
        [SerializeField] private float _moveSpeed = 6f;
        [SerializeField] private float _sprintMultiplier = 1.5f;
        [SerializeField] private float _jumpForce = 6f;
        [SerializeField] private float _gravity = -20f;

        private CharacterController _controller;
        private InputAction _moveAction;
        private InputAction _jumpAction;
        private InputAction _sprintAction;
        private InputAction _lookAction;

        private Vector3 _verticalVelocity;
        private Vector3 _pendingImpulse;
        private bool _jumpQueued;
        
        [Header("Mirada")]
        [SerializeField] private float _mouseSensitivity = 0.65f;
        [SerializeField] private CameraLookController _cameraLook;
        [SerializeField] private GameObject _cameraRoot; // el GameObject "Camera" hijo del Player
        //para al abrir el inventario no siga moviendo la vista
        private bool _inputBlocked;
        /// <summary>Bloquea/desbloquea la lectura de input (para cuando se abre UI como el inventario).</summary>
        public void SetInputBlocked(bool blocked) => _inputBlocked = blocked;

        public struct ReplicateData : FishNet.Object.Prediction.IReplicateData
        {
            public Vector2 Move;
            public bool Jump;
            public bool Sprint;
            public float Yaw; // rotación absoluta, no delta

            public ReplicateData(Vector2 move, bool jump, bool sprint, float yaw) : this()
            {
                Move = move;
                Jump = jump;
                Sprint = sprint;
                Yaw = yaw;
            }

            private uint _tick;
            public void Dispose() { }
            public uint GetTick() => _tick;
            public void SetTick(uint value) => _tick = value;
        }

        public struct ReconcileData : FishNet.Object.Prediction.IReconcileData
        {
            public Vector3 Position;
            public Vector3 VerticalVelocity;
            public Quaternion Rotation;

            public ReconcileData(Vector3 position, Vector3 verticalVelocity, Quaternion rotation) : this()
            {
                Position = position;
                VerticalVelocity = verticalVelocity;
                Rotation = rotation;
            }

            private uint _tick;
            public void Dispose() { }
            public uint GetTick() => _tick;
            public void SetTick(uint value) => _tick = value;
        }

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();

            // Input System por código: evita depender de un asset .inputactions
            // para este prototipo. Migrar a un InputActionAsset compartido cuando
            // se defina el mapeo completo de acciones del juego.
            _moveAction = new InputAction("Move", InputActionType.Value, expectedControlType: "Vector2");
            _moveAction.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");

            _jumpAction = new InputAction("Jump", InputActionType.Button, "<Keyboard>/space");
            _sprintAction = new InputAction("Sprint", InputActionType.Button, "<Keyboard>/leftShift");
            _lookAction = new InputAction("Look", InputActionType.Value, "<Mouse>/delta");
        }

        private void OnEnable()
        {
            _moveAction.Enable();
            _jumpAction.Enable();
            _sprintAction.Enable();
            _lookAction.Enable();
            Cursor.lockState = CursorLockMode.Locked;
        }

        private void OnDisable()
        {
            _moveAction.Disable();
            _jumpAction.Disable();
            _sprintAction.Disable();
            _lookAction.Disable();
        }

        public override void OnStartNetwork()
        {
            base.TimeManager.OnTick += TimeManager_OnTick;
            base.TimeManager.OnPostTick += TimeManager_OnPostTick;
        }

        public override void OnStopNetwork()
        {
            base.TimeManager.OnTick -= TimeManager_OnTick;
            base.TimeManager.OnPostTick -= TimeManager_OnPostTick;
        }

        public override void OnStartClient()
        {
            base.OnStartClient();

            if (!base.IsOwner)
            {
                // Desactivar cámara y input para jugadores que no son nuestros.
                if (_cameraRoot != null) _cameraRoot.SetActive(false);
                enabled = false;
            }
        }

        private void Update()
        {
            if (!base.IsOwner) return;

            if (_jumpAction.WasPressedThisFrame())
                _jumpQueued = true;

            // Mirada aplicada cada frame de render (fluidez tipo CS:GO).
            if (!_inputBlocked)
            {
                Vector2 look = _lookAction.ReadValue<Vector2>();
                float yawDelta = look.x * _mouseSensitivity;
                float pitchDelta = -look.y * _mouseSensitivity;

                transform.Rotate(Vector3.up, yawDelta);
                if (_cameraLook != null)
                    _cameraLook.AddPitch(pitchDelta);
            }
        }

        private void TimeManager_OnTick()
        {
            RunInputs(CreateReplicateData());
        }

        private void TimeManager_OnPostTick()
        {
            CreateReconcile();
        }

        private ReplicateData CreateReplicateData()
        {
            if (!base.IsOwner) return default;
            if (_inputBlocked) return new ReplicateData(Vector2.zero, false, false, transform.eulerAngles.y);

            Vector2 move = _moveAction.ReadValue<Vector2>();
            bool sprint = _sprintAction.IsPressed();
            bool jump = _jumpQueued;
            _jumpQueued = false;

            return new ReplicateData(move, jump, sprint, transform.eulerAngles.y);
        }

        [Replicate]
        private void RunInputs(ReplicateData data, ReplicateState state = ReplicateState.Invalid, FishNet.Transporting.Channel channel = FishNet.Transporting.Channel.Unreliable)
        {
            float delta = (float)base.TimeManager.TickDelta;
            if (!_controller.enabled)
                return;

            // El owner ya aplicó su rotación en Update (fluidez por frame).
            // Servidor y replays usan el yaw absoluto que vino en el input.
            if (!base.IsOwner || state.ContainsReplayed())
                transform.rotation = Quaternion.Euler(0f, data.Yaw, 0f);

            // Movimiento horizontal relativo a la orientación del jugador.
            float baseSpeed = _stats != null ? _stats.MoveSpeed : _moveSpeed;
            float speed = baseSpeed * (data.Sprint ? _sprintMultiplier : 1f);
            Vector3 horizontal = (transform.right * data.Move.x + transform.forward * data.Move.y) * speed;

            // Gravedad y salto en el eje vertical, integrados aparte del horizontal.
            if (_controller.isGrounded)
            {
                _verticalVelocity.y = -1f;

                float jumpForce = _stats != null ? _stats.JumpForce : _jumpForce;
                if (data.Jump)
                    _verticalVelocity.y = jumpForce;
            }
            else
            {
                _verticalVelocity.y += _gravity * delta;
            }

            // Impulso pendiente (dash) consumido en el tick.
            Vector3 impulse = _pendingImpulse;
            _pendingImpulse = Vector3.zero;

            _controller.Move((horizontal + _verticalVelocity + impulse / delta) * delta);
        }

        public override void CreateReconcile()
        {
            ReconcileData rd = new ReconcileData(transform.position, _verticalVelocity, transform.rotation);
            ReconcileState(rd);
        }

        [Reconcile]
        private void ReconcileState(ReconcileData data, FishNet.Transporting.Channel channel = FishNet.Transporting.Channel.Unreliable)
        {
            _controller.enabled = false;

            if (base.IsOwner)
            {
                // Conservar la rotación local del owner (la mirada es responsiva, no se corrige).
                transform.position = data.Position;
            }
            else
            {
                transform.SetPositionAndRotation(data.Position, data.Rotation);
            }

            _verticalVelocity = data.VerticalVelocity;
            _controller.enabled = true;
        }

        /// <summary>
        /// Encola un impulso de dash. Solo debe llamarse en el servidor (el owner lo verá vía reconcile).
        /// </summary>
        public void ApplyDashImpulse(Vector3 impulse)
        {
            _pendingImpulse += impulse;
        }

        public void DisableMovement()
        {
            if (base.TimeManager != null)
            {
                base.TimeManager.OnTick -= TimeManager_OnTick;
                base.TimeManager.OnPostTick -= TimeManager_OnPostTick;
            }
            enabled = false;
        }
    }
}
