using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class PlayerMana : MonoBehaviour
{
    [Header("Mana")]
    [Min(0f), SerializeField] private float maxMana = 100f;
    [Min(0f), SerializeField] private float startingMana = 100f;
    [Min(0f), SerializeField] private float regenerationPerSecond = 10f;

    [Header("UI")]
    [Tooltip("Optional. If empty, a child Slider named 'Mana Slider' is found automatically.")]
    [SerializeField] private Slider manaSlider;
    [SerializeField] private string manaSliderObjectName = "Mana Slider";

    private readonly HashSet<Component> manaUsers = new();
    private float currentMana;

    public event Action<float, float> ManaChanged;

    public float CurrentMana => currentMana;
    public float MaxMana => maxMana;
    public float RegenerationPerSecond => regenerationPerSecond;
    public bool HasManaUsers
    {
        get
        {
            RemoveDestroyedUsers();
            return manaUsers.Count > 0;
        }
    }

    private void Awake()
    {
        maxMana = Mathf.Max(0f, maxMana);
        startingMana = Mathf.Clamp(startingMana, 0f, maxMana);
        currentMana = startingMana;
        ResolveSlider();
        SyncSlider();
        RefreshSliderVisibility();
    }

    private void Update()
    {
        if (regenerationPerSecond <= 0f || currentMana >= maxMana)
            return;

        SetCurrentMana(currentMana + regenerationPerSecond * Time.deltaTime);
    }

    public bool TrySpend(float amount)
    {
        amount = Mathf.Max(0f, amount);
        if (amount <= 0f)
            return true;
        if (currentMana + 0.0001f < amount)
            return false;

        SetCurrentMana(currentMana - amount);
        return true;
    }

    public void Restore(float amount)
    {
        if (amount > 0f)
            SetCurrentMana(currentMana + amount);
    }

    public void Refill()
    {
        SetCurrentMana(maxMana);
    }

    public void RegisterUser(Component user)
    {
        if (user == null || !manaUsers.Add(user))
            return;

        RefreshSliderVisibility();
    }

    public void UnregisterUser(Component user)
    {
        if (user == null || !manaUsers.Remove(user))
            return;

        RefreshSliderVisibility();
    }

    public static PlayerMana Find(Transform source)
    {
        if (source == null)
            return null;

        PlayerMana mana = source.GetComponentInParent<PlayerMana>(true);
        if (mana != null)
            return mana;

        return source.root != null ? source.root.GetComponentInChildren<PlayerMana>(true) : null;
    }

    private void SetCurrentMana(float value)
    {
        float clamped = Mathf.Clamp(value, 0f, maxMana);
        if (Mathf.Approximately(currentMana, clamped))
            return;

        currentMana = clamped;
        SyncSlider();
        ManaChanged?.Invoke(currentMana, maxMana);
    }

    private void ResolveSlider()
    {
        if (manaSlider != null)
            return;

        Slider[] sliders = transform.root.GetComponentsInChildren<Slider>(true);
        for (int i = 0; i < sliders.Length; i++)
        {
            if (sliders[i] != null && sliders[i].name == manaSliderObjectName)
            {
                manaSlider = sliders[i];
                return;
            }
        }
    }

    private void SyncSlider()
    {
        ResolveSlider();
        if (manaSlider == null)
            return;

        manaSlider.minValue = 0f;
        manaSlider.maxValue = maxMana;
        manaSlider.value = currentMana;
    }

    private void RefreshSliderVisibility()
    {
        ResolveSlider();
        if (manaSlider != null)
            manaSlider.gameObject.SetActive(HasManaUsers);
    }

    private void RemoveDestroyedUsers()
    {
        manaUsers.RemoveWhere(user => user == null);
    }

    private void OnValidate()
    {
        maxMana = Mathf.Max(0f, maxMana);
        startingMana = Mathf.Clamp(startingMana, 0f, maxMana);
        regenerationPerSecond = Mathf.Max(0f, regenerationPerSecond);

        if (!Application.isPlaying)
            return;

        currentMana = Mathf.Clamp(currentMana, 0f, maxMana);
        SyncSlider();
        RefreshSliderVisibility();
    }
}
