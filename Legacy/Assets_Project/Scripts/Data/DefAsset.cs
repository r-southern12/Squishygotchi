using UnityEngine;

namespace Squishy.Data
{
    /// <summary>
    /// A data asset wrapping a plain simulation definition. The simulation reads <see cref="def"/>;
    /// subclasses add presentation-only fields (colours, prefabs, icons) that the simulation never sees.
    /// </summary>
    public abstract class DefAsset<T> : ScriptableObject where T : class, new()
    {
        public T def = new T();
    }
}
