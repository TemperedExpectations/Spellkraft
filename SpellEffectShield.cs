using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpellEffectShield : AbstractSpellEffects {

    public GameObject shield;
    public int count = 1;
    public float radius = 5;
    public float angleOffset = 20;
    public float lowerHeight = .7f;
    public float durationMult;

    List<Transform> shieldList;
    float loopVolume;
    float startTimeIncrement;
    float stopTimeIncrement;

    public override void AddElement(int strength, Element element) {
        if (elementList == null) elementList = new Dictionary<Element, int>();

        if (elementList.ContainsKey(element)) elementList[element] += strength;
        else elementList.Add(element, strength);

        if (elementList.Count == 1) {
            Duration += Duration * (strength - 1) * durationMult;
        }
    }

    public override void RemoveOneShots() {
        if (castSound) {
            if (castSound.isPlaying) castSound.Stop();
            castSound.enabled = false;
        }
        if (loopSound) {
            if (loopSound.isPlaying) loopSound.Stop();
            loopSound.enabled = false;
        }
    }

    public override void Stop() {
        if (Stopping) {
            return;
        }
        Stopping = true;

        // cleanup particle systems
        foreach (ParticleSystem p in gameObject.GetComponentsInChildren<ParticleSystem>()) {
            p.Stop();
        }
        foreach (Transform shieldItem in shieldList) {
            if (shieldItem) {
                TriggerableObject triggerable = shieldItem.GetComponent<TriggerableObject>();
                if (triggerable) {
                    foreach (TriggerableObject.SpellEffects effect in triggerable.spellEffects) {
                        if (elementList.ContainsKey(effect.element) && effect.destroyEffect) {
                            Instantiate(effect.destroyEffect, triggerable.transform);
                        }
                    }

                    triggerable.Stop();
                }
            }
        }
        if (loopSound) {
            if (loopSound.isPlaying) loopSound.Stop();
            loopSound.enabled = false;
        }

        StartCoroutine(CleanupEverythingCoRoutine());
    }

    IEnumerator CleanupEverythingCoRoutine() {
        yield return new WaitForSeconds(StopTime);
        
        foreach (Transform shieldItem in shieldList) {
            if (shieldItem) Destroy(shieldItem.gameObject);
        }

        Destroy(gameObject);
    }

    protected override void StartParticleSystems() {
        shieldList = new List<Transform>();

        for (int i = 0; i < count; i++) {
            Quaternion angle = Quaternion.AngleAxis(angleOffset * i - ((count - 1) * angleOffset * .5f), Vector3.up);
            Vector3 dir = transform.forward;
            dir.y = 0;
            dir = angle * dir.normalized;
            shieldList.Add(Instantiate(shield, transform.position + dir * radius + Vector3.down * lowerHeight, Quaternion.Euler(0, transform.rotation.eulerAngles.y, 0) * angle).transform);
            if (shieldList[shieldList.Count - 1].GetComponent<AbstractSpellEffects>())
                shieldList[shieldList.Count - 1].GetComponent<AbstractSpellEffects>().Duration = Duration;

            TriggerableObject triggerable = shieldList[shieldList.Count - 1].GetComponent<TriggerableObject>();
            if (triggerable) {
                foreach (TriggerableObject.SpellEffects effect in triggerable.spellEffects) {
                    if (elementList.ContainsKey(effect.element) && effect.spawnEffect) {
                        Instantiate(effect.spawnEffect, triggerable.transform);
                    }
                }
            }
        }

        foreach (ParticleSystem p in gameObject.GetComponentsInChildren<ParticleSystem>()) {
            if (p.main.startDelay.constant == 0.0f) {
                // wait until next frame because the transform may change
                var m = p.main;
                var d = p.main.startDelay;
                d.constant = 0.01f;
                m.startDelay = d;
            }
            p.Play();
        }
        if (castSound && castSound.enabled) {
            castSound.loop = false;
            castSound.Play();
        }
        if (loopSound && loopSound.enabled) {
            loopVolume = loopSound.volume;
            loopSound.volume = 0;
            loopSound.loop = true;
            loopSound.Play();
        }
    }

    protected virtual void Update() {
        // reduce the duration
        Duration -= Time.deltaTime;
        if (Stopping) {
            // increase the stop time
            stopTimeIncrement += Time.deltaTime;
            if (stopTimeIncrement < StopTime) {
                StopPercent = stopTimeIncrement * stopTimeMultiplier;
            }
        }
        else if (Starting) {
            // increase the start time
            startTimeIncrement += Time.deltaTime;
            if (startTimeIncrement < StartTime) {
                StartPercent = startTimeIncrement * startTimeMultiplier;
            }
            else {
                Starting = false;
            }
            if (loopSound) {
                loopSound.volume = loopVolume * StartPercent;
            }
        }
        else if (Duration <= 0.0f) {
            // time to stop, no duration left
            Stop();
        }
    }
}
