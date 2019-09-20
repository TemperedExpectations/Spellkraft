using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpellEffectsSpray : AbstractSpellEffects {

    float startTimeIncrement;
    float stopTimeIncrement;
    float loopVolume;

    public override void AddElement(int strength, Element element) {
        if (elementList == null) elementList = new Dictionary<Element, int>();

        if (elementList.ContainsKey(element)) elementList[element] += strength;
        else elementList.Add(element, strength);
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
        if (loopSound) {
            if (loopSound.isPlaying) loopSound.Stop();
            loopSound.enabled = false;
        }

        StartCoroutine(CleanupEverythingCoRoutine());
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

    IEnumerator CleanupEverythingCoRoutine() {
        // 2 extra seconds just to make sure animation and graphics have finished ending
        yield return new WaitForSeconds(StopTime + 2.0f);

        Destroy(gameObject);
    }

    protected override void StartParticleSystems() {
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
