using System.Collections;
using Lean.Transition;
using Undercooked.Appliances;
using Undercooked.Model;
using Undercooked.UI;
using UnityEngine;

using Unity.MLAgents;

namespace Undercooked.Player
{
    public class PlayerController : MonoBehaviour
    {
        [SerializeField] private Color playerColor;
        [SerializeField] private Transform selector;
        [SerializeField] private Material playerUniqueColorMaterial;

        [Header("Physics")]
        [SerializeField] private Rigidbody playerRigidbody;

        [Header("Animation")]
        [SerializeField] private Animator animator;
        private readonly int _isCleaningHash = Animator.StringToHash("isCleaning");
        private readonly int _hasPickupHash = Animator.StringToHash("hasPickup");
        private readonly int _isChoppingHash = Animator.StringToHash("isChopping");
        private readonly int _velocityHash = Animator.StringToHash("velocity");

        // Dashing
        [SerializeField] private float dashForce = 900f;
        private bool _isDashing = false;
        private bool _isDashingPossible = true;
        private readonly WaitForSeconds _dashDuration = new WaitForSeconds(0.17f);
        private readonly WaitForSeconds _dashCooldown = new WaitForSeconds(0.07f);

        [Header("Movement Settings")]
        [SerializeField] private float movementSpeed = 5f;

        private InteractableController _interactableController;
        private bool _isActive = true;
        private IPickable _currentPickable;
        private Vector3 _inputDirection;

        [SerializeField] private Transform slot;
        [SerializeField] private ParticleSystem dashParticle;
        [SerializeField] private Transform knife;

        [Header("Audio")]
        [SerializeField] private AudioClip dashAudio;
        [SerializeField] private AudioClip pickupAudio;
        [SerializeField] private AudioClip dropAudio;

        private void Awake()
        {
            _interactableController = GetComponentInChildren<InteractableController>();
            knife.gameObject.SetActive(false);
            SetPlayerUniqueColor(playerColor);
        }

        private void SetPlayerUniqueColor(Color color)
        {
            selector.GetComponent<MeshRenderer>().material.color = color;
            playerUniqueColorMaterial.color = color;
        }

        public void ActivatePlayer()
        {
            _isActive = true;
            selector.gameObject.SetActive(true);
        }

        public void DeactivatePlayer()
        {
            _isActive = false;
            animator.SetFloat(_velocityHash, 0f);
            selector.gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            SubscribeInteractableEvents();
        }

        private void OnDisable()
        {
            UnsubscribeInteractableEvents();
        }

        private void SubscribeInteractableEvents()
        {
            ChoppingBoard.OnChoppingStart += HandleChoppingStart;
            ChoppingBoard.OnChoppingStop += HandleChoppingStop;
            Sink.OnCleanStart += HandleCleanStart;
            Sink.OnCleanStop += HandleCleanStop;
        }

        private void UnsubscribeInteractableEvents()
        {
            ChoppingBoard.OnChoppingStart -= HandleChoppingStart;
            ChoppingBoard.OnChoppingStop -= HandleChoppingStop;
            Sink.OnCleanStart -= HandleCleanStart;
            Sink.OnCleanStop -= HandleCleanStop;
        }

        private void HandleCleanStart(PlayerController playerController)
        {
            if (!Equals(playerController)) return;
            animator.SetBool(_isCleaningHash, true);
        }

        private void HandleCleanStop(PlayerController playerController)
        {
            if (!Equals(playerController)) return;
            animator.SetBool(_isCleaningHash, false);
        }

        private void HandleChoppingStart(PlayerController playerController)
        {
            if (!Equals(playerController)) return;
            animator.SetBool(_isChoppingHash, true);
            knife.gameObject.SetActive(true);
        }

        private void HandleChoppingStop(PlayerController playerController)
        {
            if (!Equals(playerController)) return;
            animator.SetBool(_isChoppingHash, false);
            knife.gameObject.SetActive(false);
        }

