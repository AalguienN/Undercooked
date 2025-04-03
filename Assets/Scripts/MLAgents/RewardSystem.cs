using UnityEngine;

namespace Undercooked
{
    public class RewardSystem : MonoBehaviour
    {
        // The static instance of the RewardSystem.
        public static RewardSystem Instance { get; private set; }

        public float totalReward = 0.0f;

        public float cutRew = 0.0f;
        public float cookRew = 0.0f;
        public float burnRew  = 0.0f;
        public float deliverRew = 0.0f;
        public float cleanRew = 0.0f;

        // Awake is called when the script instance is being loaded.
        private void Awake()
        {
            // If an instance already exists and it's not this, destroy this instance.
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            // Set the instance to this and optionally make it persist across scene loads.
            Instance = this;
        }

        // Example reward methods
        public void GiveReward(float r)
        {
            totalReward += r;
        }

        public void BurnReward()
        {
            totalReward += burnRew;
        }

        public void CutReward()
        {
            totalReward += cutRew;
        }

        public void CookReward()
        { 
            totalReward += cookRew;
        }

        public void CleanReward()
        {
            totalReward += cleanRew;
        }
    }
}
