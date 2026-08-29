namespace FungalCurse.Systems
{
    /// <summary>
    /// Implement on any MonoBehaviour that is spawned through <see cref="ObjectPooler"/>.
    /// The pooler invokes these instead of the engine's Awake/OnDestroy so pooled instances
    /// can reset their state on reuse without ever being garbage collected.
    /// </summary>
    public interface IPooledObject
    {
        /// <summary>Called every time the object is taken from the pool and activated.</summary>
        void OnObjectSpawn();

        /// <summary>Called right before the object is returned (deactivated) to the pool.</summary>
        void OnObjectDespawn();
    }
}
