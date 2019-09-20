using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class AbstractSpellEffects : MonoBehaviour {
    [Tooltip("Optional audio source to play once when the script starts.")]
    public AudioSource castSound;
    public AudioSource loopSound;

    [Tooltip("How long the script takes to fully start. This is used to fade in animations and sounds, etc.")]
    public float StartTime = 1.0f;

    [Tooltip("How long the script takes to fully stop. This is used to fade out animations and sounds, etc.")]
    public float StopTime = 1.0f;

    [Tooltip("How long the effect lasts. Once the duration ends, the script lives for StopTime and then the object is destroyed.")]
    public float Duration = 2.0f;

    public Dictionary<Element, int> elementList;

    public bool Starting {
        get;
        protected set;
    }

    public float StartPercent {
        get;
        protected set;
    }

    public bool Stopping {
        get;
        protected set;
    }

    public float StopPercent {
        get;
        protected set;
    }

    protected float stopTimeMultiplier;
    protected float startTimeMultiplier;

    public abstract void AddElement(int strength, Element element);
    public abstract void Stop();
    public abstract void RemoveOneShots();
    protected abstract void StartParticleSystems();
    
    protected virtual void Awake() {
        Starting = true;
        int effectLayer = LayerMask.NameToLayer("SpellLayer");
        Physics.IgnoreLayerCollision(effectLayer, effectLayer);
    }

    protected virtual void Start() {

        // precalculate so we can multiply instead of divide every frame
        stopTimeMultiplier = 1.0f / StopTime;
        startTimeMultiplier = 1.0f / StartTime;

        // start any particle system that is not in the list of manual start particle systems
        StartParticleSystems();
    }
}
