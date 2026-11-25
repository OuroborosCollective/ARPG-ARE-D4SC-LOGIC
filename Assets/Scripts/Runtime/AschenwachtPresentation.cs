using UnityEngine;

namespace AreArpg.UnityClient
{
    public sealed partial class ArenaRuntime
    {
        /// <summary>
        /// Aschenwacht's fire baskets beside the procession path (two on the portal terrace, two halfway, two before the
        /// stairs, two at the gate) and one in the bell court. The art itself stays passive; its light is put up here.
        /// </summary>
        private static readonly Vector3[] CourtFires =
        {
            new Vector3(-7.5f, 3.15f, -36f), new Vector3(7.5f, 3.15f, -36f), new Vector3(-4.7f, 1.85f, -8f), new Vector3(4.7f, 1.85f, -8f),
            new Vector3(-4.7f, 1.85f, 23f), new Vector3(4.7f, 1.85f, 23f), new Vector3(-7.5f, 3.1f, 36f), new Vector3(7.5f, 3.1f, 36f),
            new Vector3(0f, 3.8f, 57.5f)
        };

        private static void AddCourtFireLights(Transform scenery)
        {
            var holder = new GameObject("Aschenwacht_Feuerlicht").transform;
            holder.SetParent(scenery, false);
            foreach (Vector3 spot in CourtFires)
            {
                var light = new GameObject("Glutkorb").AddComponent<Light>();
                light.transform.SetParent(holder, false);
                light.transform.localPosition = spot;
                light.type = LightType.Point;
                light.shadows = LightShadows.None;
                light.color = new Color(1f, .56f, .26f);
                light.range = 6.5f;
                light.intensity = 1.6f;
            }
        }
    }
}
