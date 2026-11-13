using UnityEngine;
namespace AreArpg.UnityClient
{
    /// <summary>Releases generated presentation resources with their chapter, never touches input assets.</summary>
    public sealed class WoodlandMeshOwner : MonoBehaviour
    {
        public Mesh Mesh;
        public Material Material;
        private void OnDestroy() { if(Mesh!=null)Destroy(Mesh);if(Material!=null)Destroy(Material); }
    }
}
