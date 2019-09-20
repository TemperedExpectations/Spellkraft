using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public enum Element {
    Water,
    Life,
    Shield,
    Cold,
    Fire,
    Earth,
    Arcane,
    Lightning,
    Steam,
    Ice,
    Poison
}

public class SpellcraftWindow : MonoBehaviour {

    public static SpellcraftWindow instance;

    public float defaultWidth = 1080;
    public float windowLeftShift;
    public float windowRightShift;
    public float windowShiftSpeed;
    public RectTransform windowLeft;
    public RectTransform windowRight;
    public bool hideSelectionFields;
    [ConditionalHide(nameof(hideSelectionFields), false)] public RectTransform selectionField1;
    [ConditionalHide(nameof(hideSelectionFields), false)] public RectTransform selectionField2;
    [ConditionalHide(nameof(hideSelectionFields), false)] public RectTransform selectionField3;
    [ConditionalHide(nameof(hideSelectionFields), false)] public RectTransform selectionField4;
    [ConditionalHide(nameof(hideSelectionFields), false)] public RectTransform selectionField5;
    [ConditionalHide(nameof(hideSelectionFields), false)] public RectTransform selectionField6;
    [ConditionalHide(nameof(hideSelectionFields), false)] public RectTransform selectionField7;
    [ConditionalHide(nameof(hideSelectionFields), false)] public RectTransform selectionField8;
    public RectTransform cursor;
    public float cursorSpeedMult = 1;
    public float wheelBorder;
    public float minSelectionRadius;
    public bool hideViewFields;
    [ConditionalHide(nameof(hideViewFields), false)] public RectTransform elementSpawnPoint;
    [ConditionalHide(nameof(hideViewFields), false)] public RectTransform elementViewPoint1;
    [ConditionalHide(nameof(hideViewFields), false)] public RectTransform elementViewPoint2;
    [ConditionalHide(nameof(hideViewFields), false)] public RectTransform elementViewPoint3;
    [ConditionalHide(nameof(hideViewFields), false)] public RectTransform elementViewPoint4;
    [ConditionalHide(nameof(hideViewFields), false)] public RectTransform elementViewPoint5;
    public bool hideSpriteFields;
    [ConditionalHide(nameof(hideSpriteFields), false)] public Sprite elementSprite1;
    [ConditionalHide(nameof(hideSpriteFields), false)] public Sprite elementSprite2;
    [ConditionalHide(nameof(hideSpriteFields), false)] public Sprite elementSprite3;
    [ConditionalHide(nameof(hideSpriteFields), false)] public Sprite elementSprite4;
    [ConditionalHide(nameof(hideSpriteFields), false)] public Sprite elementSprite5;
    [ConditionalHide(nameof(hideSpriteFields), false)] public Sprite elementSprite6;
    [ConditionalHide(nameof(hideSpriteFields), false)] public Sprite elementSprite7;
    [ConditionalHide(nameof(hideSpriteFields), false)] public Sprite elementSprite8;
    [ConditionalHide(nameof(hideSpriteFields), false)] public Sprite elementSprite9;
    [ConditionalHide(nameof(hideSpriteFields), false)] public Sprite elementSprite10;
    [ConditionalHide(nameof(hideSpriteFields), false)] public Sprite elementSprite11;
    public float elementMoveSpeed;

    public float ScreenScale { get { return widthScale; } }
    public bool IsOpen { get { return open; } }

    float widthScale;
    bool open;
    Vector3 windowLeftDefaultPosition;
    Vector3 windowRightDefaultPosition;
    int curSelected;
    Dictionary<Element, Sprite> elementSprites;
    List<ElementSpriteMover> elementQueue;
    Queue<ElementSpriteMover> elementPool;

    private void Awake() {
        if (instance) {
            Destroy(gameObject);
        }
        else {
            QualitySettings.maxQueuedFrames = 0;
            instance = this;
        }
    }

