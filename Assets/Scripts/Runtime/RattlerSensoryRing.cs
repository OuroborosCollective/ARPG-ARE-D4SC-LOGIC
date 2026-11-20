using UnityEngine;

namespace AreArpg.UnityClient
{
    public sealed partial class ArenaRuntime
    {
        private static Transform RattlerBone(Transform root, string name)
        {
            foreach (var bone in root.GetComponentsInChildren<Transform>(true))
                if (bone.name == name) return bone;
            return null;
        }

        // The contact point is in the original prefab's bind frame. Its mesh has a 45x bone
        // scale; using this frame preserves the authored size through every original clip.
        private static void MountRattlerSensoryRing(Transform host)
        {
            var original = Resources.Load<GameObject>("ARE/Visuals/CryptRattler");
            var ring = Resources.Load<GameObject>("AshenRelics/Tastkranz");
            if (!original || !ring || !host) return;
            var rest = RattlerBone(original.transform, "Head");
            var live = RattlerBone(host, "Head");
            if (!rest || !live || live.Find("Tastkranz_Innenring")) return;
            var socket = new GameObject("Tastkranz_Innenring").transform;
            socket.SetParent(live,false);
            var sourceToHead = rest.worldToLocalMatrix * original.transform.localToWorldMatrix;
            var forward = Quaternion.Euler(-6f,0,0) * Vector3.forward;
            var up = Quaternion.Euler(-6f,0,0) * Vector3.up;
            socket.localPosition = sourceToHead.MultiplyPoint3x4(new Vector3(0f,2.417f,.807f));
            socket.localRotation = Quaternion.LookRotation(sourceToHead.MultiplyVector(forward),sourceToHead.MultiplyVector(up));
            var basis = original.transform.worldToLocalMatrix * rest.localToWorldMatrix * Matrix4x4.Rotate(socket.localRotation);
            float x = basis.MultiplyVector(Vector3.right).magnitude, y = basis.MultiplyVector(Vector3.up).magnitude,
                z = basis.MultiplyVector(Vector3.forward).magnitude;
            if (Mathf.Min(x,Mathf.Min(y,z)) < .00001f) { Destroy(socket.gameObject); return; }
            socket.localScale = new Vector3(1.25f/x,1.25f/y,1.25f/z);
            if (!PassiveArtVisuals.TryBindPrefab(socket,ring)) Destroy(socket.gameObject);
        }
    }
}
