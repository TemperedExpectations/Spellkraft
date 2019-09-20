using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpellEffectProjectile : AbstractSpellEffects {

    [System.Serializable]
    public struct SpellEffect {
        public Element element;
        public GameObject trail;
        public GameObject explosion;
    }

    public ParticleSystem[] onStartParticles;
    public ParticleSystem[] onChargeParticles;
    public float chargeTime;
    public ObjectPool emissionPool;
    public float emissionRate;
    public float emissionAngle;
    public float emissionRadius;
    public float emissionSpeed;
    public float emissionMaxDst;
    public GameObject launchObject;
    public int launchNum;
    public bool elementCountEffectsLaunchNum;
    [ConditionalHide("elementCountEffectsLaunchNum", true)]
    public int launchNumMult;
    public float launchFrequency;
    public float launchSpeedMinMult;
    public float launchSpeedMaxMult;
    public float[] sizeMults;
    public bool chargeEffectsAccuracy;
    [ConditionalHide("chargeEffectsAccuracy", true)]
    public float minAccuracy;
    public bool reduceBounces;
    public SpellEffect[] spellEffects;

    float startTimeIncrement;
    float stopTimeIncrement;
    float loopVolume;
    float chargeTimer;
    float emissionTimer;
    List<Transform> emissions;
    int inverse;

    public override void AddElement(int strength, Element element) {
        if (elementList == null) elementList = new Dictionary<Element, int>();

        if (elementList.ContainsKey(element)) elementList[element] += strength;
        else elementList.Add(element, strength);
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
        if (loopSound && loopSound.enabled) {
            if (loopSound.isPlaying) loopSound.Stop();
            loopSound.enabled = false;
        }

        StartCoroutine(LaunchProjectile());

        StartCoroutine(CleanupEverythingCoRoutine());
    }

    IEnumerator CleanupEverythingCoRoutine() {
        // 2 extra seconds just to make sure animation and graphics have finished ending
        yield return new WaitForSeconds(StopTime + 2.0f);

        Destroy(gameObject);
    }

    IEnumerator LaunchProjectile() {
        if (launchObject && launchNum > 0) {
            if (castSound && castSound.enabled) {
                castSound.loop = false;
                castSound.Play();
            }

            int size = 0;
            if (elementList != null && elementList.ContainsKey(Element.Earth)) size += elementList[Element.Earth];
            if (elementList != null && elementList.ContainsKey(Element.Ice)) size += elementList[Element.Ice];
            int toLaunch = launchNum + (elementCountEffectsLaunchNum ? (size - 1) * launchNumMult : 0);
            float chargePercent = chargeTimer / chargeTime;
            for (int i = 0; i < toLaunch; i++) {
                Transform projectile = Instantiate(launchObject, transform.position, chargeEffectsAccuracy ? Utility.GetShotOffset(minAccuracy * (1 - chargePercent), transform) : transform.rotation).transform;
                if (projectile) {
                    AddEffects(projectile.GetComponent<ProjectileMover>(), size, chargePercent);
                }

                if (launchFrequency > 0)
                    yield return new WaitForSeconds(launchFrequency);
            }
        }
    }

    void AddEffects(ProjectileMover projectile, int size, float chargePercent) {
        if (projectile == null) return;

        if (sizeMults != null && sizeMults.Length > 0) projectile.sizeMult = sizeMults[Mathf.Clamp(size - 1, 0, sizeMults.Length - 1)];
        projectile.speed *= Mathf.Lerp(launchSpeedMinMult, launchSpeedMaxMult, chargePercent);
        if (elementList != null) {
            bool removeUpExplosions = elementList.ContainsKey(Element.Arcane) || elementList.ContainsKey(Element.Life);
            foreach (SpellEffect spellEffect in spellEffects) {
                if (elementList.ContainsKey(spellEffect.element)) {
                    if (spellEffect.trail) Instantiate(spellEffect.trail, projectile.transform);
                    if (spellEffect.explosion) {
                        GameObject explosion = Instantiate(spellEffect.explosion, projectile.transform);
                        if (removeUpExplosions) {
                            foreach (ParticleSystem p in explosion.GetComponentsInChildren<ParticleSystem>()) {
                                if (!p.name.Contains(" Up")) projectile.collisionEffects.Add(p);
                            }
                        }
                        else {
                            projectile.collisionEffects.AddRange(explosion.GetComponentsInChildren<ParticleSystem>());
                        }
                    }
                    if (reduceBounces) projectile.maxBounces = 0;
                }
            }
        }
    }

    protected override void StartParticleSystems() {
        emissions = new List<Transform>();
        inverse = 1;

        foreach (ParticleSystem p in onStartParticles) {
            if (p.main.startDelay.constant == 0.0f) {
                // wait until next frame because the transform may change
                var m = p.main;
                var d = p.main.startDelay;
                d.constant = 0.01f;
                m.startDelay = d;
            }
            p.Play();
        }
        if (loopSound) {
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

        if (!Stopping) {
            FireEmissions();
        }
    }

    void FireEmissions() {
        if (chargeTimer >= chargeTime) {
            emissionTimer -= Time.deltaTime;
            if (emissionTimer <= 0) {
                Transform newEmission = emissionPool.Get(transform).transform;

                float angle = (Random.value - .5f) * emissionAngle;
                newEmission.localRotation = Quaternion.FromToRotation(Vector3.forward, Quaternion.AngleAxis(angle, Vector3.forward) * Vector3.right * inverse);
                inverse *= -1;
                newEmission.position += newEmission.forward * emissionRadius;

                emissions.Add(newEmission);

                emissionTimer = emissionRate;
            }
        }
        else {
            chargeTimer += Time.deltaTime;
            if (chargeTimer >= chargeTime) {
                chargeTimer = chargeTime;
                foreach (ParticleSystem p in onChargeParticles) {
                    p.Play();
                }
            }
        }
    }

    protected virtual void FixedUpdate() {
        foreach (Transform emission in emissions) {
            emission.position += emission.forward * emissionSpeed * Time.fixedDeltaTime;
        }

        if (emissions.Count > 0 && emissions[0].transform.localPosition.sqrMagnitude > emissionMaxDst * emissionMaxDst) {
            emissionPool.Add(emissions[0].gameObject);
            emissions.RemoveAt(0);
        }
    }
}
