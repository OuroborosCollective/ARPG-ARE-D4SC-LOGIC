using System.Collections.Generic;
using AreArpg.Core;
using UnityEngine;

namespace AreArpg.UnityClient
{
    public sealed partial class ArenaRuntime
    {
        private Transform _weaponHost,_equippedVisual,_shieldVisual,_peltMantle;
        private readonly List<Transform> _starterWeaponParts=new List<Transform>();
        private string _shownWeapon="",_shownShield="";
        private RuntimeAnimatorController _originalCombatController;
        private AnimatorOverrideController _equipmentAnimation;
        private Transform Hand(bool left)
        {
            foreach(var animator in _heroMeleeAnimations)if(animator!=null&&animator.isHuman)
            {var hand=animator.GetBoneTransform(left?HumanBodyBones.LeftHand:HumanBodyBones.RightHand);if(hand!=null)return hand;}
            return _hero;
        }
        private Transform EquipmentVisual(string key,Transform parent,string name)
        {
            var t=new GameObject(name).transform;t.SetParent(parent,false);
            var scale=parent.lossyScale;t.localScale=new Vector3(1/scale.x,1/scale.y,1/scale.z);
            if(!PassiveArtVisuals.TryBind(t,key)) {Destroy(t.gameObject);return null;}
            return t;
        }
        private void UpdateEquipmentPresentation()
        {
            if(_hero==null||_weaponHost==null)return;
            var gear=_game.Progression.Equipment;var weapon=gear.Weapon;var shield=gear.Shield;
            string id=weapon?.Definition.Id??"starter",off=shield?.Definition.Id??"none";
            if(_shownWeapon!=id)
            {
                if(_equippedVisual!=null)Destroy(_equippedVisual.gameObject);
                _equippedVisual=null;
                if(weapon!=null)_equippedVisual=EquipmentVisual(weapon.Definition.VisualKey,weapon.Definition.Family==WeaponFamily.Bow?Hand(true):_weaponHost,"Equipped_"+id);
                foreach(var part in _starterWeaponParts)if(part!=null)part.gameObject.SetActive(_equippedVisual==null);
                var primitive=_weaponHost.GetComponent<Renderer>();if(primitive!=null)primitive.enabled=_equippedVisual==null&&_starterWeaponParts.Count==0;
                ConfigureWeaponAnimation(weapon);_shownWeapon=id;
            }
            if(_shownShield!=off)
            {
                if(_shieldVisual!=null)Destroy(_shieldVisual.gameObject);
                _shieldVisual=shield==null?null:EquipmentVisual(shield.Definition.VisualKey,Hand(true),"Equipped_"+off);_shownShield=off;
            }
            if(_game.Progression.Woodland.ArmorCrafted&&_peltMantle==null)
            {
                // A small fitted fur mantle makes the unique crafted reward visible without replacing the hero rig.
                var t=new GameObject("Equipped_BearPeltMantle").transform;var chest=Hand(false);
                foreach(var animator in _heroMeleeAnimations)if(animator!=null&&animator.isHuman){chest=animator.GetBoneTransform(HumanBodyBones.Chest)??_hero;break;}
                t.SetParent(chest,false);var scale=chest.lossyScale;t.localScale=new Vector3(1/scale.x,1/scale.y,1/scale.z);
                var vertices=new Vector3[18];var indices=new int[48];
                for(int i=0;i<=8;i++){float angle=i*Mathf.PI/8;vertices[i*2]=new Vector3(Mathf.Cos(angle)*.22f,.1f,-Mathf.Sin(angle)*.13f);vertices[i*2+1]=new Vector3(Mathf.Cos(angle)*.4f,-.35f,-Mathf.Sin(angle)*.22f);if(i<8){int n=i*2,k=i*6;indices[k]=n;indices[k+1]=n+1;indices[k+2]=n+2;indices[k+3]=n+2;indices[k+4]=n+1;indices[k+5]=n+3;}}
                var mesh=new Mesh{name="BearPeltMantle"};mesh.vertices=vertices;mesh.triangles=indices;mesh.RecalculateNormals();
                t.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=t.gameObject.AddComponent<MeshRenderer>();renderer.material=new Material(Shader.Find("Universal Render Pipeline/Lit")??Shader.Find("Standard")){color=new Color(.25f,.15f,.08f)};
                t.gameObject.AddComponent<WoodlandMeshOwner>().Mesh=mesh;_peltMantle=t;
            }
            if(_peltMantle!=null)_peltMantle.gameObject.SetActive(_game.Progression.Woodland.ArmorCrafted);
        }
        private void ConfigureWeaponAnimation(WeaponItem weapon)
        {
            var clip=weapon!=null&&weapon.Definition.Family==WeaponFamily.Bow?Resources.Load<AnimationClip>("ARE/Visuals/WeaponBowShot"):null;
            foreach(var animator in _heroMeleeAnimations)
            {
                if(animator==null)continue;
                if(_originalCombatController==null)_originalCombatController=animator.runtimeAnimatorController;
                if(clip==null)animator.runtimeAnimatorController=_originalCombatController;
                else
                {
                    if(_equipmentAnimation==null)
                    {
                        _equipmentAnimation=new AnimatorOverrideController(_originalCombatController);
                        var overrides=new List<KeyValuePair<AnimationClip,AnimationClip>>();_equipmentAnimation.GetOverrides(overrides);
                        for(int i=0;i<overrides.Count;i++)if(overrides[i].Key.name.Contains("Strike"))overrides[i]=new KeyValuePair<AnimationClip,AnimationClip>(overrides[i].Key,clip);
                        _equipmentAnimation.ApplyOverrides(overrides);
                    }
                    animator.runtimeAnimatorController=_equipmentAnimation;
                }
            }
        }
        private void EquipInventoryItem(WeaponItem item)
        {
            bool equipped=(_game.Progression.Equipment.Weapon?.Ordinal==item.Ordinal)||(_game.Progression.Equipment.Shield?.Ordinal==item.Ordinal);
            bool changed=equipped?_game.TryUnequipWeapon(item.Definition.Family==WeaponFamily.Shield):_game.TryEquipWeapon(item.Ordinal);
            _message=changed?(equipped?"Abgelegt: ":"Ausgerüstet: ")+item.DisplayName:"Ein Schild benötigt eine freie Nebenhand. Lege zuerst die Zweihandwaffe ab.";
            if(changed){SaveCharacter();UpdateEquipmentPresentation();}BuildMenu();
        }
    }
}
