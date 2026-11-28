using System;
using System.Collections.Generic;
using System.Linq;
using AreArpg.Core;
using UnityEditor;
using UnityEngine;
using Fit = AreArpg.EditorTools.AschenwachtGearBuilder.HeadFit;

namespace AreArpg.EditorTools
{
    public static class ThreeOrderGearBuilder
    {
        // Each delivered mesh belongs to one wearable. The source's zero-padded prefixes survive FBX import.
        private static readonly int[][] Parts = { new[] { 1 }, new[] { 2, 3, 4, 6 }, new[] { 7, 8 }, new[] { 5, 9 }, new[] { 10 } };

        public static void Build()
        {
            if (!(AssetImporter.GetAtPath(AschenwachtGearBuilder.GuardHeadModel) is ModelImporter))
                throw new InvalidOperationException("Missing armour fitting reference model: " + AschenwachtGearBuilder.GuardHeadModel);

            foreach (var set in ThreeOrderSet.All)
            {
                string path = "Assets/PrivateArt/ThreeOrders/Sources/" + set.AssetName + "/" + set.AssetName;
                var importer = AssetImporter.GetAtPath(path + ".fbx") as ModelImporter;
                if (!importer) throw new InvalidOperationException("Missing rigged armour " + path);
                bool wasReadable = importer.isReadable;
                try
                {
                    if (!wasReadable) { importer.isReadable = true; importer.SaveAndReimport(); }
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path + ".prefab");
                    if (!prefab) throw new InvalidOperationException("Missing material prefab " + path);
                    var skins = prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                    if (skins.Length != 15) throw new InvalidOperationException(path + " expected 15 source meshes, got " + skins.Length);
                    var pieces = new List<AschenwachtGearBuilder.FitPiece>();
                    var covered = new HashSet<SkinnedMeshRenderer>();
                    for (int i = 0; i < Parts.Length; i++)
                    {
                        var selected = skins.Where(s => Parts[i].Any(n => s.name.StartsWith(n.ToString("D2") + "_", StringComparison.Ordinal))).ToArray();
                        if (selected.Length == 0 || selected.Any(s => !covered.Add(s))) throw new InvalidOperationException("Invalid mesh group " + set.Pieces[i]);
                        Fit head = i != (int)ArmorSlot.Head ? Fit.None : set == ThreeOrderSet.Rostkeil ? Fit.WedgeHelm :
                            set == ThreeOrderSet.Moorvogt ? Fit.BrimmedHat : Fit.ConventHood;
                        pieces.Add(new AschenwachtGearBuilder.FitPiece(set.Pieces[i].ToString(), selected, head, set != ThreeOrderSet.Rostkeil));
                    }
                    if (covered.Count != skins.Length || !AschenwachtGearBuilder.FitToHero(pieces, "Assets/PrivateArt/ThreeOrders/" + set.AssetName + "/"))
                        throw new InvalidOperationException("Could not fit all " + set.Name + " meshes to EntryHero");
                }
                finally
                {
                    if (!wasReadable) { importer.isReadable = false; importer.SaveAndReimport(); }
                }
                Debug.Log("THREE_ORDER_FIT " + set.AssetName + " meshes=15 wearables=5");
            }
        }
    }
}
