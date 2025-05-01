using System.Collections.Generic;
using JetBrains.Annotations;
using Undercooked.Model;
using UnityEngine;

namespace Undercooked.Player
{
    public class InteractableController : MonoBehaviour
    {
        [SerializeField] private Transform playerPivot;
        private readonly HashSet<Interactable> _interactables  = new HashSet<Interactable>();

        private void Awake()
        {
            if (playerPivot == null)
            {
                playerPivot = transform;
            }
        }

        /// <summary>
        /// Get the current highlighted interactable. Null if there is none in range.
        /// </summary>
        [CanBeNull]
        public Interactable CurrentInteractable { get; private set; }

        public void Reset()
        {
            // 1) Turn off highlight on whatever was selected
            CurrentInteractable?.ToggleHighlightOff();

            // 2) Clear the runtime list
            _interactables.Clear();

            // 3) Null out the public pointer
            CurrentInteractable = null;
        }

        private void OnTriggerEnter(Collider other)
        {
            Interactable interactable = other.gameObject.GetComponent<Interactable>();
            if (!interactable) return;
            
            if (_interactables.Contains(interactable))
            {
                Debug.LogWarning($"[InteractableController] TriggerEnter on a preexisting collider {other.gameObject.name}");
                return;
            }
            _interactables.Add(interactable);
        }

        private void OnTriggerExit(Collider other)
        {
            Interactable interactable = other.GetComponent<Interactable>();
            if (interactable)
            {
                _interactables.Remove(interactable);
            }
        }
        
        public void Remove(Interactable interactable)
        {
            _interactables.Remove(interactable);
        }

        private void FixedUpdate()
        {
            Interactable closest = TryGetClosestInteractable();

            // nothing has changed
            if (closest == CurrentInteractable) { return; }
            
            // something has changed (maybe null)
            CurrentInteractable?.ToggleHighlightOff();
            CurrentInteractable = closest;

            // togglesOn only when there is a interactable near
            closest?.ToggleHighlightOn();
        }

        /// <summary>
        /// Get the closest interactables from the ones in range. Null if there none in range. 
        /// </summary>
        private Interactable TryGetClosestInteractable()
        {
            // Remove any destroyed (i == null) entries
            _interactables.RemoveWhere(i => i == null);

            float minDistance = float.MaxValue;
            Interactable closest = null;
            foreach (var interactable in _interactables)
            {
                float dist = Vector3.Distance(
                    playerPivot.position,
                    interactable.transform.position
                );
                if (dist < minDistance)
                {
                    minDistance = dist;
                    closest = interactable;
                }
            }
            return closest;
        }
    }
}
