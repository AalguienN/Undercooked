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
        public float cutRew = 0.0f;
        public float cookRew = 0.0f;
        public float burnRew  = 0.0f;
        public float cleanRew = 0.0f;
        public float deliverRew = 0.0f;
        public float expiredRew = 0.0f;

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

        private void SubscribeRewardEvents()
        {
            ChoppingBoard.OnChoppingStop += OnChopped;
            Sink.OnCleanStop += OnCleaned;
            CookingPot.OnCookFinished += OnCooked;
            CookingPot.OnBurned += OnBurned;
            OrderManager.OnOrderDelivered += OnDelivered;
            OrderManager.OnOrderExpired += OnExpired;
        }
        private void UnsubscribeRewardEvents()
        {
            ChoppingBoard.OnChoppingStop -= OnChopped;
            Sink.OnCleanStop -= OnCleaned;
            CookingPot.OnCookFinished -= OnCooked;
            CookingPot.OnBurned -= OnBurned;
            OrderManager.OnOrderDelivered -= OnDelivered;
            OrderManager.OnOrderExpired -= OnExpired;
        }

        void OnChopped(PlayerController pc, bool done)
        {
            if (done && pc == owner) Add(cutRew); //Solo el que lo ha hecho
        }

        void OnCleaned(PlayerController pc, bool done)
        {
            if (done && pc == owner) Add(cleanRew); //Solo el que lo ha hecho
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
            Add(deliverRew);
        }

        void OnExpired(Order order)
        {
            Add(expiredRew);
        }

        // ─────────── añadir recompensa ───────────
        void Add(float value)
        {
            totalReward += value;
            agent?.AddReward(value);         // solo si este jugador es ML‑Agent
            Debug.Log(totalReward);
        }


        // Example reward methods
        //public void GiveReward(float r)
        //{
        //    totalReward += r;
        //}

        //public void BurnReward()
        //{
        //    totalReward += burnRew;
        //}

        //public void CutReward()
        //{
        //    totalReward += cutRew;
        //}

        //public void CookReward()
        //{ 
        //    totalReward += cookRew;
        //}

        //public void CleanReward()
        //{
        //    totalReward += cleanRew;
        //}
    }
}
