using Undercooked.Model;
using Undercooked.Player;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;
using Undercooked.Managers;
using Undercooked.Appliances;
using System.Linq;

namespace Undercooked
{
    public class PlayerAgent : Agent
    {
        [Header("Debug")]
        public bool actionDebug = false;

        // Cool-down lengths (in FixedUpdate steps)
        private const int DashCdFrames = 0;
        private const int PickCdFrames = 0;
        private const int InteractCdFrames = 0;

        // Cool-down counters
        private int dashCd = 0;
        private int pickCd = 0;
        private int interactCd = 0;

        // Rising-edge helper
        private readonly int[] lastDiscrete = new int[3];

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
            AddObjectPositionObservation<IngredientCrate>(sensor, pos);
            crate = FindFirstObjectByType<IngredientCrate>();

            AddObjectPositionObservation<ChoppingBoard>(sensor, pos);
            board = FindFirstObjectByType<ChoppingBoard>();
            sensor.AddObservation(board.choppingPercentage);

            AddObjectPositionObservation<Hob>(sensor, pos);
            AddObjectPositionObservation<CookingPot>(sensor, pos);
            CookingPot Cpot = FindFirstObjectByType<CookingPot>();
            sensor.AddObservation((float)(Cpot?.Ingredients?.Count ?? 0));

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
                    AddReward(rewardAmount);
                    if (actionDebug)
                        Debug.Log($"[MoveTowards] +{rewardAmount:F4} (dot={dot:F2}) → {targetWorldPos}");
                }
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

            movementInput.x = a.ContinuousActions[0];
            movementInput.y = a.ContinuousActions[1];

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

            var sceneIngredients = FindObjectsByType<Ingredient>(FindObjectsSortMode.None);
            int rawCount = sceneIngredients.Count(ing => ing.Status == IngredientStatus.Raw);
            var rawIngridients = sceneIngredients.Where(ing => ing.Status == IngredientStatus.Raw).ToList();

            // 4) Rewards
            if (holdingNothing && boardEmpty && rawCount == 0)
                RewardForMovingTowards(crate.transform.position, 0.8f, 0.002f);

            // Raw in hand → empty board
            if (isHoldRaw && boardEmpty)
                RewardForMovingTowards(board.transform.position, 0.8f, 0.001f);

            // Empty-handed → board empty, go to closest raw ingridient
            var nearestRaw = FindObjectsByType<Ingredient>(FindObjectsSortMode.None).Where(ing => ing.Status == IngredientStatus.Raw)
            .OrderBy(ing => Vector3.SqrMagnitude(ing.transform.position - transform.position)).FirstOrDefault();

            if (holdingNothing && boardEmpty && nearestRaw != null)
                RewardForMovingTowards(nearestRaw.transform.position, 0.8f, 0.001f);
            
            // Penalty for board already processed
            if (boardHasProcessed)
                AddReward(-0.0001f);
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
