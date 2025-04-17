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
                    foreach (RewardSystem rs in FindObjectsByType<RewardSystem>(sortMode: FindObjectsSortMode.None)) { 
                        rs.cutRew  = 0.1f;
                        rs.cookRew  = 0.0f;
                        rs.burnRew  = 0.0f;
                        rs.cleanRew = 0.0f;
                        rs.deliverRew = 0.0f;
                    }
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
