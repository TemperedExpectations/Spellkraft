using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpellEffectLightning : AbstractSpellEffects {

    [Range(0.01f, 1.0f)]
    [Tooltip("How long each bolt should last before creating a new bolt. In ManualMode, the bolt will simply disappear after this amount of seconds.")]
    public float boltDuration = 0.05f;
    
    public Gradient fireColor;
    public Gradient coldColor;

    float startTimeIncrement;
    float stopTimeIncrement;
    float loopVolume;
    float timer;

    private LightningBolt[] bolts;

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
        yield return new WaitForSeconds(StopTime);

        Destroy(gameObject);
    }

    protected override void StartParticleSystems() {
        Gradient useColor = null;
        if (elementList != null) {
            useColor = elementList.ContainsKey(Element.Fire) ? fireColor : elementList.ContainsKey(Element.Cold) ? coldColor : null;
        }
        if (useColor != null && GetComponent<SpellLighting>()) {
            GetComponent<SpellLighting>().SetColor(useColor.Evaluate(.5f));
        }
        bolts = GetComponentsInChildren<LightningBolt>();
        foreach (LightningBolt bolt in bolts) {
            bolt.Activate(useColor);
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
        
        if (timer <= 0.0f) {
            Trigger();
        }
        timer -= Time.deltaTime;
    }
    
    public void Trigger() {
        timer = boltDuration + Mathf.Min(0.0f, timer);

        foreach (LightningBolt bolt in bolts) {
            bolt.Trigger(transform.position, transform.rotation);
        }
    }
}
