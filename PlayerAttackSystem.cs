using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum SpellType {
    Empty,
    Shield,
    Projectile,
    Beam,
    Lightning,
    Spray
}

public class PlayerAttackSystem : MonoBehaviour {

    public struct CastSpell {
        public SpellType spellType;
        public Element[] spell;

        public override string ToString() {
            string n = $"Spell Type: {spellType}\nElements: ";
            for (int i = 0; i < spell.Length; i++) {
                n += spell[i];
                if (i != spell.Length - 1) n += ", ";
            }
            return n;
        }
    }

    [Serializable]
    public struct HingeGroup {
        public Transform hinge;
        public AnimationCurve curveX;
        public AnimationCurve curveY;
        public AnimationCurve curveZ;
        [HideInInspector]
        public Vector3 defaultRotation;
    }

    [Serializable]
    public class AnimationGroup {
        public HingeGroup[] hingeGroup;
        public float animTime;
        public float holdTime;
        public float resetTime;
        [Range(0f, 1f)]
        public float followUpWindow;

        float timer;

        public bool Playing { get { return timer != 0; } }
        public bool ResetWindow { get { return timer >= animTime + holdTime && timer < resetTime * followUpWindow + animTime + holdTime; } }
        public bool AnimReady { get { return timer == 0 || ResetWindow; } }

        public void Initilize() {
            for (int i = 0; i < hingeGroup.Length; i++) {
                hingeGroup[i].defaultRotation = hingeGroup[i].hinge.localEulerAngles;
            }
        }

        public void Reset() {
            timer = 0;
        }

        public void AdvanceAnim(float deltaTime, bool reset = false) {
            if (reset) timer = 0;
            timer += deltaTime;
            for (int i = 0; i < hingeGroup.Length; i++) {
                Vector3 nextRot;
                if (timer > animTime + holdTime) {
                    nextRot = Vector3.Lerp(new Vector3(hingeGroup[i].curveX.Evaluate(1), hingeGroup[i].curveY.Evaluate(1), hingeGroup[i].curveZ.Evaluate(1)) * 180, Vector3.zero, (timer - animTime - holdTime) / resetTime);
                }
                else if (timer > animTime) {
                    nextRot = new Vector3(hingeGroup[i].curveX.Evaluate(1), hingeGroup[i].curveY.Evaluate(1), hingeGroup[i].curveZ.Evaluate(1)) * 180;
                }
                else {
                    nextRot = new Vector3(hingeGroup[i].curveX.Evaluate(timer / animTime), hingeGroup[i].curveY.Evaluate(timer / animTime), hingeGroup[i].curveZ.Evaluate(timer / animTime)) * 180;
                }
                hingeGroup[i].hinge.localEulerAngles = hingeGroup[i].defaultRotation + nextRot;
            }

            if (timer > animTime + holdTime + resetTime) timer = 0;
        }
    }

    [Serializable]
    public struct HingeHold {
        public Transform hinge;
        public Vector3 rotation;
        [HideInInspector]
        public Vector3 defaultRotation;
    }

    [Serializable]
    public class HoldGroup {
        public HingeHold[] hingeGroup;
        public float animTime;
        public AnimationCurve lerpFlow;

        float timer;

        public bool Playing { get { return timer > 0 && timer < animTime; } }
        public bool Holding { get { return timer >= animTime; } }
        public bool Waiting { get { return timer <= 0; } }

        public void Initilize() {
            for (int i = 0; i < hingeGroup.Length; i++) {
                if (hingeGroup[i].hinge) hingeGroup[i].defaultRotation = hingeGroup[i].hinge.localEulerAngles;
            }
        }

        public void AdvanceAnim(float deltaTime, bool reset = false) {
            if (reset) timer = 0;
            timer = Mathf.Clamp(timer + deltaTime, 0, animTime);
            for (int i = 0; i < hingeGroup.Length; i++) {
                float t = lerpFlow.Evaluate(timer / animTime);
                Vector3 nextRot = Vector3.Lerp(hingeGroup[i].defaultRotation, hingeGroup[i].rotation, t);
                hingeGroup[i].hinge.localEulerAngles = nextRot;
            }
        }

        public void TransferAnim(float deltaTime, HoldGroup from, bool reset = false) {
            if (reset) timer = 0;
            timer = Mathf.Clamp(timer + deltaTime, 0, animTime);
            for (int i = 0; i < hingeGroup.Length; i++) {
                float t = timer / animTime;
                Vector3 nextRot = Vector3.Lerp(from.hingeGroup[i].rotation, hingeGroup[i].rotation, t);
                hingeGroup[i].hinge.localEulerAngles = nextRot;
            }
        }
    }

