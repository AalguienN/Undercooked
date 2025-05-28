using Undercooked.Model;
using Undercooked.Player;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;
using Undercooked.Managers;
using Undercooked.Appliances;
using System.Linq;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Undercooked
{
    public class PlayerAgent : Agent
    {
        [Header("Debug")]
        public bool actionDebug = false;

        // Cool-down lengths (in FixedUpdate steps)
        private const int DashCdFrames = 1000;
        private const int PickCdFrames = 20;
        private const int InteractCdFrames = 10;

        // Cool-down counters
        private int dashCd = 1000;
        private int pickCd = 20;
        private int interactCd = 1;

        // Rising-edge helper
        private readonly int[] lastDiscrete = new int[4];

        // Controllers & helpers
        private PlayerController player_controller;
        private InteractableController interactable_controller;
        public ObjectRandomizer objectRandomizer;
        private OrderManager OM;

        // Input buffers
        private Vector2 movementInput;
        private bool dashInput;
        private bool pickupInput;
        private bool interactionInput;
        private Vector3 oldPos = Vector3.zero;

        // Buffer sensors
        public BufferSensorComponent ingredientBuffer;
        public BufferSensorComponent plateBuffer;
        public BufferSensorComponent orderBuffer;
        public BufferSensorComponent ingredientCrateBuffer;

        ChoppingBoard board;
        IngredientCrate crate;
        CookingPot pot;
        Hob hob;

        RewardSystem rewardSystem;
        private void Awake()
        {
            player_controller = GetComponent<PlayerController>();
            interactable_controller = GetComponentInChildren<InteractableController>();

            foreach (var b in GetComponents<BufferSensorComponent>())
            {
                switch (b.SensorName)
                {
                    case "IngredientesSensor": ingredientBuffer = b; break;
                    case "PlatesSensor": plateBuffer = b; break;
                    case "IngredientCrateBuffer": ingredientCrateBuffer = b; break;
                    default:
                        Debug.LogError($"{b.SensorName}: sensor name not recognised!");
                        break;
                }
            }

            rewardSystem = GetComponent<RewardSystem>();
        }

        public override void OnEpisodeBegin()
        {
            // Reset controllers & environment
            player_controller.Reset();
            interactable_controller.Reset();
            OM = FindAnyObjectByType<OrderManager>();
            OM.Reset();

            // --- Unity 6: FindObjectsByType<T>(FindObjectsSortMode.None) ---
            foreach (CookingPot cp in FindObjectsByType<CookingPot>(FindObjectsSortMode.None))
                cp.Reset();
            foreach (Hob h in FindObjectsByType<Hob>(FindObjectsSortMode.None))
                h.Reset();
            foreach (ChoppingBoard cb in FindObjectsByType<ChoppingBoard>(FindObjectsSortMode.None))
                cb.Reset();
            foreach (Countertop ct in FindObjectsByType<Countertop>(FindObjectsSortMode.None))
                ct.Reset();
            FindAnyObjectByType<DishTray>()?.Reset();
            // ------------------------------------------------------------

            // Destroy stray Ingredients and re-randomize
            foreach (Ingredient ing in FindObjectsByType<Ingredient>(FindObjectsSortMode.None))
                Destroy(ing.gameObject);
            objectRandomizer.Randomize();

            // Reset cooldowns & edge-detector
            dashCd = pickCd = interactCd = 0;
            lastDiscrete[0] = lastDiscrete[1] = lastDiscrete[2] = 0;
            oldPos = Vector3.zero;
        }

        public override void CollectObservations(VectorSensor sensor)
        {
            Vector3 pos = transform.position;
            sensor.AddObservation(pos.x - oldPos.x);
            sensor.AddObservation(pos.z - oldPos.z);
            oldPos = pos;

            var carried = player_controller.HeldObject;
            if (carried == null)
            {
                sensor.AddObservation(-1f);
                sensor.AddObservation(-1f);
            }
            else if (carried is Ingredient i)
            {
                sensor.AddObservation(0f);
                sensor.AddObservation((float)i.Status);
            }
            else if (carried is Plate p)
            {
                sensor.AddObservation(1f);
                sensor.AddObservation(Mathf.Clamp01(p.Ingredients.Count / 3f));
            }
            else if (carried is CookingPot pot)
            {
                sensor.AddObservation(2f);
                sensor.AddObservation(pot.Ingredients.Count);
            }
            else
            {
                sensor.AddObservation(-1f);
                sensor.AddObservation(-1f);
            }

            // Add the position of everything else
            crate = FindFirstObjectByType<IngredientCrate>();
            AddObjectPositionObservation<IngredientCrate>(sensor, pos);

            AddObjectPositionObservation<ChoppingBoard>(sensor, pos);
            board = FindFirstObjectByType<ChoppingBoard>();

            if (board._ingredient == null)
                sensor.AddObservation(0);
            else if (board._ingredient.Status == IngredientStatus.Raw)
                sensor.AddObservation(1);
            else if (board._ingredient.Status == IngredientStatus.Processed)
                sensor.AddObservation(2);

            sensor.AddObservation(board.choppingPercentage);

            AddObjectPositionObservation<Hob>(sensor, pos);
            hob = FindFirstObjectByType<Hob>();
            if (hob?.CurrentPickable == null)
                sensor.AddObservation(0);
            else 
                sensor.AddObservation(1);


            AddObjectPositionObservation<CookingPot>(sensor, pos);
            pot = FindFirstObjectByType<CookingPot>();
            sensor.AddObservation((float)(pot?.Ingredients?.Count ?? 0));

            AddObjectPositionObservation<DeliverCountertop>(sensor, pos);

            //Añadir observacion del plato, y si tiene algo dentro
            Plate plate = FindFirstObjectByType<Plate>();
            if (plate != null)
            {
                Vector3 ppos = plate.transform.position;
                sensor.AddObservation(new float[] {
                    ppos.x - pos.x, ppos.z - pos.z, plate.Ingredients.Count
                });
            }
            else { // por si quitamos el plato en algun momento del entrenamiento para que no moleste, poner los valores a 0
                sensor.AddObservation(new float[] {
                    0,0,0
                });
            }
            
            // ingridient positions
            foreach (Ingredient ing in FindObjectsByType<Ingredient>(FindObjectsSortMode.None))
            {
                Vector3 ipos = ing.transform.position;
                ingredientBuffer.AppendObservation(new float[] {
                    ipos.x - pos.x, ipos.z - pos.z, (float)ing.Status
                });
            }
        }
        private void RewardForMovingTowards(Vector3 targetWorldPos, float dotThreshold, float rewardAmount)
        {
            // Build 2D vectors for horizontal plane
            Vector2 moveDir = new Vector2(movementInput.x, movementInput.y);
            Vector2 toTarget = new Vector2(
                targetWorldPos.x - transform.position.x,
                targetWorldPos.z - transform.position.z
            );

            // Only if both vectors are non-zero
            if (moveDir.sqrMagnitude > 1e-4f && toTarget.sqrMagnitude > 1e-4f)
            {
                float dot = Vector2.Dot(moveDir.normalized, toTarget.normalized);
                if (dot >= dotThreshold)
                {
                    rewardSystem.Add(rewardAmount);
                    if (actionDebug)
                        Debug.Log($"[MoveTowards] +{rewardAmount:F4} (dot={dot:F2}) → {targetWorldPos}");
                }
            }
        }



        private void RewardForMovingAway(Vector3 targetWorldPos, float dotThreshold, float rewardAmount)
        {
            // Build 2D vectors for horizontal plane
            Vector2 moveDir = new Vector2(movementInput.x, movementInput.y);
            Vector2 toTarget = new Vector2(
                targetWorldPos.x - transform.position.x,
                targetWorldPos.z - transform.position.z
            );
            toTarget = -toTarget;
            // Only if both vectors are non-zero
            if (moveDir.sqrMagnitude > 1e-4f && toTarget.sqrMagnitude > 1e-4f)
            {
                float dot = Vector2.Dot(moveDir.normalized, toTarget.normalized);
                if (dot >= dotThreshold)
                {
                    rewardSystem.Add(rewardAmount);
                    if (actionDebug)
                        Debug.Log($"[MoveTowards] +{rewardAmount:F4} (dot={dot:F2}) → {targetWorldPos}");
                }
            }
        }

        private void RewardForProximity(Vector3 targetPos, float minDistance, float maxDistance, float maxReward, float penalty)
        {
            float dist = Vector3.Distance(transform.position, targetPos);

            if (dist <= minDistance)
            {
                // Está dentro del rango óptimo: máxima recompensa
                rewardSystem.Add(maxReward);
                if (actionDebug)
                    Debug.Log($"[Proximity] +{maxReward:F4} (dist={dist:F2}) → {targetPos}");
            }
            else if (dist <= maxDistance)
            {
                // Interpolación lineal entre min y max reward
                float t = 1 - (dist - minDistance) / (maxDistance - minDistance);
                float reward = maxReward * t;
                rewardSystem.Add(reward);
                if (actionDebug)
                    Debug.Log($"[Proximity] +{reward:F4} (dist={dist:F2}) → {targetPos}");
            }
            else
            {
                // Está fuera del rango máximo → penalización
                rewardSystem.Add(-penalty);
                if (actionDebug)
                    Debug.Log($"[Proximity] -{penalty:F4} (dist={dist:F2}) → {targetPos}");
            }
        }


        private void RewardForLookingAt(Vector3 targetWorldPos, float dotThreshold, float rewardAmount)
        {
            Vector3 forward = transform.forward;
            Vector3 toTarget = (targetWorldPos - transform.position).normalized;

            float dot = Vector3.Dot(forward, toTarget);
            if (dot >= dotThreshold)
            {
                rewardSystem.Add(rewardAmount);
                if (actionDebug)
                    Debug.Log($"[LookAt] +{rewardAmount:F4} (dot={dot:F2}) → {targetWorldPos}");
            }
        }

        private void AddObjectPositionObservation<T>(VectorSensor sensor, Vector3 refPos) where T : MonoBehaviour
        {
            var obj = FindFirstObjectByType<T>();
            if (obj != null)
            {
                Vector3 p = obj.transform.position;
                sensor.AddObservation(p.x - refPos.x);
                sensor.AddObservation(p.z - refPos.z);
            }
            else
            {
                sensor.AddObservation(0f);
                sensor.AddObservation(0f);
            }
        }

        public override void OnActionReceived(ActionBuffers a)
        {
            dashCd = Mathf.Max(0, dashCd - 1);
            pickCd = Mathf.Max(0, pickCd - 1);
            interactCd = Mathf.Max(0, interactCd - 1);

            //movementInput.x = a.ContinuousActions[0];
            //movementInput.y = a.ContinuousActions[1];

            var d = a.DiscreteActions;
            bool dashPressed = d[0] == 1 && lastDiscrete[0] == 0 && dashCd == 0;
            bool pickPressed = d[1] == 1 && lastDiscrete[1] == 0 && pickCd == 0;
            bool intPressed = d[2] == 1 && lastDiscrete[2] == 0 && interactCd == 0;

            dashInput = dashPressed;
            pickupInput = pickPressed;
            interactionInput = intPressed;

            if (dashPressed) dashCd = DashCdFrames;
            if (pickPressed) pickCd = PickCdFrames;
            if (intPressed) interactCd = InteractCdFrames;

            lastDiscrete[0] = d[0];
            lastDiscrete[1] = d[1];
            lastDiscrete[2] = d[2];

            lastDiscrete[3] = d[3];

            switch (d[3])
            {
                case 0:
                    movementInput = new Vector2(0, 1);
                    break;
                case 1:
                    movementInput = new Vector2(1, 1);
                    break;
                case 2:
                    movementInput = new Vector2(1, 0);
                    break;
                case 3:
                    movementInput = new Vector2(1, -1);
                    break;
                case 4:
                    movementInput = new Vector2(0, -1);
                    break;
                case 5:
                    movementInput = new Vector2(-1, -1);
                    break;
                case 6:
                    movementInput = new Vector2(-1, 0);
                    break;
                case 7:
                    movementInput = new Vector2(-1, 1);
                    break;
                case 8:
                    movementInput = new Vector2(0,0);
                    break;
            } 

            if (rewardSystem.UsarRailes) {
                Railes();
            }
        }
        private void Railes() {
            // 1) Held‐object & status
            var heldObj = player_controller.HeldObject;
            var heldIng = heldObj as Ingredient;
            bool holdingNothing = heldObj == null;
            bool holdingIngredient = heldIng != null;
            bool isHoldRaw = holdingIngredient && heldIng.Status == IngredientStatus.Raw;
            bool isHoldProcessed = holdingIngredient && heldIng.Status == IngredientStatus.Processed;

            // 2) Cutting‐board & its single ingredient var
            var board = FindFirstObjectByType<ChoppingBoard>();
            var boardIngredient = board != null
                ? board._ingredient as Ingredient
                : null;

            // 3) State‐flags for the board
            bool boardEmpty = board != null && boardIngredient == null;
            bool boardHasRaw = boardIngredient != null
                                        && boardIngredient.Status == IngredientStatus.Raw;
            bool boardHasProcessed = boardIngredient != null
                                        && boardIngredient.Status == IngredientStatus.Processed;

            bool coockingPotFull = pot?.Ingredients.Count == 3;
            bool coockingPotEmpty = pot?.Ingredients.Count == 0;

            var sceneIngredients = FindObjectsByType<Ingredient>(FindObjectsSortMode.None);
            int rawCount = sceneIngredients.Count(ing => ing.Status == IngredientStatus.Raw);
            var rawIngridients = sceneIngredients.Where(ing => ing.Status == IngredientStatus.Raw).ToList();

            int procesedCount = sceneIngredients.Count(ing => ing.Status == IngredientStatus.Processed);
            var procesedIngridients = sceneIngredients.Where(ing => ing.Status == IngredientStatus.Processed).ToList();


            // 4) Rewards
            //float distToCrate = (transform.position - crate.transform.position).magnitude;

            //if (holdingNothing && boardEmpty)
            //{
            //    RewardForMovingTowards(crate.transform.position, 0.8f, 0.004f);
            //    RewardForMovingAway(crate.transform.position, -0.5f, -0.006f);
            //    RewardForLookingAt(crate.transform.position, 0.9f, 0.001f);
            //    RewardForProximity(crate.transform.position, 1f, 3f, 0.01f, 0.005f);
            //}

            if (holdingNothing && boardEmpty && rawCount == 0 && procesedCount == 0)
            {
                RewardForMovingTowards(crate.transform.position, 0.8f, 0.0004f);
                RewardForMovingAway(crate.transform.position, -0.8f, -0.01f);
                RewardForLookingAt(crate.transform.position, 0.9f, 0.0001f);
                RewardForProximity(crate.transform.position, 1f, 5f, 0.01f, 0.005f);
            }


            // Raw in hand → empty board
            if (isHoldRaw && boardEmpty && procesedCount == 0)
            {
                RewardForMovingTowards(board.transform.position, 0.8f, 0.01f);
                RewardForMovingAway(board.transform.position, -0.8f, -0.01f);
                RewardForLookingAt(board.transform.position, 0.9f, 0.0001f);
                RewardForProximity(board.transform.position, 1f, 8f, 0.01f, 0.01f);
            }

            // Empty-handed → board empty, go to closest raw ingridient
            var nearestRaw = FindObjectsByType<Ingredient>(FindObjectsSortMode.None)
                .Where(ing => ing.Status == IngredientStatus.Raw)
                .OrderBy(ing => Vector3.SqrMagnitude(ing.transform.position - transform.position)).FirstOrDefault();


            if (holdingNothing && boardEmpty && nearestRaw != null && procesedCount == 0)
            {
                RewardForMovingTowards(nearestRaw.transform.position, 0.8f, 0.000025f);
                RewardForMovingAway(nearestRaw.transform.position, -0.5f, -0.000025f);
                //RewardForLookingAt(nearestRaw.transform.position, 0.9f, 0.001f);
            }
            // Penalty for board already processed
            if (boardHasProcessed)
                rewardSystem.Add(-0.001f);

            //if (hob?.CurrentPickable == null)
            //    rewardSystem.Add(-1f);

            if (isHoldProcessed)
            {
                RewardForMovingTowards(pot.transform.position, 0.8f, 0.01f);
                RewardForMovingAway(pot.transform.position, -0.5f, -0.01f);
                RewardForLookingAt(pot.transform.position, 0.9f, 0.001f);
                RewardForProximity(pot.transform.position, 1f, 5f, 0.01f, 0.01f);

            }

            var nearestProc = FindObjectsByType<Ingredient>(FindObjectsSortMode.None).Where(ing => ing.Status == IngredientStatus.Processed)
            .OrderBy(ing => Vector3.SqrMagnitude(ing.transform.position - transform.position)).FirstOrDefault();

            if (holdingNothing && !coockingPotFull && nearestProc != null && procesedCount > 0)
            {
                RewardForMovingTowards(nearestProc.transform.position, 0.8f, 0.0005f);
                RewardForMovingAway(nearestProc.transform.position, -0.5f, -0.0005f);
                RewardForLookingAt(nearestProc.transform.position, 0.9f, 0.001f);
                RewardForProximity(nearestProc.transform.position, 1f, 5f, 0.01f, 0.008f);

            }

            //var carried = player_controller.HeldObject;
            //if (carried is CookingPot)
            //{
            //    rewardSystem.Add(-0.5f);
            //}
        }

        public override void WriteDiscreteActionMask(IDiscreteActionMask m)
        {
            m.SetActionEnabled(0, 1, player_controller.isDashingPossible && dashCd == 0);
            bool canPickOrDrop =
                (interactable_controller.CurrentInteractable != null ||
                 player_controller.HeldObject != null) && pickCd == 0;
            m.SetActionEnabled(1, 1, canPickOrDrop);
            bool canInteract = interactable_controller.CurrentInteractable != null && interactCd == 0;
            m.SetActionEnabled(2, 1, canInteract);
        }

        private void Update()
        {
            player_controller.SetMLAgentInput(
                movementInput, dashInput, pickupInput, interactionInput);
        }
    }
}
