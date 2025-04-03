using UnityEngine;

namespace Undercooked
{
    public class FunctionalityManager : MonoBehaviour
    {
        // Singleton instance for static access.
        public static FunctionalityManager Instance { get; private set; }

        [HideInInspector]
        public bool foodBurning = true;

        [Header("Functionality Toggles")]
        [SerializeField] private bool _CuttingTraining = true;

        // Static properties to access the toggles
        public static bool CuttingTraining
        {
            get { return Instance._CuttingTraining; }
            set 
            {
                if (value) 
                {
                    // Set the functionalities you want to work on this configuration
                    Instance.foodBurning = false;

                    // Set the reward values you want for certain actions, negativa, 0, positive
                    RewardSystem.Instance.cutRew  = 0.1f;
                    RewardSystem.Instance.cookRew  = 0.0f;
                    RewardSystem.Instance.burnRew  = 0.0f;
                    RewardSystem.Instance.cleanRew = 0.0f;
                    RewardSystem.Instance.deliverRew = 0.0f;
                }
                Instance._CuttingTraining = value; 
            }
        }


        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }
    }
}
