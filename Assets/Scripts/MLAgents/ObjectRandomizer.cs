using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;   // for NavMeshAgent

namespace Undercooked
{
    public class ObjectRandomizer : MonoBehaviour
    {
        [Tooltip("List of GameObjects to shuffle")]
        public List<GameObject> GameObjects;

        private struct TransformSnapshot
        {
            public Vector3 Position;
            public Quaternion Rotation;
        }

        private List<TransformSnapshot> _snapshots = new List<TransformSnapshot>();

        private void Awake()
        {
            if (GameObjects == null || GameObjects.Count == 0)
                Debug.LogError("ObjectRandomizer: 'GameObjects' list is not set or empty.");

            CaptureTransforms();
        }

        [ContextMenu("Capture Transforms")]
        public void CaptureTransforms()
        {
            _snapshots.Clear();
            if (GameObjects == null) return;

            foreach (var go in GameObjects)
            {
                if (go == null) continue;
                _snapshots.Add(new TransformSnapshot
                {
                    Position = go.transform.position,
                    Rotation = go.transform.rotation
                });
            }
        }

        [ContextMenu("Shuffle Transforms")]
        public void ShuffleTransforms()
        {
            if (GameObjects == null || _snapshots.Count != GameObjects.Count)
            {
                Debug.LogWarning("ObjectRandomizer: Call CaptureTransforms() first or counts mismatch.");
                return;
            }

            // --- Fisher‐Yates shuffle on a copy of the snapshots ---
            var shuffled = new List<TransformSnapshot>(_snapshots);
            for (int i = shuffled.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                var tmp = shuffled[i];
                shuffled[i] = shuffled[j];
                shuffled[j] = tmp;
            }

            // --- Reapply to your GameObjects ---
            for (int i = 0; i < GameObjects.Count; i++)
            {
                var go = GameObjects[i];
                if (go == null) continue;

                var target = shuffled[i];

                // 1) NavMeshAgent?
                var agent = go.GetComponent<NavMeshAgent>();
                if (agent != null)
                {
                    agent.Warp(target.Position);
                }
                else
                {
                    // 2) Rigidbody?
                    var rb = go.GetComponent<Rigidbody>();
                    if (rb != null && !rb.isKinematic)
                    {
                        rb.MovePosition(target.Position);
                    }
                    else
                    {
                        // 3) Plain transform
                        go.transform.position = target.Position;
                    }
                }

                // Always restore the rotation
                go.transform.rotation = target.Rotation;
            }

            // Make sure physics colliders & queries are immediately in sync
            Physics.SyncTransforms();
        }

        public void Randomize()
        {
            ShuffleTransforms();
        }
    }
}
