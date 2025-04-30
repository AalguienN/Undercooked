using System.Collections.Generic;
using System;
using Undercooked.Model;
using Undercooked.Player;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;
using Undercooked.Managers;
using NUnit.Framework.Interfaces;
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

        private void Awake()
        {
            player_controller = GetComponent<PlayerController>();
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

        public override void CollectObservations(VectorSensor sensor)
        {
            //Necesita 

            // Observation 1 (Espacio 2): Posici�n del agente en el mundo:
            // En el futuro quiz� estar�a mejor con posiciones relativas si queremos entrenar varios a la vez
            // Pero si hacemos eso tendremos que tocar varios managers.... igual no interesa
            Vector3 pos = transform.position;
            sensor.AddObservation(pos.x);
            sensor.AddObservation(pos.z);

            //Observation 2 (Espacio 20): Lista de orders
            //En principio solo puede haber 5 a la vez...
            //pero si se a�adieran m�s hay que tener en cuenta que hay que cambiar el vector a N * 4
            var orders = OM.Orders;   // crea un getter que devuelva la lista
            int slots = OM.MaxConcurrentOrders;

            for (int i = 0; i < slots; i++) {
                if (i < orders.Count)
                {
                    Order o = orders[i];
                    float tNorm = o.RemainingTime / o.InitialRemainingTime;

                    float ingrediente1 = (float)o.Ingredients[0].type;
                    float ingrediente2 = (float)o.Ingredients[1].type;
                    float ingrediente3 = (float)o.Ingredients[2].type;

                    sensor.AddObservation(tNorm);
                    sensor.AddObservation(ingrediente1);
                    sensor.AddObservation(ingrediente2);
                    sensor.AddObservation(ingrediente3);
                }
                else {
                    sensor.AddObservation(1); //Maximo tiempo posible
                    sensor.AddObservation(-1f);
                    sensor.AddObservation(-1f);
                    sensor.AddObservation(-1f);
                }
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