    public AnimationGroup primaryAttack;
    public AnimationGroup secondaryAttack;
    public float attackHoldTime;
    public HoldGroup staffSpell;
    public HoldGroup staffSpray;
    public HoldGroup staffCharge;
    public HoldGroup staffBeam;
    public HoldGroup staffLightning;
    public HoldGroup staffShield;
    public HoldGroup staffTargetSelf;
    public AnimationGroup staffAreaUp;
    public AnimationGroup staffAreaDown;
    public AnimationGroup staffAreaOut;
    public HoldGroup infuseSword;
    public AnimationGroup swordVertical;
    public AnimationGroup swordHorizontal;
    public AnimationGroup swordForward;
    public float targetSelfCastTime;
    public float infuseCastTime;
    public Transform castPoint;
    public List<GameObject> spellPrefabList;

    float attackHoldTimer;
    bool castingSpell;
    bool castingSelf;
    bool castingArea;
    bool infusingSword;
    bool infusedAttack;
    bool openWindow;
    float castTimer;
    float selfCastTimer;
    float swordInfuseTimer;

    CastSpell castSpell;
    CastSpell infusedSword;
    AbstractSpellEffects[] spellScript;

    void Start() {
        primaryAttack.Initilize();
        secondaryAttack.Initilize();
        staffSpell.Initilize();
        staffSpray.Initilize();
        staffCharge.Initilize();
        staffBeam.Initilize();
        staffLightning.Initilize();
        staffShield.Initilize();
        staffTargetSelf.Initilize();
        staffAreaUp.Initilize();
        staffAreaDown.Initilize();
        staffAreaOut.Initilize();
        infuseSword.Initilize();

        castSpell = new CastSpell();
        infusedSword = new CastSpell();
        spellScript = new AbstractSpellEffects[5];
    }

    void Update() {
        bool windowNeedsOpen = false;
        bool playingAnimation = false;
        bool castPrimaryHeld = Input.GetButton("Cast Primary");
        bool castPrimaryDown = Input.GetButtonDown("Cast Primary");
        bool castSelfDown = Input.GetButtonDown("Target Self");
        bool openSpellWindow = Input.GetButton("Spell Window");
        bool useSwordHeld = Input.GetButton("Use Sword");

        if (castingSpell) { // Continue playing current animations
            castingSpell = ContinueSpellCast(castPrimaryHeld);
        }
        else if (castingSelf) {
            castingSelf = ContinueSelfCast();
        }
        else if (castingArea) {
            castingArea = ContinueAreaCast();
        }
        else if (infusingSword) {
            infusingSword = ContinueInfusionCast();
        }
        else if (infusedAttack) {
            infusedAttack = ContinueInfusedAttack();
            if (!infusedAttack) {
                infusedSword.spellType = SpellType.Empty;
            }
        }
        else if (openWindow) {
            CraftSpell();
        }
        else if (primaryAttack.Playing || secondaryAttack.Playing) {
            ContinueSwordAttack();
        }

        playingAnimation = castingSpell || castingSelf || castingArea || infusingSword || infusedAttack;

        if (!playingAnimation) { // No animation playing, check for new command
            if (primaryAttack.Playing || secondaryAttack.Playing) {
                if (castPrimaryDown && useSwordHeld) {
                    attackHoldTimer = attackHoldTime;
                }
            }
            else {
                attackHoldTimer = 0;
                if (castPrimaryDown && !useSwordHeld) { // Main Spell Cast
                    castingSpell = true;
                    SetSpell();
                    Cast();
                }
                else if (castSelfDown && !useSwordHeld) { // Self Cast
                    castingSelf = true;
                    SetSpell();
                    CastSelf();
                }
                else if (castSelfDown && useSwordHeld) { // Area Cast
                    castingArea = true;
                    SetSpell();
                    CastArea();
                }
                else if (castPrimaryDown && useSwordHeld) {
                    if (infusedSword.spellType == SpellType.Empty) { // Sword is not infused, check for spell
                        SetSpell(true);
                        if (infusedSword.spellType == SpellType.Empty) { // No spell, attack as normal
                            attackHoldTimer = attackHoldTime;
                        }
                        else { // Infuse with spell
                            infusingSword = true;
                            CastInfusion();
                        }
                    }
                    else { // Sword is infused, cast infused spell
                        infusedAttack = true;
                        InfusedSwordAttack();
                    }
                }
                else if (openSpellWindow) {
                    CraftSpell();
                    windowNeedsOpen = true;
                }
                else {
                    BacktrackAnimations();
                }
            }
        }

        if (attackHoldTimer > 0 && castPrimaryHeld && useSwordHeld) {
            AttackSword();
        }

        if (windowNeedsOpen != openWindow) {
            SpellcraftWindow.instance.SetOpenning(windowNeedsOpen);
            openWindow = windowNeedsOpen;
        }
    }