    // Start is called before the first frame update
    void Start() {
        widthScale = Screen.width / defaultWidth;

        windowLeftDefaultPosition = windowLeft.localPosition;
        windowRightDefaultPosition = windowRight.localPosition;

        windowLeft.gameObject.SetActive(true);

        selectionField1.gameObject.SetActive(false);
        selectionField2.gameObject.SetActive(false);
        selectionField3.gameObject.SetActive(false);
        selectionField4.gameObject.SetActive(false);
        selectionField5.gameObject.SetActive(false);
        selectionField6.gameObject.SetActive(false);
        selectionField7.gameObject.SetActive(false);
        selectionField8.gameObject.SetActive(false);

        elementSpawnPoint.gameObject.SetActive(false);
        elementViewPoint1.gameObject.SetActive(false);
        elementViewPoint2.gameObject.SetActive(false);
        elementViewPoint3.gameObject.SetActive(false);
        elementViewPoint4.gameObject.SetActive(false);
        elementViewPoint5.gameObject.SetActive(false);

        elementSprites = new Dictionary<Element, Sprite> {
            { Element.Water, elementSprite1 },
            { Element.Life, elementSprite2 },
            { Element.Shield, elementSprite3 },
            { Element.Cold, elementSprite4 },
            { Element.Fire, elementSprite5 },
            { Element.Earth, elementSprite6 },
            { Element.Arcane, elementSprite7 },
            { Element.Lightning, elementSprite8 },
            { Element.Steam, elementSprite9 },
            { Element.Ice, elementSprite10 },
            { Element.Poison, elementSprite11 }
        };
        elementQueue = new List<ElementSpriteMover>();
        elementPool = new Queue<ElementSpriteMover>();
    }

    // Update is called once per frame
    void Update() {
        if (open && windowRight.localPosition != windowRightDefaultPosition) {
            Vector3 lerpAmount = Vector3.Lerp(windowRight.localPosition, windowRightDefaultPosition, Time.deltaTime * windowShiftSpeed) - windowRight.localPosition;
            Vector3 distanceLeft = windowLeftDefaultPosition - windowLeft.localPosition;

            if (distanceLeft.magnitude == 0) {
                windowRight.localPosition += lerpAmount;
            }
            if (lerpAmount.magnitude > distanceLeft.magnitude) {
                windowLeft.localPosition = windowLeftDefaultPosition;
                windowRight.localPosition += lerpAmount - distanceLeft;
            }
            else {
                windowLeft.localPosition += lerpAmount;
            }
        }
        else if (!open && windowLeft.localPosition != windowLeftDefaultPosition + Vector3.left * windowLeftShift * widthScale) {
            Vector3 lerpAmount = Vector3.Lerp(windowLeft.localPosition, windowLeftDefaultPosition + Vector3.left * windowLeftShift * widthScale, Time.deltaTime * windowShiftSpeed) - windowLeft.localPosition;
            Vector3 distanceLeft = windowRight.localPosition - (windowRightDefaultPosition + Vector3.left * windowRightShift * widthScale);

            if (distanceLeft.magnitude == 0) {
                windowLeft.localPosition += lerpAmount;
            }
            if (lerpAmount.magnitude > distanceLeft.magnitude) {
                windowRight.localPosition = windowRightDefaultPosition + Vector3.left * windowRightShift * widthScale;
                windowLeft.localPosition += lerpAmount - distanceLeft;
            }
            else {
                windowRight.localPosition += lerpAmount;
            }
        }

        if (open) {
            Vector3 nextPos = cursor.localPosition + GetInput();
            nextPos = Vector3.ClampMagnitude(nextPos, wheelBorder * widthScale);

            float radius = nextPos.magnitude;
            if (radius > minSelectionRadius * widthScale) {
                SelectElement(Mathf.FloorToInt((Vector3.SignedAngle(Vector3.right, nextPos, Vector3.back) + 180) / 45f) % 8);
                
            }
            else if (curSelected != -1) {
                GetSelection(curSelected).gameObject.SetActive(false);
                curSelected = -1;
            }

            cursor.localPosition = nextPos;
        }
        
        for (int i = 0; i < elementQueue.Count; i++) {
            if (elementQueue[i].Removed) {
                elementQueue[i].gameObject.SetActive(false);
                elementPool.Enqueue(elementQueue[i]);
                elementQueue.RemoveAt(i);
                
                break;
            }
            if (elementQueue[i].MarkedForDeath) {
                if (elementQueue[i].AtTarget && !elementQueue[i].Removing) {
                    for (int j = 0; j < elementQueue.Count; j++) {
                        if (i != j && elementQueue[j].slot == elementQueue[i].slot && elementQueue[j].AtTarget) {
                            elementQueue[i].StartRemoval();
                            if (elementQueue[j].MarkedForDeath) {
                                elementQueue[j].StartRemoval();
                            }
                            else {
                                elementQueue[j].GetComponent<Image>().sprite = elementSprites[elementQueue[j].element];
                            }
                            break;
                        }
                    }
                }
            }
        }
    }

