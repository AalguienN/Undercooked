using Undercooked.Data;
using UnityEngine;

namespace Undercooked.Model
{
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(Collider))]
    public class Ingredient : Interactable, IPickable
    {
        [SerializeField] private IngredientData data;
        private Rigidbody _rigidbody;
        private Collider _collider;
        private MeshRenderer _meshRenderer;
        private MeshFilter _meshFilter;

        public IngredientStatus Status { get; private set; }
        public IngredientType Type => data.type;
        public Color BaseColor => data.baseColor;

        [SerializeField] private IngredientStatus startingStatus = IngredientStatus.Raw;

        [SerializeField] private GameObject IconCamera;
        [SerializeField] private GameObject IconCameraProcessed;

        public float ProcessTime => data.processTime;
        public float CookTime => data.cookTime;
        public Sprite SpriteUI => data.sprite;

        protected override void Awake()
        {
            base.Awake();
            
            _meshRenderer = GetComponent<MeshRenderer>();
            _meshFilter = GetComponent<MeshFilter>();
            _rigidbody = GetComponent<Rigidbody>();
            _collider = GetComponent<Collider>();
            Setup();

            
            //IconCameraProcessed.SetActive(false);
        }

        private void Setup()
        {
            // Rigidbody is kinematic almost all the time, except when we drop it on the floor
            // re-enabling when picked up.
            _rigidbody.isKinematic = true;
            _collider.enabled = false;
            
            Status = IngredientStatus.Raw;
            _meshFilter.mesh = data.rawMesh;
            _meshRenderer.material = data.ingredientMaterial;

            if (startingStatus == IngredientStatus.Processed)
            {
                ChangeToProcessed();
            }
        }
        
        public void Pick()
        {
            // Only set isKinematic if the Rigidbody still exists
            if (_rigidbody != null)
                _rigidbody.isKinematic = true;
            
            // Only disable collider if it still exists
            if (_collider != null)
                _collider.enabled = false;
        }
        
        public void Drop()
        {
            // Similarly guard here as well
            if (_rigidbody != null)
                _rigidbody.isKinematic = false;
            if (_collider != null)
                _collider.enabled = true;

            transform.SetParent(null);
        }
        
        public void ChangeToProcessed()
        {
            Status = IngredientStatus.Processed;
            _meshFilter.mesh = data.processedMesh;
            //IconCameraProcessed.SetActive(true);
        }

        public void ChangeToCooked()
        {
            Status = IngredientStatus.Cooked;
            var cookedMesh = data.cookedMesh;
            if (cookedMesh == null) return;
            
            _meshFilter.mesh = cookedMesh;
            SetMeshRendererEnabled(true);
        }

        public void SetMeshRendererEnabled(bool enable)
        {
            _meshRenderer.enabled = enable;
            //IconCamera.SetActive(enable);
            //if (Status == IngredientStatus.Processed)
            //    IconCameraProcessed.SetActive(enable);
            //else IconCameraProcessed.SetActive(false);
        }

        public override bool TryToDropIntoSlot(IPickable pickableToDrop)
        {
            // Ingredients normally don't get any pickables dropped into it.
            // Debug.Log("[Ingredient] TryToDrop into an Ingredient isn't possible by design");
            return false;
        }

        public override IPickable TryToPickUpFromSlot(IPickable playerHoldPickable)
        {
            // Debug.Log($"[Ingredient] Trying to PickUp {gameObject.name}");
            _rigidbody.isKinematic = true;
            return this;
        }
    }
}