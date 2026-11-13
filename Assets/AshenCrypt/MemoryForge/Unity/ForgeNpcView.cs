using UnityEngine;

namespace AshenCrypt.MemoryForge.Integration
{
    /// <summary>Read-only bridge for an existing host-owned mind. Does not spawn, tick, decide, save or apply actions.</summary>
    [DisallowMultipleComponent]
    public sealed class ForgeNpcView : MonoBehaviour
    {
        public NpcMind Mind { get; private set; }
        public void Bind(NpcMind hostOwnedMind) { Mind=hostOwnedMind; }
        private void OnDestroy(){Mind=null;}
    }
}
