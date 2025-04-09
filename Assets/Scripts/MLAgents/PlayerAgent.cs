using System.Collections.Generic;
using System;
using Undercooked.Model;
using Undercooked.Player;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;

namespace Undercooked
{
    public class PlayerAgent : Agent
    {
        PlayerController player_controller;

        private float movementAction_x;
        private float movementAction_y;
        private float dashAction;
        private float pickupAction;
        private float interactionAction;

        private Vector2 movementInput;
        private float dashInput;
        private float pickupInput;
        private float interactionInput;

        private void Awake()
        {
            player_controller = GetComponent<PlayerController>();
        }

        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {
            
        }

        public override void CollectObservations(VectorSensor sensor) { 
            
        }

        public override void OnActionReceived(ActionBuffers actionBuffers)
        {
            // Leer las acciones continuas
            a_MoveX(actionBuffers.ContinuousActions[0]);
            a_MoveY(actionBuffers.ContinuousActions[1]);
            a_Dash(actionBuffers.ContinuousActions[2]);
            a_Pickup(actionBuffers.ContinuousActions[3]);
            a_Interact(actionBuffers.ContinuousActions[4]);
        }

        // Lo separo por si queremos hacer algo entre medias antes de actualizar el valor
        private void a_MoveX(float value) { Debug.Log($"a_MoveX {value}");movementInput.x = value; }
        private void a_MoveY(float value) { Debug.Log($"a_MoveY {value}");movementInput.y = value; }
        private void a_Dash(float value) { Debug.Log($"a_Dash {value}");dashInput = value; }
        private void a_Pickup(float value) { Debug.Log($"a_Pickup {value}");pickupInput = value; }
        private void a_Interact(float value) { Debug.Log($"a_Interact {value}");interactionInput = value; }


        // Update is called once per frame
        void Update()
        {
            player_controller.SetMLAgentInput(movementInput, dashInput, pickupInput,interactionInput);
        }
    }
}