    public void SetOpenning(bool isOpenning) {
        open = isOpenning;

        cursor.localPosition = Vector3.zero;
    }

    public List<Element> GetSpell() {
        List<Element> elementList = ListPool<Element>.Get();
        foreach (ElementSpriteMover element in elementQueue) {
            if (!element.MarkedForDeath) {
                elementList.Add(element.element);
                element.StartRemoval();
            }
        }
        
        return elementList;
    }

    Vector3 GetInput() {
        Vector2 input = new Vector2 {
            x = Input.GetAxis("Mouse X"),
            y = Input.GetAxis("Mouse Y")
        };
        return input * cursorSpeedMult * widthScale;
    }

    void SelectElement(int selection) {
        if (curSelected != selection) {
            if (curSelected != -1) {
                GetSelection(curSelected).gameObject.SetActive(false);
            }
            curSelected = selection;
            GetSelection(curSelected).gameObject.SetActive(true);
            
            ProcessElement(NewSprite(GetElement(selection)));
        }
    }

    ElementSpriteMover NewSprite(Element element) {
        ElementSpriteMover mover;

        if (elementPool.Count == 0) {
            RectTransform newElementSprite = Instantiate(elementSpawnPoint, windowRight);
            mover = newElementSprite.gameObject.AddComponent<ElementSpriteMover>();
        }
        else {
            mover = elementPool.Dequeue();
            mover.transform.SetAsLastSibling();
            mover.transform.localPosition = elementSpawnPoint.localPosition;
        }
        
        mover.name = $"Element {element}";
        mover.element = element;
        mover.GetComponent<Image>().sprite = elementSprites[element];

        return mover;
    }

    void ProcessElement(ElementSpriteMover elementSpriteMover) {
        int slot = GetAvailableSlot();

        int interaction = CheckCancel(elementSpriteMover.element);

        if (interaction >= 0) {
            slot = elementQueue[interaction].slot;

            elementSpriteMover.Initialize(GetMovePosition(slot), elementMoveSpeed, slot, true);
            elementQueue[interaction].MarkForDeath();
            ShiftMovers(slot);
        }
        else {
            interaction = CheckCancelToWater(elementSpriteMover.element);

            if (interaction >= 0) {
                slot = elementQueue[interaction].slot;

                elementSpriteMover.Initialize(GetMovePosition(slot), elementMoveSpeed, slot, true);
                elementQueue[interaction].element = Element.Water;
                elementQueue[interaction].name = $"Element {Element.Water}";
            }
            else {
                interaction = CheckCombines(elementSpriteMover.element);

                if (interaction >= 0) {
                    slot = elementQueue[interaction].slot;

                    elementSpriteMover.Initialize(GetMovePosition(slot), elementMoveSpeed, slot, true);
                    elementQueue[interaction].element = CombinesTo(elementSpriteMover.element, elementQueue[interaction].element);
                    elementQueue[interaction].name = $"Element {elementQueue[interaction].element}";
                }
                else {
                    elementSpriteMover.Initialize(GetMovePosition(slot), elementMoveSpeed, slot);
                }
            }
        }

        if (slot > 4) {
            elementSpriteMover.StartRemoval();
        }

        elementQueue.Add(elementSpriteMover);
    }

    void ShiftMovers(int slot) {
        for (int i = 0; i < elementQueue.Count; i++) {
            if (elementQueue[i].slot > slot) elementQueue[i].SetTarget(GetMovePosition(elementQueue[i].slot - 1), elementQueue[i].slot - 1);
        }
    }

    int GetAvailableSlot() {
        int used = 0;
        for (int i = 0; i < elementQueue.Count; i++) {
            if (!elementQueue[i].MarkedForDeath) ++used;
        }
        return used;
    }

