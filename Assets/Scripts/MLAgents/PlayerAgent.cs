using Undercooked.Model;
using Undercooked.Player;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;
using Undercooked.Managers;
using Undercooked.Appliances;

namespace Undercooked
{
    public class PlayerAgent : Agent
    {
        public bool actionDebug = true; 

        PlayerController player_controller;
        public ObjectRandomizer objectRandomizer;

        private float movementAction_x;
        private float movementAction_y;
        private float dashAction;
        private float pickupAction;
        private float interactionAction;

        private Vector2 movementInput;
        private bool dashInput;
        private bool pickupInput;
        private bool interactionInput;

        private OrderManager OM;
        private bool endEpisode => orderFailedToReset == ordersFailed;
        private int orderFailedToReset = 20;
        private int ordersFailed = 0;


        public BufferSensorComponent ingredientBuffer;
        public BufferSensorComponent plateBuffer;
        public BufferSensorComponent orderBuffer;
        public BufferSensorComponent ingredientCrateBuffer;

        private void Awake()
        {
            player_controller = GetComponent<PlayerController>();
            var buffers = GetComponents<BufferSensorComponent>();
            foreach (var b in buffers) {
                switch (b.SensorName) {
                    case "IngredientesSensor":
                        ingredientBuffer = b;
                        break;
                    case "PlatesSensor":
                        plateBuffer = b;
                        break;
                    case "OrderBuffer":
                        orderBuffer = b;
                        break;
                    case "IngredientCrateBuffer":
                        ingredientCrateBuffer = b;
                        break;
                    default:
                        Debug.LogError($"{b.SensorName}: Sensor sin buffer sensor component asociado... Algo falla!");
                        break;
                }
            }
        }

        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {
            // objectRandomizer.Randomice();
            OM = FindAnyObjectByType<OrderManager>();
        }
        public override void OnEpisodeBegin()
        {
            GameObject.FindAnyObjectByType<OrderManager>().Reset();
            foreach (Ingredient ing in GameObject.FindObjectsByType<Ingredient>(sortMode:FindObjectsSortMode.None)) {
                Destroy(ing.gameObject);                
            }
            foreach (CookingPot cp in GameObject.FindObjectsByType<CookingPot>(sortMode: FindObjectsSortMode.None)) {
                cp.Reset();
            }
            foreach (Hob h in GameObject.FindObjectsByType<Hob>(sortMode: FindObjectsSortMode.None)) {
                h.Reset();
            }
            foreach (ChoppingBoard cb in GameObject.FindObjectsByType<ChoppingBoard>(sortMode: FindObjectsSortMode.None)) {
                cb.Reset();
            }
            GameObject.FindAnyObjectByType<DishTray>()?.Reset();

            GetComponent<PlayerController>().Reset();

            //foreach (ChoppingBoard cb in GameObject.FindObjectsByType<ChoppingBoard>(sortMode: FindObjectsSortMode.None))
            //foreach (ChoppingBoard cb in GameObject.FindObjectsByType<ChoppingBoard>(sortMode: FindObjectsSortMode.None))

            objectRandomizer.Randomize();
        }

        private void AddObjectPositionObservation<T>(VectorSensor sensor) where T : MonoBehaviour {
            var obj = FindFirstObjectByType<T>();
            if (obj != null) {
                Vector3 pos = obj.transform.position;
                sensor.AddObservation(pos.x);
                sensor.AddObservation(pos.z);
            }
            else {
                sensor.AddObservation(0f);
                sensor.AddObservation(0f);
            }
        }

