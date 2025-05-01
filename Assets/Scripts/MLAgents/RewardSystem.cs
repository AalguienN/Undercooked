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
        public float startCutRew = 0.0f;
        public float duringCutRew = 0.0f;
        public float endCutRew = 0.0f;
        public float cookRew = 0.0f;
        public float burnRew  = 0.0f;
        public float cleanRew = 0.0f;
        public float deliverRew = 0.0f;
        public float expiredRew = 0.0f;
        public int numberOfTooMuchFood = 0;
        public float tooMuchFoodRew = 0.0f;
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
            var ingredients = GameObject.FindObjectsByType<Ingredient>(sortMode: FindObjectsSortMode.None);
            if (ingredients.Length > numberOfTooMuchFood)
            {
                agent?.AddReward(tooMuchFoodRew);
            }
            
        }

        private void SubscribeRewardEvents()
        {
            ChoppingBoard.OnChoppingStop += OnChopped;
            ChoppingBoard.OnChopping += OnChopping;
            Sink.OnCleanStop += OnCleaned;
            CookingPot.OnCookFinished += OnCooked;
            CookingPot.OnBurned += OnBurned;
            CookingPot.OnIngredientAdded += OnIngridienAdded;
            OrderManager.OnOrderDelivered += OnDelivered;
            OrderManager.OnOrderExpired += OnExpired;

            //Para orientar
            ChoppingBoard.OnChoppingStart += OnStartChopping;
        }
        private void UnsubscribeRewardEvents()
        {
            ChoppingBoard.OnChoppingStop -= OnChopped;
            Sink.OnCleanStop -= OnCleaned;
            CookingPot.OnCookFinished -= OnCooked;
            CookingPot.OnBurned -= OnBurned;
            OrderManager.OnOrderDelivered -= OnDelivered;
            OrderManager.OnOrderExpired -= OnExpired;

            //Para orientar
            ChoppingBoard.OnChoppingStart -= OnStartChopping;
        }

        void OnStartChopping(PlayerController pc, bool done) {
            if (pc == owner) Add(startCutRew);
        }

        void OnChopped(PlayerController pc, bool done)
        {
            if (done && pc == owner) Add(endCutRew); //Solo el que lo ha hecho
        }

        void OnChopping(PlayerController pc, bool done)
        {
            if (done && pc == owner) Add(duringCutRew); //Solo el que lo ha hecho
        }

        void OnCleaned(PlayerController pc, bool done)
        {
            if (done && pc == owner) Add(cleanRew); //Solo el que lo ha hecho
        }

        void OnIngridienAdded(CookingPot pot) 
        {
            Add(addIngredientToPotRew);
        }

        void OnCooked(CookingPot pot)
        {
            Add(cookRew); //Cualquier jugador
        }

        void OnBurned(CookingPot pot)
        {
            Add(burnRew); //Cualquier jugador
        }

        void OnDelivered(Order order, int tip)
        {
            if (order == null) 
                Add(-deliverRew);
            else 
                Add(deliverRew);
        }

        void OnExpired(Order order)
        {
            Add(expiredRew);
            agent.OrderFailed();
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