    int CheckCancel(Element element) {
        int interacts = -1;
        for (int i = 0; i < elementQueue.Count; i++) {
            if (interacts != -1 && elementQueue[i].slot == elementQueue[interacts].slot) return -1;

            if (interacts == -1 && !elementQueue[i].MarkedForDeath && Cancels(element, elementQueue[i].element)) interacts = i;
        }
        return interacts;
    }

    bool Cancels(Element element1, Element element2) {
        switch (element1) {
            case Element.Water: return element2 == Element.Lightning;
            case Element.Life: return element2 == Element.Arcane;
            case Element.Shield: return element2 == Element.Shield;
            case Element.Cold: return element2 == Element.Fire;
            case Element.Fire: return element2 == Element.Cold;
            case Element.Earth: return element2 == Element.Lightning;
            case Element.Arcane: return element2 == Element.Life;
            case Element.Lightning: return element2 == Element.Earth || element2 == Element.Water;
        }
        return false;
    }

    int CheckCancelToWater(Element element) {
        for (int i = 0; i < elementQueue.Count; i++) {
            if (CancelsToWater(element, elementQueue[i].element)) return i;
        }
        return -1;
    }

    bool CancelsToWater(Element element1, Element element2) {
        switch (element1) {
            case Element.Steam: return element2 == Element.Cold;
            case Element.Ice: return element2 == Element.Fire;
            case Element.Poison: return element2 == Element.Life;
            case Element.Cold: return element2 == Element.Steam;
            case Element.Fire: return element2 == Element.Ice;
            case Element.Life: return element2 == Element.Poison;
        }
        return false;
    }

    int CheckCombines(Element element) {
        int interacts = -1;
        for (int i = 0; i < elementQueue.Count; i++) {
            if (interacts != -1 && elementQueue[i].slot == elementQueue[interacts].slot) return -1;

            if (interacts == -1 && !elementQueue[i].MarkedForDeath && Combines(element, elementQueue[i].element)) interacts = i;
        }
        return interacts;
    }

    bool Combines(Element element1, Element element2) {
        switch (element1) {
            case Element.Water: return element2 == Element.Fire || element2 == Element.Cold || element2 == Element.Arcane;
            case Element.Cold: return element2 == Element.Water;
            case Element.Fire: return element2 == Element.Water;
            case Element.Arcane: return element2 == Element.Water;
        }
        return false;
    }

    Element CombinesTo(Element element1, Element element2) {
        if (element1 == Element.Fire && element2 == Element.Water || element1 == Element.Water && element2 == Element.Fire) return Element.Steam;
        if (element1 == Element.Cold && element2 == Element.Water || element1 == Element.Water && element2 == Element.Cold) return Element.Ice;
        if (element1 == Element.Arcane && element2 == Element.Water || element1 == Element.Water && element2 == Element.Arcane) return Element.Poison;
        return Element.Water;
    }

    Vector3 GetMovePosition(int selected) {
        switch (selected) {
            case 0: return elementViewPoint1.localPosition;
            case 1: return elementViewPoint2.localPosition;
            case 2: return elementViewPoint3.localPosition;
            case 3: return elementViewPoint4.localPosition;
            case 4: return elementViewPoint5.localPosition;
            default: return elementSpawnPoint.localPosition;
        }
    }

    Element GetElement(int selected) {
        switch (selected) {
            case 0: return Element.Water;
            case 1: return Element.Life;
            case 2: return Element.Shield;
            case 3: return Element.Cold;
            case 4: return Element.Fire;
            case 5: return Element.Earth;
            case 6: return Element.Arcane;
            case 7: return Element.Lightning;
            case 8: return Element.Steam;
            case 9: return Element.Ice;
            default: return Element.Poison;
        }
    }

    RectTransform GetSelection(int selected) {
        switch (selected) {
            case 0: return selectionField1;
            case 1: return selectionField2;
            case 2: return selectionField3;
            case 3: return selectionField4;
            case 4: return selectionField5;
            case 5: return selectionField6;
            case 6: return selectionField7;
            case 7: return selectionField8;
            default: return null;
        }
    }
}
