using Lean.Transition;
using System.Collections;
using Undercooked.Model;
using Undercooked.Player;
using UnityEngine;
using UnityEngine.Assertions;
using Slider = UnityEngine.UI.Slider;

namespace Undercooked.Appliances
{
    public class ChoppingBoard : Interactable
    {
        [SerializeField] private Transform knife;
        [SerializeField] private Slider slider;
        
        private float _finalProcessTime = 1;
        private float _currentProcessTime = 0;
        private Coroutine _chopCoroutine;
        public Ingredient _ingredient;
        private bool _isChopping;

        public float choppingPercentage => _currentProcessTime / _finalProcessTime;

        public delegate void ChoppingStatus(PlayerController playerController, bool completed);
        public static event ChoppingStatus OnChoppingStart;
        public static event ChoppingStatus OnChopping;
        public static event ChoppingStatus OnChoppingStop;

        public static event ChoppingStatus OnChoppingPositive;
        public static event ChoppingStatus OnChoppingNegative;

        public GameObject Cebolla;

        public void Reset()
        {
            slider.gameObject.SetActive(false);
            _isChopping = false;
            if (_chopCoroutine != null) StopCoroutine(_chopCoroutine);
            _chopCoroutine = null;

            // 1) Destroy whatever was sitting on the board…
            if (CurrentPickable != null)
                Destroy(CurrentPickable.gameObject);

            // 2) …then clear *every* reference to it:
            CurrentPickable = null;   // Interactable’s field
            _ingredient = null;   // your own private pointer
            LastPlayerControllerInteracting = null;

            // 3) Put the knife back
            knife.gameObject.SetActive(true);
        }

        protected override void Awake()
        {
            #if UNITY_EDITOR
                Assert.IsNotNull(slider);
                Assert.IsNotNull(slider);
            #endif
            
            base.Awake();
            slider.gameObject.SetActive(false);
        }

        public override void Interact(PlayerController playerController)
        {
            LastPlayerControllerInteracting = playerController; 
            base.Interact(playerController);
            if (CurrentPickable == null ||
                _ingredient == null ||
                _ingredient.Status != IngredientStatus.Raw)
            {
                OnChoppingNegative.Invoke(null, false);
                return;
            } 

            if (_chopCoroutine == null)
            {
                _finalProcessTime = _ingredient.ProcessTime;
                _currentProcessTime = 0f;
                slider.value = 0f;
                slider.gameObject.SetActive(true);
                OnChoppingStart?.Invoke(LastPlayerControllerInteracting, false);
                StartChopCoroutine();
                return;
            }

            if (_isChopping == false)
            {
                StartChopCoroutine();
            }
        }

        private void StartChopCoroutine()
        {
            _chopCoroutine = StartCoroutine(Chop());
        }

        private void StopChopCoroutine()
        {
            if (_chopCoroutine == null) return;  
            OnChoppingStop?.Invoke(LastPlayerControllerInteracting, false);
            _isChopping = false;
            if (_chopCoroutine != null) StopCoroutine(_chopCoroutine); 
        }

        public override void ToggleHighlightOff()
        {
            base.ToggleHighlightOff();
            StopChopCoroutine();
        }
        
        private IEnumerator Chop()
        {
            _isChopping = true;
            while (_currentProcessTime < _finalProcessTime)
            {
                OnChopping?.Invoke(LastPlayerControllerInteracting, true);
                slider.value = _currentProcessTime / _finalProcessTime;
                _currentProcessTime += Time.deltaTime;
                yield return null;
            }

            // finished
            _ingredient.ChangeToProcessed();
            slider.gameObject.SetActive(false);
            _isChopping = false;
            _chopCoroutine = null;
            OnChoppingStop?.Invoke(LastPlayerControllerInteracting, true);
        }
        
        public override bool TryToDropIntoSlot(IPickable pickableToDrop)
        {
            if (pickableToDrop is Ingredient)
            {
                bool res = TryDropIfNotOccupied(pickableToDrop);
                if(!res)
                    OnChoppingNegative.Invoke(null, false);

                return res;
            }
            OnChoppingNegative.Invoke(null, false);
            return false;
        }

        public override IPickable TryToPickUpFromSlot(IPickable playerHoldPickable)
        {
            // only allow Pickup after we finish chopping the ingredient. Essentially locking it in place.
            if (CurrentPickable == null)
            {
                OnChoppingNegative.Invoke(null, false);
                return null;
            }
            if (_chopCoroutine != null)
            { 
                OnChoppingNegative.Invoke(null, false);
                return null;
            }
            
            var output = CurrentPickable;
            _ingredient = null;
            var interactable = CurrentPickable as Interactable;
            interactable?.ToggleHighlightOff();
            CurrentPickable = null;
            knife.gameObject.SetActive(true);
            OnChoppingPositive.Invoke(null, false);
            return output;
        }
        
        private bool TryDropIfNotOccupied(IPickable pickable)
        {
            if (CurrentPickable != null) return false;
            CurrentPickable = pickable;
            _ingredient = pickable as Ingredient;
            if (_ingredient == null) return false;

            _finalProcessTime = _ingredient.ProcessTime;
            
            CurrentPickable.gameObject.transform.SetParent(Slot);
            CurrentPickable.gameObject.transform.SetPositionAndRotation(Slot.position, Quaternion.identity);
            knife.gameObject.SetActive(false);
            return true;
        }
    }
}