        /// <summary>
        /// This method receives the ML Agent's outputs.
        /// - moveInput: a Vector2 (x, y) for movement.
        /// - dashAction: int flag for dash (nonzero triggers dash).
        /// - pickupAction: int flag for pick up (nonzero triggers pick up).
        /// - interactAction: int flag for interact (nonzero triggers interact).
        /// </summary>
        public void SetMLAgentInput(Vector2 moveInput, float dashInput, float pickupInput, float interactAction)
        {
            // Update movement direction from neural network output.
            _inputDirection = new Vector3(moveInput.x, 0f, moveInput.y);

            // Check and trigger individual actions.
            if (dashInput >= 0.5f)
            {
                HandleDash();
            }
            if (pickupInput >= .5f)
            {
                HandlePickUp();
            }
            if (interactAction >= .05f)
            {
                HandleInteract();
            }
        }

        public void HandleDash()
        {
            if (!_isDashingPossible) return;
            StartCoroutine(Dash());
        }

        private IEnumerator Dash()
        {
            _isDashingPossible = false;
            playerRigidbody.AddRelativeForce(dashForce * Vector3.forward);
            dashParticle.Play();
            dashParticle.PlaySoundTransition(dashAudio);

            yield return new WaitForFixedUpdate();
            _isDashing = true;
            yield return _dashDuration;
            _isDashing = false;
            yield return _dashCooldown;
            _isDashingPossible = true;
        }

        public void HandlePickUp()
        {
            var interactable = _interactableController.CurrentInteractable;

            // If not holding an item, try to pick one up.
            if (_currentPickable == null)
            {
                _currentPickable = interactable as IPickable;
                if (_currentPickable != null)
                {
                    animator.SetBool(_hasPickupHash, true);
                    this.PlaySoundTransition(pickupAudio);
                    _currentPickable.Pick();
                    _interactableController.Remove(_currentPickable as Interactable);
                    _currentPickable.gameObject.transform.SetPositionAndRotation(slot.position, Quaternion.identity);
                    _currentPickable.gameObject.transform.SetParent(slot);
                    return;
                }

                // If the interactable isn t directly pickable.
                _currentPickable = interactable?.TryToPickUpFromSlot(_currentPickable);
                if (_currentPickable != null)
                {
                    animator.SetBool(_hasPickupHash, true);
                    this.PlaySoundTransition(pickupAudio);
                }

                _currentPickable?.gameObject.transform.SetPositionAndRotation(slot.position, Quaternion.identity);
                _currentPickable?.gameObject.transform.SetParent(slot);
                return;
            }

            // If already carrying an item, attempt to drop it.
            if (interactable == null || interactable is IPickable)
            {
                animator.SetBool(_hasPickupHash, false);
                this.PlaySoundTransition(dropAudio);
                _currentPickable.Drop();
                _currentPickable = null;
                return;
            }

            // If an interactable is present, try to drop the held item into it.
            bool dropSuccess = interactable.TryToDropIntoSlot(_currentPickable);
            if (!dropSuccess) return;

            animator.SetBool(_hasPickupHash, false);
            this.PlaySoundTransition(dropAudio);
            _currentPickable = null;
        }

        public void HandleInteract()
        {
            _interactableController.CurrentInteractable?.Interact(this);
        }

        private void Update()
        {
            SetMLAgentInput(new Vector2(0, .05f), 0, 1, 1);
            // Movement is driven externally via ML agent inputs.
            if (!_isActive) return;
        }

        private void FixedUpdate()
        {
            if (!_isActive) return;
            MoveThePlayer();
            AnimatePlayerMovement();
            TurnThePlayer();
        }

        private void MoveThePlayer()
        {
            if (_isDashing)
            {
                float currentVelocity = playerRigidbody.linearVelocity.magnitude;
                Vector3 inputNormalized = _inputDirection.normalized;
                if (inputNormalized == Vector3.zero)
                {
                    inputNormalized = transform.forward;
                }
                playerRigidbody.linearVelocity = inputNormalized * currentVelocity;
            }
            else
            {
                playerRigidbody.linearVelocity = _inputDirection.normalized * movementSpeed;
            }
        }

        private void TurnThePlayer()
        {
            if (!(playerRigidbody.linearVelocity.magnitude > 0.1f) || _inputDirection == Vector3.zero) return;
            Quaternion newRotation = Quaternion.LookRotation(_inputDirection);
            transform.rotation = Quaternion.Slerp(transform.rotation, newRotation, Time.deltaTime * 15f);
        }

        private void AnimatePlayerMovement()
        {
            animator.SetFloat(_velocityHash, _inputDirection.sqrMagnitude);
        }
    }
}
