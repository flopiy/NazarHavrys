namespace FungalCurse.Combat
{
    /// <summary>
    /// Contract for an entity that can be killed outright, bypassing armor / damage mitigation.
    /// Kept separate from <see cref="IDamageable"/> so hazard zones can request an instant,
    /// un-mitigated elimination without knowing the concrete target type (decoupling pattern).
    /// </summary>
    public interface IKillable
    {
        /// <summary>Immediately and unconditionally eliminate this entity.</summary>
        void Kill();
    }
}
