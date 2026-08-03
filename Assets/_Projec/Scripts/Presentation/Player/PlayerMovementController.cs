using FishNet.Object;
using FishNet.Object.Prediction;
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
        private bool _jumpQueued;
        
        [Header("Mirada")]
        [SerializeField] private float _mouseSensitivity = 0.65f;
        [SerializeField] private CameraLookController _cameraLook;

        public struct ReplicateData : FishNet.Object.Prediction.IReplicateData
        {
            public Vector2 Move;
            public bool Jump;
            public bool Sprint;
            public float YawDelta;
            public float PitchDelta;

            public ReplicateData(Vector2 move, bool jump, bool sprint, float yawDelta, float pitchDelta) : this()
            {
                Move = move;
                Jump = jump;
                Sprint = sprint;
                YawDelta = yawDelta;
                PitchDelta = pitchDelta;
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

        private void Update()
        {
            // Solo el owner acumula input de "single frame" como el jump.
            if (base.IsOwner && _jumpAction.WasPressedThisFrame())
                _jumpQueued = true;
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
            if (!base.IsOwner)
                return default;

            Vector2 move = _moveAction.ReadValue<Vector2>();
            bool sprint = _sprintAction.IsPressed();
            bool jump = _jumpQueued;
            _jumpQueued = false;

            Vector2 look = _lookAction.ReadValue<Vector2>();
            float yawDelta = look.x * _mouseSensitivity;
            float pitchDelta = -look.y * _mouseSensitivity;

            return new ReplicateData(move, jump, sprint, yawDelta, pitchDelta);
        }

        [Replicate]
        private void RunInputs(ReplicateData data, ReplicateState state = ReplicateState.Invalid, FishNet.Transporting.Channel channel = FishNet.Transporting.Channel.Unreliable)
        {
            float delta = (float)base.TimeManager.TickDelta;
            transform.Rotate(Vector3.up, data.YawDelta);

            if (_cameraLook != null)
                _cameraLook.AddPitch(data.PitchDelta);
            // Movimiento horizontal relativo a la orientación del jugador.
            float speed = _moveSpeed * (data.Sprint ? _sprintMultiplier : 1f);
            Vector3 horizontal = (transform.right * data.Move.x + transform.forward * data.Move.y) * speed;

            // Gravedad y salto en el eje vertical, integrados aparte del horizontal.
            if (_controller.isGrounded)
            {
                _verticalVelocity.y = -1f; // pequeño valor negativo para mantener grounded check estable
                if (data.Jump)
                    _verticalVelocity.y = _jumpForce;
            }
            else
            {
                _verticalVelocity.y += _gravity * delta;
            }

            Vector3 motion = (horizontal + _verticalVelocity) * delta;
            _controller.Move(motion);
        }

        public override void CreateReconcile()
        {
            ReconcileData rd = new ReconcileData(transform.position, _verticalVelocity, transform.rotation);
            ReconcileState(rd);
        }

        [Reconcile]
        private void ReconcileState(ReconcileData data, FishNet.Transporting.Channel channel = FishNet.Transporting.Channel.Unreliable)
        {
            // CharacterController debe desactivarse antes de forzar posición/rotación
            // y reactivarse después, o el estado interno queda desincronizado (ver docs Fish-Net).
            _controller.enabled = false;
            transform.SetPositionAndRotation(data.Position, data.Rotation);
            _verticalVelocity = data.VerticalVelocity;
            _controller.enabled = true;
        }
    }
}