    void Cast() {
        ResetAnimations();

        switch (castSpell.spellType) { // Choose which animation to use based on spell type
            case SpellType.Spray:
                staffSpray.AdvanceAnim(Time.deltaTime, true);
                break;
            case SpellType.Projectile:
                staffCharge.AdvanceAnim(Time.deltaTime, true);
                break;
            case SpellType.Beam:
                staffBeam.AdvanceAnim(Time.deltaTime, true);
                break;
            case SpellType.Lightning:
                staffLightning.AdvanceAnim(Time.deltaTime, true);
                break;
            case SpellType.Shield:
                staffShield.AdvanceAnim(Time.deltaTime, true);
                break;
            default:
                staffCharge.AdvanceAnim(Time.deltaTime, true);
                break;
        }

        CreateSpellPrefabs(castSpell);
    }

    bool ContinueSpellCast(bool castHeld) {
        if (castTimer > 0) { // Not used yet
            castTimer -= Time.deltaTime;
        }

        bool dontStop = false;

        if (staffSpray.Playing) staffSpray.AdvanceAnim(Time.deltaTime);
        else if (staffCharge.Playing) staffCharge.AdvanceAnim(Time.deltaTime);
        else if (staffBeam.Playing) staffBeam.AdvanceAnim(Time.deltaTime);
        else if (staffLightning.Playing) staffLightning.AdvanceAnim(Time.deltaTime);
        else if (staffShield.Playing) {
            staffShield.AdvanceAnim(Time.deltaTime);
            if (!castHeld) {
                dontStop = true;
            }
            if (staffShield.Holding) {
                castHeld = false;
                dontStop = true;
            }
        }

        if (!castHeld && spellScript != null && !dontStop) {
            foreach (AbstractSpellEffects script in spellScript) {
                if (script) script.Stop();
            }
        }

        return castHeld;
    }

    void CastSelf() {
        ResetAnimations();

        selfCastTimer = targetSelfCastTime;
        staffTargetSelf.AdvanceAnim(Time.deltaTime, true);
    }

    bool ContinueSelfCast() {
        if (staffTargetSelf.Playing) staffTargetSelf.AdvanceAnim(Time.deltaTime);

        selfCastTimer -= Time.deltaTime;

        return selfCastTimer > 0;
    }

    void CastArea() {
        ResetAnimations();

        switch (castSpell.spellType) {
            case SpellType.Shield:
            case SpellType.Lightning:
                staffAreaUp.AdvanceAnim(Time.deltaTime, true);
                break;
            case SpellType.Spray:
            case SpellType.Beam:
                staffAreaOut.AdvanceAnim(Time.deltaTime, true);
                break;
            case SpellType.Projectile:
                staffAreaDown.AdvanceAnim(Time.deltaTime, true);
                break;
            default:
                staffAreaOut.AdvanceAnim(Time.deltaTime, true);
                break;
        }
    }

    bool ContinueAreaCast() {
        if (staffAreaUp.Playing) {
            staffAreaUp.AdvanceAnim(Time.deltaTime);
            return true;
        }
        if (staffAreaOut.Playing) {
            staffAreaOut.AdvanceAnim(Time.deltaTime);
            return true;
        }
        if (staffAreaDown.Playing) {
            staffAreaDown.AdvanceAnim(Time.deltaTime);
            return true;
        }
        return false;
    }

    void CastInfusion() {
        ResetAnimations(false);

        swordInfuseTimer = infuseCastTime;
        infuseSword.AdvanceAnim(Time.deltaTime, true);
    }

    bool ContinueInfusionCast() {
        if (!staffSpell.Waiting) staffSpell.AdvanceAnim(-Time.deltaTime);
        if (infuseSword.Playing) infuseSword.AdvanceAnim(Time.deltaTime);

        swordInfuseTimer -= Time.deltaTime;

        return swordInfuseTimer > 0;
    }

