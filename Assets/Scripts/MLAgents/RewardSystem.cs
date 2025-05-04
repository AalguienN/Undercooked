using System;
using Undercooked.Appliances;
using Undercooked.Model;
using Undercooked.Player;
using UnityEngine;
using Undercooked.Managers;

namespace Undercooked
{
    public class RewardSystem : MonoBehaviour
    {
        [Header("Rewards")]
        [Header("Cutting")]
        public float CutRew = 0.0f;
        public float duringCutRew = 0.0f;
        public float ChopingTableRew = 0.0f;
        [Header("Plate")]
        public float PlateRew = 0.0f; 
        
        [Header("CookingPot")]
        public float cookingPotRew = 0.0f;
        public float cookRew = 0.0f;

        public float deliverRew = 0.0f;
        public float addIngredientToPotRew = 0.0f;

        public float totalReward { get; private set; }

        public PlayerAgent agent;

        PlayerController owner;
        void Awake()
        {
            agent = GetComponent<PlayerAgent>();
            owner = GetComponent<PlayerController>();
        }

        void OnEnable()
        {
            SubscribeRewardEvents();
        }

        void OnDisable()
        {
            UnsubscribeRewardEvents();
        }

        private void Update()
        {
        }

        private void SubscribeRewardEvents()
        {
            ChoppingBoard.OnChoppingStart += OnChopped;
            ChoppingBoard.OnChoppingStop += OnChopped;
            ChoppingBoard.OnChopping += OnChopping;
            ChoppingBoard.OnChoppingNegative += ChoppingTablePositive;
            //ChoppingBoard.OnChoppingPositive += ChoppingTableNegative;

            Plate.PlateNegative += PlateNeg;
            //Plate.PlatePositive += PlatePos;

            CookingPot.OnCookFinished += OnCooked;
            //CookingPot.OnPotPositive += OnPotPos;
            CookingPot.OnPotNegative += OnPotNeg;

            OrderManager.OnOrderDelivered += OnDelivered;
        }

        private void UnsubscribeRewardEvents()
        {
            ChoppingBoard.OnChoppingStart -= OnChopped;
            ChoppingBoard.OnChoppingStop -= OnChopped;
            ChoppingBoard.OnChopping -= OnChopping;
            ChoppingBoard.OnChoppingNegative -= ChoppingTablePositive;
            //ChoppingBoard.OnChoppingPositive -= ChoppingTableNegative;

            Plate.PlateNegative -= PlateNeg;
            //Plate.PlatePositive -= PlatePos;

            CookingPot.OnCookFinished -= OnCooked;
            //CookingPot.OnPotPositive -= OnPotPos;
            CookingPot.OnPotNegative -= OnPotNeg;

            OrderManager.OnOrderDelivered -= OnDelivered;
        }

        void OnChopped(PlayerController pc, bool done)
        {
            Add(CutRew); //Solo el que lo ha hecho
        }

        void OnChopping(PlayerController pc, bool done)
        {
            Add(duringCutRew); //Solo el que lo ha hecho
        }

        void ChoppingTablePositive(PlayerController pc, bool done)
        {
            Add(ChopingTableRew); //Solo el que lo ha hecho
        }
        void ChoppingTableNegative(PlayerController pc, bool done)
        {
            Add(-ChopingTableRew); //Solo el que lo ha hecho
        }

        void PlatePos()
        {
            Add(-ChopingTableRew); //Solo el que lo ha hecho
        }

        void PlateNeg()
        {
            Add(-ChopingTableRew); //Solo el que lo ha hecho
        }

        void OnPotPos(CookingPot pot) 
        {
            Add(cookingPotRew);
        }
        void OnPotNeg(CookingPot pot)
        {
            Add(-cookingPotRew);
        }

        void OnCooked(CookingPot pot)
        {
            Add(cookRew); //Cualquier jugador
        }

        void OnDelivered(Order order, int tip)
        {
            if (order == null) 
                Add(-deliverRew);
            else 
                Add(deliverRew);
        }

        // ─────────── añadir recompensa ───────────
        void Add(float value)
        {
            totalReward += value;
            agent?.AddReward(value);         // solo si este jugador es ML‑Agent
            Debug.Log(totalReward);
        }

    }
}
