﻿using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Undercooked.Data;
using Undercooked.Model;
using Undercooked.UI;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Undercooked.Managers
{
    public class OrderManager : MonoBehaviour
    {

        [SerializeField] private LevelData currentLevel;
        [SerializeField] private Order orderPrefab;
        [SerializeField] private float spawnIntervalBetweenOrders = 15f;
        [SerializeField] private float extraTimePerOrder = 20f;
        [SerializeField] private int maxConcurrentOrders = 5;
        [SerializeField] private OrdersPanelUI ordersPanelUI;

        private readonly List<Order> _orders = new List<Order>();
        private readonly Queue<Order> _poolOrders = new Queue<Order>();

        private WaitForSeconds _intervalBetweenDropsWait;
        private bool _isGeneratorActive;
        private Coroutine _generatorCoroutine;
        
        public delegate void OrderSpawned(Order order);
        public static event OrderSpawned OnOrderSpawned;

        public delegate void OrderExpired(Order order);
        public static event OrderExpired OnOrderExpired;
        
        public delegate void OrderDelivered(Order order, int tipCalculated);
        public static event OrderDelivered OnOrderDelivered;

        [HideInInspector] public List<Order> Orders { get => _orders; }
        [HideInInspector] public int MaxConcurrentOrders { get => maxConcurrentOrders; }

        public void Reset()
        {
            // 1) Stop the generator so we don’t spawn while we’re clearing things
            if (_generatorCoroutine != null)
            {
                StopCoroutine(_generatorCoroutine);
                _generatorCoroutine = null;
            }
            _isGeneratorActive = false;

            // 2) Return every live order to the pool
            //    We iterate over a copy because DeactivateSendBackToPool removes from _orders.
            foreach (var order in _orders.ToList())
            {
                // Mark it delivered (so RemoveAll will pick it up),
                // unsubscribe, enqueue back to pool:
                order.SetOrderDelivered();
                UnsubscribeEvents(order);
                _poolOrders.Enqueue(order);
            }
            // 3) Clear the active‐orders list
            _orders.Clear();

            // 4) Update your UI: remove all existing panels
            ordersPanelUI.ClearAll();  // <-- implement a method that hides/clears all slots

            // 5) Restart the generator
            _intervalBetweenDropsWait = new WaitForSeconds(spawnIntervalBetweenOrders);
            _isGeneratorActive = true;
            _generatorCoroutine = StartCoroutine(OrderGeneratorCoroutine());
        }


        private Order GetOrderFromPool()
        {
            return _poolOrders.Count > 0 ? _poolOrders.Dequeue() : Instantiate(orderPrefab, transform);
        }
        
        public void Init(LevelData levelData)
        {
            currentLevel = levelData;
            _orders.Clear();
            _intervalBetweenDropsWait = new WaitForSeconds(spawnIntervalBetweenOrders);
            _isGeneratorActive = true;
            _generatorCoroutine = StartCoroutine(OrderGeneratorCoroutine());
        }

        private void PauseOrderGenerator()
        {
            _isGeneratorActive = false;
            if (_generatorCoroutine != null)
            {
                StopCoroutine(_generatorCoroutine);
            }
        }

        private void ResumeIfPaused()
        {
            _isGeneratorActive = true;
            _generatorCoroutine ??= StartCoroutine(OrderGeneratorCoroutine());
        }
        
        public void StopAndClear()
        {
            PauseOrderGenerator();
            _generatorCoroutine = null;
            _orders.Clear();
            
            Debug.Log("[OrderManager] StopAndClear");
        }

        private IEnumerator OrderGeneratorCoroutine()
        {
            while (_isGeneratorActive)
            {
                TrySpawnOrder();
                yield return _intervalBetweenDropsWait;    
            }
        }

        private void TrySpawnOrder()
        {
            if (_orders.Count < maxConcurrentOrders)
            {
                var order = GetOrderFromPool();
                if (order == null)
                {
                    Debug.LogWarning("[OrderManager] Couldn't pick an Order from pool", this);
                    return;
                }
                
                order.Setup(GetRandomOrderData(), _orders.Count * extraTimePerOrder);
                _orders.Add(order);
                SubscribeEvents(order);
                OnOrderSpawned?.Invoke(order);
            }
        }

        private static void SubscribeEvents(Order order)
        {
            order.OnDelivered += HandleOrderDelivered;
            order.OnExpired += HandleOrderExpired;
        }
        
        private static void UnsubscribeEvents(Order order)
        {
            order.OnDelivered -= HandleOrderDelivered;
            order.OnExpired -= HandleOrderExpired;
        }

        private static void HandleOrderDelivered(Order order)
        {
            // Debug.Log("[OrderManager] HandleOrderDelivered");
        }
        
        private static void HandleOrderExpired(Order order)
        {
            // Debug.Log("[OrderManager] HandleOrderExpired");
            OnOrderExpired?.Invoke(order);
        }

        private void DeactivateSendBackToPool(Order order)
        {
            order.SetOrderDelivered();
            UnsubscribeEvents(order);
            _orders.RemoveAll(x => x.IsDelivered);
            _poolOrders.Enqueue(order);
        }

        private OrderData GetRandomOrderData()
        {
            var randomIndex = Random.Range(0, currentLevel.orders.Count);
            return Instantiate(currentLevel.orders[randomIndex]);
        }
        
        public void CheckIngredientsMatchOrder(List<Ingredient> ingredients)
        {
            if (ingredients == null) return;

            if (ingredients.Count == 0)
            {
                // signal “empty plate delivered” with order=null, tip=-1
                OnOrderDelivered?.Invoke(null, -1);
                return;
            }

            List<IngredientType> plateIngredients = ingredients.Select(x => x.Type).ToList();

            // orders are checked by arrival order (arrivalTime is reset when order expires)
            List<Order> orderByArrivalNotDelivered = _orders
                .Where(x => x.IsDelivered == false)
                .OrderBy(x => x.ArrivalTime).ToList();
            
            for (int i = 0; i < orderByArrivalNotDelivered.Count; i++)
            {
                var order = orderByArrivalNotDelivered[i];

                List<IngredientType> orderIngredients = order.Ingredients.Select(x => x.type).ToList();

                if (plateIngredients.Count != orderIngredients.Count) continue;
                
                var intersection = plateIngredients.Except(orderIngredients).ToList();
                
                if (intersection.Count != 0) continue; // doesn't match any plate
                
                var tip = CalculateTip(order);
                DeactivateSendBackToPool(order);
                OnOrderDelivered?.Invoke(order, tip);
                ordersPanelUI.RegroupPanelsLeft();
                return;
            }
        }

        private static int CalculateTip(Order order)
        {
            var ratio = order.RemainingTime / order.InitialRemainingTime;
            if (ratio > 0.75f) return 6;
            if (ratio > 0.5f) return 4;
            return ratio > 0.25f ? 2 : 0;
        }
    }
    
}