        public override void CollectObservations(VectorSensor sensor)
        {
            //En total 15 floats no variables 

            // Observation 1 (Espacio 2): Posici�n del agente en el mundo:
            // En el futuro quiz� estar�a mejor con posiciones relativas si queremos entrenar varios a la vez
            // Pero si hacemos eso tendremos que tocar varios managers.... igual no interesa
            Vector3 pos = transform.position;
            sensor.AddObservation(pos.x);
            sensor.AddObservation(pos.z);

            //Observación 2 (espacio 2): Posición de dish tray en el mundo
            Vector3 DishTrayPos = FindFirstObjectByType<DishTray>().transform.position;
            sensor.AddObservation(DishTrayPos.x);
            sensor.AddObservation(DishTrayPos.z);

            // Observación 3: Posición del CookingPot más cercano (2 floats)
            AddObjectPositionObservation<CookingPot>(sensor);

            // Observación 4: Posición del Hob más cercano (2 floats)
            AddObjectPositionObservation<Hob>(sensor);

            // Observación 5: Posición del ChoppingBoard más cercano (2 floats)
            AddObjectPositionObservation<ChoppingBoard>(sensor);

            // Observación 6: Posición del DeliverCounterTop más cercano (2 floats)
            AddObjectPositionObservation<DeliverCountertop>(sensor);

            //Observación  7: Objeto cargado por el personaje (3 floats)
            var carried = player_controller.HeldObject;

            if (carried == null) { // Nada en la mano
                sensor.AddObservation(-1f); //Codificado como vacío
                sensor.AddObservation(-1f); // tipo
                sensor.AddObservation(-1f); // estado o extra info
            }
            else if (carried is Ingredient ingredient) { // Observamos ingrediente: tipo y estado
                sensor.AddObservation(0f); //Codificado como ingrediente
                sensor.AddObservation((float)ingredient.Type);
                sensor.AddObservation((float)ingredient.Status);
            }
            else if (carried is Plate plate) { // Observamos plato: limpieza y nº de ingredientes
                sensor.AddObservation(1f); //Codificado como plato
                sensor.AddObservation(plate.IsClean ? 1f : 0f);
                sensor.AddObservation(Mathf.Clamp01(plate.Ingredients.Count / 3f)); // suponiendo máx 3
            }
            else if (carried is CookingPot pot) { // Observamos olla: ¿está cocinando? y nº de ingredientes
                sensor.AddObservation(2f); //Codificado como olla
                sensor.AddObservation(pot.Ingredients.Count);
                sensor.AddObservation((float)pot.Ingredients[0].Type);
            }
            else { // Objeto no reconocido: observa algo por defecto
                sensor.AddObservation(-2f); // tipo desconocido
                sensor.AddObservation(0f);
                sensor.AddObservation(0f);
            }

            foreach (var o in OM.Orders)
            {
                float tNorm = o.RemainingTime / o.InitialRemainingTime;
                float ingrediente1 = (float)o.Ingredients[0].type;
                float ingrediente2 = (float)o.Ingredients[1].type;
                float ingrediente3 = (float)o.Ingredients[2].type;

                float[] obs = new float[4] {
                    Mathf.Clamp(tNorm, 0f, 1f),
                    ingrediente1,
                    ingrediente2,
                    ingrediente3
                };

                orderBuffer.AppendObservation(obs);
            }

            foreach (var ing in FindObjectsByType<Ingredient>(sortMode:FindObjectsSortMode.InstanceID))
            {
                Vector3 ipos = ing.transform.position;
                float[] obs = new float[] //Tamaño de 5
                {
                    ipos.x, ipos.y, ipos.z, (float)ing.Type, (float)ing.Status
                };
                ingredientBuffer.AppendObservation(obs);
            }


            foreach (var plate in FindObjectsByType<Plate>(sortMode: FindObjectsSortMode.InstanceID))
            {
                Vector3 ipos = plate.transform.position;
                float[] obs = new float[] //Tamaño de 5
                {
                    ipos.x, ipos.y, ipos.z, plate.IsClean ? 1 : 0, plate.Ingredients.Count
                };
                plateBuffer.AppendObservation(obs);
            }

            foreach (var ic in FindObjectsByType<IngredientCrate>(sortMode: FindObjectsSortMode.None)) {
                Vector3 ipos = ic.transform.position;
                float[] obs = new float[] {
                    ipos.x, ipos.z, (float) ic.type
                };
            }
        }

        public override void OnActionReceived(ActionBuffers actionBuffers)
        {
            if (actionDebug)
                Debug.Log("ACTION!");
            // Leer las acciones continuas
            a_MoveX(actionBuffers.ContinuousActions[0]);
            a_MoveY(actionBuffers.ContinuousActions[1]);
            a_Dash(actionBuffers.ContinuousActions[2]);
            a_Pickup(actionBuffers.ContinuousActions[3]);
            a_Interact(actionBuffers.ContinuousActions[4]);
        }

        // Lo separo por si queremos hacer algo entre medias antes de actualizar el valor
        private void a_MoveX(float value)    
        { 
            if(actionDebug)
                Debug.Log($"a_MoveX {value}");       
            movementInput.x = value; 
        }
        private void a_MoveY(float value)    
        { 
            if(actionDebug) 
                Debug.Log($"a_MoveY {value}"); 
            movementInput.y = value; 
        }
        private void a_Dash(float value)     
        {
            if (actionDebug)
                Debug.Log($"a_Dash {value}");        
            dashInput = value >= 0.5; 
        }
        private void a_Pickup(float value)   
        {
            value = 0; // HEY BORRAR ESTA KK
            if (actionDebug)
                Debug.Log($"a_Pickup {value}");      
            pickupInput = value >= 0.5; 
        }
        private void a_Interact(float value) 
        {
            if (actionDebug)
                Debug.Log($"a_Interact {value}");    
            interactionInput = value >= 0.5; 
        }

        public override void WriteDiscreteActionMask(IDiscreteActionMask actionMask)
        {
            actionMask.SetActionEnabled(0, 0, player_controller.isDashingPossible);
        }

        public void OrderFailed()
        {
            ordersFailed++;
        }

        // Update is called once per frame
        void Update()
        {
            player_controller.SetMLAgentInput(movementInput, dashInput, pickupInput,interactionInput);
            if (endEpisode)
            {
                ordersFailed = 0;
                EndEpisode();
            }

        }
    }
}