    void InfusedSwordAttack() {
        print($"Casting {infusedSword.spellType} from sword");
        ResetAnimations();
        attackHoldTimer = 0;

        switch (infusedSword.spellType) {
            case SpellType.Shield:
            case SpellType.Projectile:
                swordVertical.AdvanceAnim(Time.deltaTime, true);
                break;
            case SpellType.Beam:
                swordHorizontal.AdvanceAnim(Time.deltaTime, true);
                break;
            case SpellType.Spray:
            case SpellType.Lightning:
                swordForward.AdvanceAnim(Time.deltaTime, true);
                break;
        }
    }

    bool ContinueInfusedAttack() {
        if (swordVertical.Playing) {
            swordVertical.AdvanceAnim(Time.deltaTime);
            return true;
        }
        if (swordHorizontal.Playing) {
            swordHorizontal.AdvanceAnim(Time.deltaTime);
            return true;
        }
        if (swordForward.Playing) {
            swordForward.AdvanceAnim(Time.deltaTime);
            return true;
        }
        return false;
    }

    void CraftSpell() {
        ResetAnimations(false);

        if (!staffSpell.Holding) staffSpell.AdvanceAnim(Time.deltaTime);
    }

    void ResetAnimations(bool spell = true) {
        if (spell && !staffSpell.Waiting) staffSpell.AdvanceAnim(0, true);
        if (!staffSpray.Waiting) staffSpray.AdvanceAnim(0, true);
        if (!staffCharge.Waiting) staffCharge.AdvanceAnim(0, true);
        if (!staffBeam.Waiting) staffBeam.AdvanceAnim(0, true);
        if (!staffLightning.Waiting) staffLightning.AdvanceAnim(0, true);
        if (!staffShield.Waiting) staffShield.AdvanceAnim(0, true);
        if (!staffTargetSelf.Waiting) staffTargetSelf.AdvanceAnim(0, true);
        if (!infuseSword.Waiting) infuseSword.AdvanceAnim(0, true);
    }

    void BacktrackAnimations() {
        if (!staffSpell.Waiting) staffSpell.AdvanceAnim(-Time.deltaTime);
        else if (!staffSpray.Waiting) staffSpray.AdvanceAnim(-Time.deltaTime);
        else if (!staffCharge.Waiting) staffCharge.AdvanceAnim(-Time.deltaTime);
        else if (!staffBeam.Waiting) staffBeam.AdvanceAnim(-Time.deltaTime);
        else if (!staffLightning.Waiting) staffLightning.AdvanceAnim(-Time.deltaTime);
        else if (!staffShield.Waiting) staffShield.AdvanceAnim(-Time.deltaTime);
        else if (!staffTargetSelf.Waiting) staffTargetSelf.AdvanceAnim(-Time.deltaTime);
        else if (!infuseSword.Waiting) infuseSword.AdvanceAnim(-Time.deltaTime);
    }

    void AttackSword() {
        attackHoldTimer -= Time.deltaTime;
        if (primaryAttack.AnimReady && !secondaryAttack.Playing) { // Can attack, start animation
            ResetAnimations(false);
            if (primaryAttack.ResetWindow) { // Done with primary attack, switch to followup
                primaryAttack.Reset();
                secondaryAttack.AdvanceAnim(Time.deltaTime, true);
            }
            else {
                primaryAttack.AdvanceAnim(Time.deltaTime, true);
            }
            attackHoldTimer = 0;
        }
    }

    void ContinueSwordAttack() {
        if (!staffSpell.Waiting) staffSpell.AdvanceAnim(-Time.deltaTime);

        if (primaryAttack.Playing) { // First swing animation
            primaryAttack.AdvanceAnim(Time.deltaTime);
        }
        else if (secondaryAttack.Playing) { // Follow-up swing animation
            secondaryAttack.AdvanceAnim(Time.deltaTime);
        }
    }

    void SetSpell(bool infusion = false) {
        List<Element> toCast = SpellcraftWindow.instance.GetSpell();
        
        if (infusion) {
            infusedSword.spellType = GetType(toCast);
            infusedSword.spell = toCast.ToArray();
            print(infusedSword.ToString());
        }
        else {
            castSpell.spellType = GetType(toCast);
            castSpell.spell = toCast.ToArray();
            print(castSpell.ToString());
        }
        
        ListPool<Element>.Add(toCast);
    }

