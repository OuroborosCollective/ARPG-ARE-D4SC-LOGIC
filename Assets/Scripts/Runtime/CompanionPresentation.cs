using System.Collections.Generic;
using AreArpg.Core;
using UnityEngine;

namespace AreArpg.UnityClient
{
    public sealed partial class ArenaRuntime
    {
        private int _pendingCompanionAction=-1;
        private Transform _companionVisual,_companionWeaponVisual,_companionShieldVisual;
        private Animator _companionAnimator;
        private AnimatorOverrideController _companionBowController;
        private string _companionWeaponId="",_companionShieldId="";
        private int _companionLastFlash;
        private void QueueCompanionAction(int action)
        {if(action<0||action>2)return;CloseMenu();_game.SetMoveInput(0,0);_pendingCompanionAction=action;}
        private void ProcessCompanionAction()
        {
            if(_pendingCompanionAction<0)return;
            int action=_pendingCompanionAction;_pendingCompanionAction=-1;
            var result=action==0?_game.RecruitHunter():action==1?_game.DismissCompanion():_game.FeedCompanion();
            _message=string.IsNullOrEmpty(result.Text)?"Gehe zum lebenden Jäger, um mit ihm zu sprechen.":result.Text;
            _atmosphere.AcceptedSpeech(result);if(result.Accepted)SaveCharacter();
        }
        private void BuildCompanionTab(RectTransform body,Vector2 size)
        {
            var c=_game.Progression.Companion;var p=_game.Progression;bool joined=c.HasJoined;
            var panel=AshenUiTheme.Frame("CompanionPanel",body,new Rect(0,0,size.x,size.y),"Der gerettete Jäger");
            var info=AshenUiTheme.Label("CompanionStats",panel.transform,16,AshenUiTheme.Parchment,TextAnchor.UpperLeft);
            AshenUiTheme.Place(info.rectTransform,new Rect(20,42,size.x-40,size.y-112));
            if(!joined) info.text=p.Road.Rescued?"Der Jäger hat deine Rettung nicht vergessen. Sprich ihn an der Straße an, damit er dich begleitet.\n\nEr gewinnt eigene Erfahrung durch seine Treffer. Du kannst ihn an einem sicheren Ort warten lassen und später wieder abholen.":"Auf der Straße gerät ein Jäger in Gefahr. Wenn er überlebt, kann er sich dir anschließen.";
            else
            {
                var r=p.Social.Forge.Archive.Get("hunter").RelationshipTo("player");
                string state=c.Status==CompanionStatus.Following?"An deiner Seite":c.Status==CompanionStatus.Waiting?"Wartet auf dich":"Verstorben";
                info.text="<size=22><b>"+state+"</b></size>\nStufe "+c.Level+" · Erfahrung "+c.TotalExperience+"\nGesundheit "+c.Health+" / "+CompanionState.MaximumHealth+" · Hunger "+c.Hunger/10+" % · Müdigkeit "+c.Fatigue/10+" %\n\nWaffenübung: Rang "+c.PracticeLevel(0)+" · "+c.PracticeExperience(0)+" Erfahrung\nWaffe: "+_game.CompanionWeaponDefinition.Name+"\nSchild: "+(_game.CompanionShield?.DisplayName??"Keiner")+"\n\nVertrauen "+r.Trust+" · Dankbarkeit "+r.Gratitude+"\n"+(c.Status==CompanionStatus.Dead?"Sein Tod bleibt Teil eurer Geschichte. Überlassene Ausrüstung kannst du an seinem Leichnam zurücknehmen.":"Waffen und Schilde kannst du ihm in der Tasche übergeben. Proviant versorgt ihn, ohne ihm Erfahrung zu schenken.");
                if(!_game.CompanionPresent)info.text+="\nAufenthaltsort: "+CompanionAreaName(c.Area)+".";
            }
            float width=(size.x-64)/3;bool near=joined?_game.CanManageCompanionEquipment:CanShowHunterAction();
            var recruit=AshenUiTheme.MenuButton("CompanionRecruit",panel.transform,new Rect(20,size.y-56,width,38),joined?"WIEDER MITKOMMEN":"MITKOMMEN",()=>QueueCompanionAction(0),fontSize:13);
            recruit.interactable=near&&c.Status!=CompanionStatus.Following&&c.Status!=CompanionStatus.Dead&&p.Road.Rescued;
            var wait=AshenUiTheme.MenuButton("CompanionWait",panel.transform,new Rect(32+width,size.y-56,width,38),"HIER WARTEN",()=>QueueCompanionAction(1),fontSize:13);
            wait.interactable=near&&c.Status==CompanionStatus.Following;
            var care=AshenUiTheme.MenuButton("CompanionCare",panel.transform,new Rect(44+width*2,size.y-56,width,38),"PROVIANT GEBEN",()=>QueueCompanionAction(2),fontSize:13);
            care.interactable=joined&&near&&c.Health>0&&p.Social.Provisions>0&&(c.Health<CompanionState.MaximumHealth||c.Hunger>=400||c.Fatigue>=300);
        }
        private static string CompanionAreaName(int area) => area==2?"Immergrün":area==3?"Waldstraße":area==4?"Alte Mühle":area==10?"Aschenzoll":"Eure letzte gemeinsame Reise";
        private void EquipCompanionInventoryItem(WeaponItem item)
        {
            var c=_game.Progression.Companion;bool worn=c.WeaponOrdinal==item.Ordinal||c.ShieldOrdinal==item.Ordinal;
            bool accepted=worn?_game.TryUnequipCompanionWeapon(item.Definition.Family==WeaponFamily.Shield):_game.TryEquipCompanionWeapon(item.Ordinal);
            _message=accepted?(worn?"Zurück in deiner Tasche: ":"Jäger ausgerüstet: ")+item.DisplayName:"Gehe zum Jäger. Ein Schild benötigt eine freie Nebenhand.";
            if(accepted){SaveCharacter();UpdateEquipmentPresentation();UpdateCompanionPresentation(1f);}BuildMenu();
        }
        private void ClearCompanionVisuals()
        {
            if(_companionVisual!=null)Destroy(_companionVisual.gameObject);
            if(_companionBowController!=null)Destroy(_companionBowController);
            _companionVisual=_companionWeaponVisual=_companionShieldVisual=null;_companionAnimator=null;_companionBowController=null;_companionWeaponId=_companionShieldId="";_companionLastFlash=0;
        }
        private Transform CompanionHand(bool left)
        {
            if(_companionAnimator!=null&&_companionAnimator.isHuman)return _companionAnimator.GetBoneTransform(left?HumanBodyBones.LeftHand:HumanBodyBones.RightHand)??_companionVisual;
            return _companionVisual;
        }
        private void UpdateCompanionPresentation(float alpha)
        {
            var c=_game.Progression.Companion;
            if(!_game.CompanionPresent){ClearCompanionVisuals();return;}
            if(_companionVisual==null)
            {
                _companionVisual=Shape(PrimitiveType.Capsule,"ARE_Companion_Hunter",Vector3.up,new Vector3(.8f,1,.8f),new Color(.27f,.45f,.25f));
                PassiveArtVisuals.TryBindGrounded(_companionVisual,"RoadHunter",0);
                foreach(var part in _companionVisual.GetComponentsInChildren<Transform>(true))if(part.name=="HunterBow")part.gameObject.SetActive(false);
                _companionAnimator=_companionVisual.GetComponentInChildren<Animator>();
                if(_companionAnimator!=null){_companionAnimator.applyRootMotion=false;_companionAnimator.cullingMode=AnimatorCullingMode.AlwaysAnimate;_companionAnimator.runtimeAnimatorController=Resources.Load<RuntimeAnimatorController>("ARE/Visuals/EntryMelee");}
            }
            float ground=GroundHeight(Mathf.Lerp(c.PreviousXmm,c.Xmm,alpha)/1000f,Mathf.Lerp(c.PreviousZmm,c.Zmm,alpha)/1000f);
            _companionVisual.position=new Vector3(Mathf.Lerp(c.PreviousXmm,c.Xmm,alpha)/1000f,ground+(c.Health>0?1:.3f),Mathf.Lerp(c.PreviousZmm,c.Zmm,alpha)/1000f);
            var direction=new Vector3(c.Xmm-c.PreviousXmm,0,c.Zmm-c.PreviousZmm);
            if(c.AttackFlash>0){var target=_game.Monsters.Find(x=>x.Id==c.TargetId);if(target!=null)direction=new Vector3(target.Xmm-c.Xmm,0,target.Zmm-c.Zmm);}
            if(c.Health<=0)_companionVisual.rotation=Quaternion.Euler(90,0,0);
            else if(direction.sqrMagnitude>0)_companionVisual.rotation=Quaternion.LookRotation(direction);
            if(_companionAnimator!=null)
            {
                _companionAnimator.enabled=c.Health>0;
                if(c.Health>0){_companionAnimator.SetFloat("MoveMagnitude",c.Xmm!=c.PreviousXmm||c.Zmm!=c.PreviousZmm?1:0);if(c.AttackFlash>_companionLastFlash)_companionAnimator.SetTrigger("Melee");}
            }
            _companionLastFlash=c.AttackFlash;
            var weapon=_game.CompanionWeaponDefinition;string shield=_game.CompanionShield?.Definition.Id??"none";
            if(_companionWeaponId!=weapon.Id)
            {
                if(_companionWeaponVisual!=null)Destroy(_companionWeaponVisual.gameObject);
                _companionWeaponVisual=EquipmentVisual(weapon.VisualKey,CompanionHand(weapon.Family==WeaponFamily.Bow),"CompanionWeapon_"+weapon.Id);_companionWeaponId=weapon.Id;
                if(_companionAnimator!=null)
                {
                    var controller=Resources.Load<RuntimeAnimatorController>("ARE/Visuals/EntryMelee");
                    if(weapon.Family==WeaponFamily.Bow&&controller!=null)
                    {
                        if(_companionBowController==null)
                        {
                            _companionBowController=new AnimatorOverrideController(controller);
                            var clips=new List<KeyValuePair<AnimationClip,AnimationClip>>();_companionBowController.GetOverrides(clips);
                            var shot=Resources.Load<AnimationClip>("ARE/Visuals/WeaponBowShot");
                            if(shot!=null){for(int i=0;i<clips.Count;i++)if(clips[i].Key.name.Contains("Strike"))clips[i]=new KeyValuePair<AnimationClip,AnimationClip>(clips[i].Key,shot);_companionBowController.ApplyOverrides(clips);}
                        }
                        controller=_companionBowController;
                    }
                    _companionAnimator.runtimeAnimatorController=controller;
                }
            }
            if(_companionShieldId!=shield)
            {
                if(_companionShieldVisual!=null)Destroy(_companionShieldVisual.gameObject);
                _companionShieldVisual=_game.CompanionShield==null?null:EquipmentVisual(_game.CompanionShield.Definition.VisualKey,CompanionHand(true),"CompanionShield_"+shield);_companionShieldId=shield;
            }
        }
    }
}
