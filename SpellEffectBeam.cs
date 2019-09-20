using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpellEffectBeam : AbstractSpellEffects {
    
    [Serializable]
    public struct LineEquation {
        public bool use;
        public LineRenderer line;
        public float lineStepDist;
        public float lineDistMax;
        public float lineScale;
        public float lineHeightDeviation;
        public float lineHeightMean;
        public float lineRadiusDiminishingScale;
        public float lineRadiusPeriod;
        public float lineRadiusAmplitude;
        public float lineRadiusSpeed;
        public float lineRotationScale;
        public float lineRotationSpeed;
    }

    public float durationMult;
    public float radius = .25f;
    public float height = 10f;
    public float stepDst = 0;
    public float distance = 100f;
    public float boltFrequency = .1f;
    public float boltStepDst = 2f;
    public ParticleSystem particlesBack;
    public LineEquation[] lineEquations;
    public ObjectPool pool;
    public float emitRate;
    public float emissionSpeed;

    float div1;

    LineRenderer beam;
    float startTimeIncrement;
    float stopTimeIncrement;
    float loopVolume;
    float maxDist;
    float maxRadius;
    List<Transform> emissions;
    LightningBolt[] bolts;
    float emitTimer;
    float boltTimer;

    public override void AddElement(int strength, Element element) {
        if (elementList == null) elementList = new Dictionary<Element, int>();

        if (elementList.ContainsKey(element)) elementList[element] += strength;
        else elementList.Add(element, strength);

        if (element == Element.Arcane || element == Element.Life) {
            Duration += Duration * durationMult * (strength - 1);
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
        if (loopSound) {
            if (loopSound.isPlaying) loopSound.Stop();
            loopSound.enabled = false;
        }

        StartCoroutine(CleanupEverythingCoRoutine());
    }

    public override void RemoveOneShots() {
        GetComponent<LineRenderer>().enabled = false;
        foreach (LineRenderer line in GetComponentsInChildren<LineRenderer>()) {
            line.enabled = false;
        }

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
        yield return new WaitForSeconds(StopTime + .25f);

        Destroy(gameObject);
    }

    protected override void StartParticleSystems() {
        beam = GetComponent<LineRenderer>();
        emissions = new List<Transform>();

        maxDist = 0;
        maxRadius = radius;

        bolts = GetComponentsInChildren<LightningBolt>();
        foreach (LightningBolt bolt in bolts) {
            bolt.Activate(null);
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

        UpdateEmissions();
        UpdateBolts();
    }

    protected virtual void FixedUpdate() {
        if (Stopping) {
            maxRadius = (1 - StopPercent) * radius;
        }
        CheckBeamHit();
        UpdateLine();
    }

    void UpdateBolts() {
        if (bolts != null && bolts.Length > 0) {
            boltTimer -= Time.deltaTime;

            if (boltTimer <= 0) {
                foreach (LightningBolt bolt in bolts) {
                    bolt.Trigger(Vector3.zero, Quaternion.identity);
                }
                boltTimer = boltFrequency;
            }
        }
    }

    void UpdateEmissions() {
        if (pool) {
            foreach (Transform e in emissions) {
                e.localPosition += Vector3.forward * emissionSpeed * Time.fixedDeltaTime;
            }

            if (emissions.Count > 0 && emissions[0].transform.localPosition.z > maxDist) {
                pool.Add(emissions[0].gameObject);
                emissions.RemoveAt(0);
            }

            emitTimer -= Time.fixedDeltaTime;
            if (emitTimer <= 0) {
                emissions.Add(pool.Get(transform).transform);
                emitTimer = emitRate;
            }
        }
    }

    void UpdateLine() {
        foreach (LineEquation lineEquation in lineEquations) {
            if (!lineEquation.use || lineEquation.line == null || lineEquation.lineStepDist <= 0) continue;

            int pointCount = lineEquation.line.positionCount = Mathf.CeilToInt(Mathf.Min(maxDist, lineEquation.lineDistMax) / lineEquation.lineStepDist) + 1;

            float step = 0;
            int index = 0;
            float div = lineEquation.lineRadiusDiminishingScale / (lineEquation.lineHeightDeviation * Mathf.Sqrt(2 * Mathf.PI));
            float expDiv = -1 / (2 * lineEquation.lineHeightDeviation * lineEquation.lineHeightDeviation);
            while (step < maxDist && step < lineEquation.lineDistMax) {
                float radius = step * lineEquation.lineScale;
                radius = Mathf.Pow(2.7182818f, (radius - lineEquation.lineHeightMean) * (radius - lineEquation.lineHeightMean) * expDiv) * div;

                radius += Mathf.Sin(step * lineEquation.lineRadiusPeriod + Duration * lineEquation.lineRadiusSpeed) * lineEquation.lineRadiusAmplitude;

                float angle = step * lineEquation.lineRotationScale + Duration * lineEquation.lineRotationSpeed;
                lineEquation.line.SetPosition(index, Vector3.forward * step + Quaternion.AngleAxis(angle, Vector3.forward) * Vector3.up * radius);

                step += lineEquation.lineStepDist;
                ++index;
            }
            lineEquation.line.SetPosition(pointCount - 1, Vector3.forward * Mathf.Min(maxDist, lineEquation.lineDistMax));
        }
    }

    void CheckBeamHit() {
        if (maxDist < distance) maxDist = Mathf.Clamp(maxDist + height, 0, distance);

        float dis = maxDist;
        RaycastHit hit;
        if (Physics.SphereCast(transform.position, radius, transform.forward, out hit, dis, ~0, QueryTriggerInteraction.Ignore)) {
            Vector3 hitPoint = Vector3.Project(hit.point - transform.position, transform.forward);
            dis = maxDist = hitPoint.magnitude;
        }
        if (particlesBack) particlesBack.transform.localPosition = Vector3.forward * dis;
        
        if (beam) beam.SetPosition(1, Vector3.forward * dis);
        if (bolts != null && bolts.Length > 0) {
            foreach (LightningBolt bolt in bolts) {
                bolt.boltSegments = Mathf.Min(8, Mathf.Max(2, Mathf.FloorToInt(dis / boltStepDst)));
                if (bolt.boltSegments < 3) {
                    bolt.chaosDistanceBased = true;
                }
                else {
                    bolt.chaosDistanceBased = false;
                }
                bolt.endOffset = new Vector3(bolt.endOffset.x, bolt.endOffset.y, dis);
            }
        }
    }
}