    SpellType GetType(List<Element> typeCheck) {
        if (typeCheck.Contains(Element.Shield)) return SpellType.Shield;
        if (typeCheck.Contains(Element.Earth) || typeCheck.Contains(Element.Ice)) return SpellType.Projectile;
        if (typeCheck.Contains(Element.Arcane) || typeCheck.Contains(Element.Life)) return SpellType.Beam;
        if (typeCheck.Contains(Element.Steam) || typeCheck.Contains(Element.Poison)) return SpellType.Spray;
        if (typeCheck.Contains(Element.Lightning)) return SpellType.Lightning;
        if (typeCheck.Contains(Element.Water) || typeCheck.Contains(Element.Fire) || typeCheck.Contains(Element.Cold)) return SpellType.Spray;
        return SpellType.Empty;
    }

    void CreateSpellPrefabs(CastSpell spell) {
        int[] elementCounts = new int[11];
        foreach (Element element in spell.spell) {
            elementCounts[(int)element]++;
        }

        if (spell.spellType == SpellType.Shield) {
            List<Element> elementList = new List<Element>(spell.spell);
            elementList.Remove(Element.Shield);
            SpellType type = GetType(elementList);

            if (type == SpellType.Empty) {
                spellScript[0] = Instantiate(spellPrefabList[18], castPoint).GetComponent<AbstractSpellEffects>();
                spellScript[0].AddElement(elementCounts[(int)Element.Shield], Element.Shield);
            }
            else if (type == SpellType.Spray) {
                if (elementCounts[(int)Element.Fire] > 0) {
                    spellScript[0] = Instantiate(spellPrefabList[19], castPoint).GetComponent<AbstractSpellEffects>();
                    spellScript[0].AddElement(elementCounts[(int)Element.Fire], Element.Fire);
                    if (elementCounts[(int)Element.Steam] > 0) spellScript[0].AddElement(elementCounts[(int)Element.Steam], Element.Steam);
                    if (elementCounts[(int)Element.Poison] > 0) spellScript[0].AddElement(elementCounts[(int)Element.Poison], Element.Poison);
                }
                else if (elementCounts[(int)Element.Water] > 0) {
                    spellScript[0] = Instantiate(spellPrefabList[20], castPoint).GetComponent<AbstractSpellEffects>();
                    spellScript[0].AddElement(elementCounts[(int)Element.Water], Element.Water);
                    if (elementCounts[(int)Element.Steam] > 0) spellScript[0].AddElement(elementCounts[(int)Element.Steam], Element.Steam);
                    if (elementCounts[(int)Element.Poison] > 0) spellScript[0].AddElement(elementCounts[(int)Element.Poison], Element.Poison);
                }
                else if (elementCounts[(int)Element.Cold] > 0) {
                    spellScript[0] = Instantiate(spellPrefabList[21], castPoint).GetComponent<AbstractSpellEffects>();
                    spellScript[0].AddElement(elementCounts[(int)Element.Cold], Element.Cold);
                    if (elementCounts[(int)Element.Poison] > 0) spellScript[0].AddElement(elementCounts[(int)Element.Poison], Element.Poison);
                }
                else if (elementCounts[(int)Element.Poison] > 0) {
                    spellScript[0] = Instantiate(spellPrefabList[23], castPoint).GetComponent<AbstractSpellEffects>();
                    spellScript[0].AddElement(elementCounts[(int)Element.Poison], Element.Poison);
                    if (elementCounts[(int)Element.Steam] > 0) spellScript[0].AddElement(elementCounts[(int)Element.Steam], Element.Steam);
                }
                else if (elementCounts[(int)Element.Steam] > 0) {
                    spellScript[0] = Instantiate(spellPrefabList[22], castPoint).GetComponent<AbstractSpellEffects>();
                    spellScript[0].AddElement(elementCounts[(int)Element.Steam], Element.Steam);
                }
            }
            else if (type == SpellType.Lightning) {
                spellScript[0] = Instantiate(spellPrefabList[24], castPoint).GetComponent<AbstractSpellEffects>();
                spellScript[0].AddElement(elementCounts[(int)Element.Lightning], Element.Lightning);
                if (elementCounts[(int)Element.Fire] > 0) spellScript[0].AddElement(elementCounts[(int)Element.Fire], Element.Fire);
                if (elementCounts[(int)Element.Cold] > 0) spellScript[0].AddElement(elementCounts[(int)Element.Cold], Element.Cold);
                if (elementCounts[(int)Element.Water] > 0) spellScript[0].AddElement(elementCounts[(int)Element.Water], Element.Water);
            }
            else if (type == SpellType.Beam) {
                if (elementCounts[(int)Element.Arcane] > 0) {
                    spellScript[0] = Instantiate(spellPrefabList[25], castPoint).GetComponent<AbstractSpellEffects>();
                    spellScript[0].AddElement(elementCounts[(int)Element.Arcane], Element.Arcane);
                }
                if (elementCounts[(int)Element.Life] > 0) {
                    spellScript[0] = Instantiate(spellPrefabList[26], castPoint).GetComponent<AbstractSpellEffects>();
                    spellScript[0].AddElement(elementCounts[(int)Element.Life], Element.Life);
                }

                if (elementCounts[(int)Element.Steam] > 0) spellScript[0].AddElement(elementCounts[(int)Element.Steam], Element.Steam);
                if (elementCounts[(int)Element.Poison] > 0) spellScript[0].AddElement(elementCounts[(int)Element.Poison], Element.Poison);
                if (elementCounts[(int)Element.Lightning] > 0) spellScript[0].AddElement(elementCounts[(int)Element.Lightning], Element.Lightning);
                if (elementCounts[(int)Element.Fire] > 0) spellScript[0].AddElement(elementCounts[(int)Element.Fire], Element.Fire);
                if (elementCounts[(int)Element.Water] > 0) spellScript[0].AddElement(elementCounts[(int)Element.Water], Element.Water);
                if (elementCounts[(int)Element.Cold] > 0) spellScript[0].AddElement(elementCounts[(int)Element.Cold], Element.Cold);
            }
            else if (type == SpellType.Projectile) {
                if (elementCounts[(int)Element.Earth] > 0) {
                    if (elementList.Contains(Element.Steam) || elementList.Contains(Element.Poison) || elementList.Contains(Element.Fire) || elementList.Contains(Element.Water) || elementList.Contains(Element.Cold)) {
                        spellScript[0] = Instantiate(spellPrefabList[28], castPoint).GetComponent<AbstractSpellEffects>();
                    }
                    else {
                        spellScript[0] = Instantiate(spellPrefabList[27], castPoint).GetComponent<AbstractSpellEffects>();
                    }

                    spellScript[0].AddElement(elementCounts[(int)Element.Earth], Element.Earth);
                    if (elementCounts[(int)Element.Ice] > 0) spellScript[0].AddElement(elementCounts[(int)Element.Ice], Element.Ice);
                }
                else if (elementCounts[(int)Element.Ice] > 0) {
                    if (elementList.Contains(Element.Steam) || elementList.Contains(Element.Poison) || elementList.Contains(Element.Fire) || elementList.Contains(Element.Water) || elementList.Contains(Element.Cold)) {
                        spellScript[0] = Instantiate(spellPrefabList[30], castPoint).GetComponent<AbstractSpellEffects>();
                    }
                    else {
                        spellScript[0] = Instantiate(spellPrefabList[29], castPoint).GetComponent<AbstractSpellEffects>();
                    }

                    spellScript[0].AddElement(elementCounts[(int)Element.Ice], Element.Ice);
                }

                if (elementCounts[(int)Element.Arcane] > 0) spellScript[0].AddElement(elementCounts[(int)Element.Arcane], Element.Arcane);
                if (elementCounts[(int)Element.Life] > 0) spellScript[0].AddElement(elementCounts[(int)Element.Life], Element.Life);
                if (elementCounts[(int)Element.Steam] > 0) spellScript[0].AddElement(elementCounts[(int)Element.Steam], Element.Steam);
                if (elementCounts[(int)Element.Poison] > 0) spellScript[0].AddElement(elementCounts[(int)Element.Poison], Element.Poison);
                if (elementCounts[(int)Element.Lightning] > 0) spellScript[0].AddElement(elementCounts[(int)Element.Lightning], Element.Lightning);
                if (elementCounts[(int)Element.Fire] > 0) spellScript[0].AddElement(elementCounts[(int)Element.Fire], Element.Fire);
                if (elementCounts[(int)Element.Water] > 0) spellScript[0].AddElement(elementCounts[(int)Element.Water], Element.Water);
                if (elementCounts[(int)Element.Cold] > 0) spellScript[0].AddElement(elementCounts[(int)Element.Cold], Element.Cold);
            }
        }
        else if (spell.spellType == SpellType.Projectile) {
            if (elementCounts[(int)Element.Earth] > 0 && elementCounts[(int)Element.Ice] > 0) {
                spellScript[0] = Instantiate(spellPrefabList[16], castPoint).GetComponent<AbstractSpellEffects>();
                spellScript[0].AddElement(elementCounts[(int)Element.Earth], Element.Earth);
                spellScript[0].AddElement(elementCounts[(int)Element.Ice], Element.Ice);
            }
            else if (elementCounts[(int)Element.Earth] > 0) {
                spellScript[0] = Instantiate(spellPrefabList[15], castPoint).GetComponent<AbstractSpellEffects>();
                spellScript[0].AddElement(elementCounts[(int)Element.Earth], Element.Earth);
            }
            else if (elementCounts[(int)Element.Ice] > 0) {
                spellScript[0] = Instantiate(spellPrefabList[17], castPoint).GetComponent<AbstractSpellEffects>();
                spellScript[0].AddElement(elementCounts[(int)Element.Ice], Element.Ice);
            }

            if (elementCounts[(int)Element.Arcane] > 0) spellScript[0].AddElement(elementCounts[(int)Element.Arcane], Element.Arcane);
            if (elementCounts[(int)Element.Life] > 0) spellScript[0].AddElement(elementCounts[(int)Element.Life], Element.Life);
            if (elementCounts[(int)Element.Steam] > 0) spellScript[0].AddElement(elementCounts[(int)Element.Steam], Element.Steam);
            if (elementCounts[(int)Element.Poison] > 0) spellScript[0].AddElement(elementCounts[(int)Element.Poison], Element.Poison);
            if (elementCounts[(int)Element.Lightning] > 0) spellScript[0].AddElement(elementCounts[(int)Element.Lightning], Element.Lightning);
            if (elementCounts[(int)Element.Fire] > 0) spellScript[0].AddElement(elementCounts[(int)Element.Fire], Element.Fire);
            if (elementCounts[(int)Element.Water] > 0) spellScript[0].AddElement(elementCounts[(int)Element.Water], Element.Water);
            if (elementCounts[(int)Element.Cold] > 0) spellScript[0].AddElement(elementCounts[(int)Element.Cold], Element.Cold);
        }
        else if (spell.spellType == SpellType.Beam) {
            int pos = 0;

            if (elementCounts[(int)Element.Steam] > 0) {
                spellScript[pos] = Instantiate(spellPrefabList[10], castPoint).GetComponent<AbstractSpellEffects>();
                if (elementCounts[(int)Element.Arcane] > 0) spellScript[pos].AddElement(elementCounts[(int)Element.Arcane], Element.Arcane);
                if (elementCounts[(int)Element.Life] > 0) spellScript[pos].AddElement(elementCounts[(int)Element.Life], Element.Life);
                spellScript[pos++].AddElement(elementCounts[(int)Element.Steam], Element.Steam);
            }
            if (elementCounts[(int)Element.Poison] > 0) {
                spellScript[pos] = Instantiate(spellPrefabList[11], castPoint).GetComponent<AbstractSpellEffects>();
                if (elementCounts[(int)Element.Arcane] > 0) spellScript[pos].AddElement(elementCounts[(int)Element.Arcane], Element.Arcane);
                if (elementCounts[(int)Element.Life] > 0) spellScript[pos].AddElement(elementCounts[(int)Element.Life], Element.Life);
                spellScript[pos++].AddElement(elementCounts[(int)Element.Poison], Element.Poison);
            }
            if (elementCounts[(int)Element.Fire] > 0) {
                spellScript[pos] = Instantiate(spellPrefabList[8], castPoint).GetComponent<AbstractSpellEffects>();
                if (elementCounts[(int)Element.Arcane] > 0) spellScript[pos].AddElement(elementCounts[(int)Element.Arcane], Element.Arcane);
                if (elementCounts[(int)Element.Life] > 0) spellScript[pos].AddElement(elementCounts[(int)Element.Life], Element.Life);
                spellScript[pos++].AddElement(elementCounts[(int)Element.Fire], Element.Fire);
            }
            if (elementCounts[(int)Element.Water] > 0) {
                spellScript[pos] = Instantiate(spellPrefabList[13], castPoint).GetComponent<AbstractSpellEffects>();
                if (elementCounts[(int)Element.Arcane] > 0) spellScript[pos].AddElement(elementCounts[(int)Element.Arcane], Element.Arcane);
                if (elementCounts[(int)Element.Life] > 0) spellScript[pos].AddElement(elementCounts[(int)Element.Life], Element.Life);
                spellScript[pos++].AddElement(elementCounts[(int)Element.Water], Element.Water);
            }
            if (elementCounts[(int)Element.Cold] > 0) {
                spellScript[pos] = Instantiate(spellPrefabList[12], castPoint).GetComponent<AbstractSpellEffects>();
                if (elementCounts[(int)Element.Arcane] > 0) spellScript[pos].AddElement(elementCounts[(int)Element.Arcane], Element.Arcane);
                if (elementCounts[(int)Element.Life] > 0) spellScript[pos].AddElement(elementCounts[(int)Element.Life], Element.Life);
                spellScript[pos++].AddElement(elementCounts[(int)Element.Cold], Element.Cold);
            }
            if (elementCounts[(int)Element.Lightning] > 0) {
                spellScript[pos] = Instantiate(spellPrefabList[9], castPoint).GetComponent<AbstractSpellEffects>();
                if (elementCounts[(int)Element.Arcane] > 0) spellScript[pos].AddElement(elementCounts[(int)Element.Arcane], Element.Arcane);
                if (elementCounts[(int)Element.Life] > 0) spellScript[pos].AddElement(elementCounts[(int)Element.Life], Element.Life);
                spellScript[pos++].AddElement(elementCounts[(int)Element.Lightning], Element.Lightning);
            }

            if (elementCounts[(int)Element.Arcane] > 0) {
                spellScript[pos] = Instantiate(spellPrefabList[7], castPoint).GetComponent<AbstractSpellEffects>();
                if (pos > 0) spellScript[pos].RemoveOneShots();
                spellScript[pos].AddElement(elementCounts[(int)Element.Arcane], Element.Arcane);
            }
            if (elementCounts[(int)Element.Life] > 0) {
                spellScript[pos] = Instantiate(spellPrefabList[14], castPoint).GetComponent<AbstractSpellEffects>();
                if (pos > 0) spellScript[pos].RemoveOneShots();
                spellScript[pos].AddElement(elementCounts[(int)Element.Life], Element.Life);
            }
        }
        else if (spell.spellType == SpellType.Lightning) {
            spellScript[0] = Instantiate(spellPrefabList[6], castPoint).GetComponent<AbstractSpellEffects>();
            spellScript[0].AddElement(elementCounts[(int)Element.Lightning], Element.Lightning);
            if (elementCounts[(int)Element.Fire] > 0) spellScript[0].AddElement(elementCounts[(int)Element.Fire], Element.Fire);
            if (elementCounts[(int)Element.Cold] > 0) spellScript[0].AddElement(elementCounts[(int)Element.Cold], Element.Cold);
        }
        else if (spell.spellType == SpellType.Spray) {
            int pos = 0;

            if (elementCounts[(int)Element.Steam] > 0) {
                spellScript[pos] = Instantiate(spellPrefabList[3], castPoint).GetComponent<AbstractSpellEffects>();
                if (pos > 0) spellScript[pos].RemoveOneShots();
                spellScript[pos++].AddElement(elementCounts[(int)Element.Steam], Element.Steam);
            }
            if (elementCounts[(int)Element.Poison] > 0) {
                spellScript[pos] = Instantiate(spellPrefabList[4], castPoint).GetComponent<AbstractSpellEffects>();
                if (pos > 0) spellScript[pos].RemoveOneShots();
                spellScript[pos++].AddElement(elementCounts[(int)Element.Poison], Element.Poison);
            }
            if (elementCounts[(int)Element.Fire] > 0) {
                spellScript[pos] = Instantiate(spellPrefabList[0], castPoint).GetComponent<AbstractSpellEffects>();
                if (pos > 0) spellScript[pos].RemoveOneShots();
                spellScript[pos++].AddElement(elementCounts[(int)Element.Fire], Element.Fire);
            }
            if (elementCounts[(int)Element.Water] > 0) {
                spellScript[pos] = Instantiate(spellPrefabList[1], castPoint).GetComponent<AbstractSpellEffects>();
                if (pos > 0) spellScript[pos].RemoveOneShots();
                spellScript[pos++].AddElement(elementCounts[(int)Element.Water], Element.Water);
            }
            if (elementCounts[(int)Element.Cold] > 0) {
                spellScript[pos] = Instantiate(spellPrefabList[2], castPoint).GetComponent<AbstractSpellEffects>();
                if (pos > 0) spellScript[pos].RemoveOneShots();
                spellScript[pos++].AddElement(elementCounts[(int)Element.Cold], Element.Cold);
            }
            if (elementCounts[(int)Element.Lightning] > 0) {
                spellScript[pos] = Instantiate(spellPrefabList[5], castPoint).GetComponent<AbstractSpellEffects>();
                if (pos > 0) spellScript[pos].RemoveOneShots();
                spellScript[pos++].AddElement(elementCounts[(int)Element.Lightning], Element.Lightning);
            }
        }
        else {
            spellScript[0] = Instantiate(spellPrefabList[31], castPoint).GetComponent<AbstractSpellEffects>();
        }
    }
}
