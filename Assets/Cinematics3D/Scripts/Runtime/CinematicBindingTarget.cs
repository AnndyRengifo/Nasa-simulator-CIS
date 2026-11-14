using UnityEngine;

namespace Sol3.Cinematics
{
    /// <summary>
    /// Marca un objeto de la escena como destino de una pista del Timeline.
    /// Clave de enlace = nombre de la pista (ver <see cref="CinematicBindingKeys"/>).
    ///
    /// Opcionalmente define un "Visual Root": el contenedor cuyo contenido (el modelo 3D) puede
    /// sustituirse sin tocar animaciones ni cámaras, porque las pistas animan la raíz del actor
    /// y el contenedor "Visual", nunca el modelo concreto.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("SOL-3/Cinemáticas/Cinematic Binding Target")]
    public sealed class CinematicBindingTarget : MonoBehaviour
    {
        [Tooltip("Clave de enlace = nombre de la pista del Timeline. Si está vacía se usa el nombre del GameObject.")]
        [SerializeField] string bindingKey = string.Empty;

        [Tooltip("Opcional. Contenedor del modelo visual reemplazable (normalmente el hijo 'Visual').")]
        [SerializeField] Transform visualRoot;

        public string BindingKey => string.IsNullOrEmpty(bindingKey) ? gameObject.name : bindingKey;
        public Transform VisualRoot => visualRoot;

        /// <summary>
        /// Sustituye en tiempo de ejecución el modelo visual por una instancia del prefab indicado.
        /// Solo se usa con los 'Cast Overrides' de la definición; la escena en disco no cambia.
        /// </summary>
        public GameObject ReplaceVisual(GameObject visualPrefab)
        {
            if (visualPrefab == null)
                return null;

            if (visualRoot == null)
            {
                Debug.LogWarning($"[Cinematics] '{BindingKey}' no tiene 'Visual Root': no se puede sustituir su modelo.", this);
                return null;
            }

            for (int i = visualRoot.childCount - 1; i >= 0; i--)
            {
                var child = visualRoot.GetChild(i).gameObject;
                child.SetActive(false);
                child.name += " (reemplazado)";
                Destroy(child);
            }

            var instance = Instantiate(visualPrefab, visualRoot, false);
            instance.name = visualPrefab.name;
            CinematicLayers.AssignRecursively(instance, gameObject.layer);

            if (TryGetComponent<Animator>(out var animator))
                animator.Rebind();

            return instance;
        }
    }
}
