using Undercooked.Model;
using UnityEngine;

namespace Undercooked.Appliances
{
    public class Countertop : Interactable
    {
        public void Reset()
        {
            if (CurrentPickable != null)
            {
                switch (CurrentPickable)
                {
                    // 1) If it's a loose Ingredient, destroy it outright:
                    case Ingredient ing:
                        Destroy(ing.gameObject);
                        break;
                }

                // 5) Finally, wipe out the reference so Interactable logic never sees it again:
                CurrentPickable = null;
            }
        }

        public override bool TryToDropIntoSlot(IPickable pickableToDrop)
        {
            if (CurrentPickable == null) return TryDropIfNotOccupied(pickableToDrop);

            return CurrentPickable switch
            {
                CookingPot cookingPot => cookingPot.TryToDropIntoSlot(pickableToDrop),
                Ingredient ingredient => ingredient.TryToDropIntoSlot(pickableToDrop),
                Plate plate => plate.TryToDropIntoSlot(pickableToDrop),
                _ => false
            };
        }

        public override IPickable TryToPickUpFromSlot(IPickable playerHoldPickable)
        {
            if (CurrentPickable == null) return null;

            var output = CurrentPickable;
            var interactable = CurrentPickable as Interactable;
            interactable?.ToggleHighlightOff();
            CurrentPickable = null;
            return output;
        }

        private bool TryDropIfNotOccupied(IPickable pickable)
        {
            if (CurrentPickable != null) return false;
            
            CurrentPickable = pickable;
            CurrentPickable.gameObject.transform.SetParent(Slot);
            CurrentPickable.gameObject.transform.SetPositionAndRotation(Slot.position, Quaternion.identity);
            return true;
        }
    }
}
