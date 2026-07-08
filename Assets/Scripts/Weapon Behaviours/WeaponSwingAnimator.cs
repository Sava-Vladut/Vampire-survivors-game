using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Knife))]
public class WeaponSwingAnimator : MonoBehaviour
{
    [Header("Animation Settings")]
    [Tooltip("The total angle of the swing in degrees.")]
    public float swingAngle = 120f;
    [Tooltip("The duration of one full swing (out and back).")]
    public float swingDuration = 0.3f;
    [Tooltip("The offset of the sprite from the pivot point.")]
    public Vector3 spriteOffset = new Vector3(0.75f, 0, 0);
    [Tooltip("The scale of the weapon sprite.")]
    public float spriteScale = 1f;
    [Tooltip("The sorting order of the swinging sprite.")]
    public int sortingOrder = 1;

    [Header("Arc Targeting")]
    [Tooltip("If true, directed swings rotate the arc toward the selected target.")]
    [SerializeField] private bool aimSwingAtTarget = true;
    [Tooltip("If true, swings without a target still mirror with the player's facing.")]
    [SerializeField] private bool mirrorFallbackWithPlayerFacing = true;

    private GameObject _spriteObject;
    private GameObject _spriteHolder;
    private SpriteRenderer _weaponRenderer;
    private Coroutine _swingCoroutine;
    private Knife _knife;
    private bool _swingingTowardTarget;
    [Header("Flip Settings")]
    [Tooltip("Optional: Source SpriteRenderer to mirror flipX from (e.g., player sprite). If not set, will search in parents.")]
    [SerializeField] private SpriteRenderer playerSpriteRenderer;

    void Awake()
    {
        _knife = GetComponent<Knife>();
    }

    void Start()
    {
        // Try to find a source SpriteRenderer (player) before creating our own
        if (playerSpriteRenderer == null)
        {
            playerSpriteRenderer = GetComponentInParent<SpriteRenderer>();
        }

        _spriteObject = new GameObject("WeaponSwingPivot");
        _spriteObject.transform.SetParent(transform);
        _spriteObject.transform.localPosition = Vector3.zero;

        _spriteHolder = new GameObject("SpriteHolder");
        _spriteHolder.transform.SetParent(_spriteObject.transform);
        _spriteHolder.transform.localPosition = spriteOffset;
        _spriteHolder.transform.localScale = Vector3.one * spriteScale;

        _weaponRenderer = _spriteHolder.AddComponent<SpriteRenderer>();
        if (_knife.weaponSprite != null)
        {
            _weaponRenderer.sprite = _knife.weaponSprite;
        }
        else
        {
            Debug.LogWarning("WeaponSwingAnimator: The referenced Knife script is missing a weaponSprite.", this);
        }
        _weaponRenderer.sortingOrder = sortingOrder;

        _spriteObject.SetActive(false);
    }

    void LateUpdate()
    {
        if (_spriteObject == null) return;

        if (_swingingTowardTarget)
        {
            SetPivotScaleX(1f);
        }
        // Mirror fallback swings with the player's facing (flipX).
        else if (mirrorFallbackWithPlayerFacing && playerSpriteRenderer != null)
        {
            bool flip = !playerSpriteRenderer.flipX;
            SetPivotScaleX(flip ? -1f : 1f);
        }
    }

    public void Swing()
    {
        StartSwing(Vector2.right, false);
    }

    public void SwingTowards(Vector3 targetWorldPosition, Vector3 originWorldPosition)
    {
        Vector2 direction = targetWorldPosition - originWorldPosition;
        StartSwing(direction, true);
    }

    public void SwingTowards(Vector2 worldDirection)
    {
        StartSwing(worldDirection, true);
    }

    private void StartSwing(Vector2 worldDirection, bool hasTargetDirection)
    {
        if (gameObject.activeInHierarchy)
        {
            if (_swingCoroutine != null)
            {
                StopCoroutine(_swingCoroutine);
            }

            bool useTargetDirection = aimSwingAtTarget && hasTargetDirection && worldDirection.sqrMagnitude > 0.0001f;
            _swingCoroutine = StartCoroutine(SwingCoroutine(worldDirection, useTargetDirection));
        }
    }

    private IEnumerator SwingCoroutine(Vector2 worldDirection, bool useTargetDirection)
    {
        _swingingTowardTarget = useTargetDirection;
        if (_swingingTowardTarget)
        {
            SetPivotScaleX(1f);
        }

        _spriteObject.SetActive(true);

        float halfDuration = swingDuration / 2f;
        float baseAngle = useTargetDirection ? GetLocalAimAngle(worldDirection) : 0f;

        Quaternion swingStartOffset = Quaternion.Euler(0, 0, baseAngle + swingAngle / 2f);
        Quaternion swingEndOffset = Quaternion.Euler(0, 0, baseAngle - swingAngle / 2f);

        float timer = 0f;
        while (timer < halfDuration)
        {
            _spriteObject.transform.localRotation = Quaternion.Slerp(swingStartOffset, swingEndOffset, timer / halfDuration);
            timer += Time.deltaTime;
            yield return null;
        }
        _spriteObject.transform.localRotation = swingEndOffset;

        timer = 0f;
        while (timer < halfDuration)
        {
            _spriteObject.transform.localRotation = Quaternion.Slerp(swingEndOffset, swingStartOffset, timer / halfDuration);
            timer += Time.deltaTime;
            yield return null;
        }

        _spriteObject.SetActive(false);
        _swingingTowardTarget = false;
        _swingCoroutine = null;
    }

    private float GetLocalAimAngle(Vector2 worldDirection)
    {
        Vector3 localDirection = transform.InverseTransformDirection(worldDirection.normalized);
        return Mathf.Atan2(localDirection.y, localDirection.x) * Mathf.Rad2Deg;
    }

    private void SetPivotScaleX(float x)
    {
        Vector3 ls = _spriteObject.transform.localScale;
        ls.x = x;
        _spriteObject.transform.localScale = ls;

        if (_weaponRenderer != null)
            _weaponRenderer.flipX = false;
    }
}
