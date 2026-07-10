using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class StatusEffectSystemTests
{
    private GameObject testObject;
    private StatusEffectSystem statusEffects;

    [SetUp]
    public void SetUp()
    {
        testObject = new GameObject("StatusEffectSystemTests");
        statusEffects = testObject.AddComponent<StatusEffectSystem>();
    }

    [TearDown]
    public void TearDown()
    {
        if (testObject != null)
            Object.DestroyImmediate(testObject);
    }

    [Test]
    public void AddStatus_StacksToCap_RefreshesDuration_AndStartsOnce()
    {
        int starts = 0;
        int updates = 0;
        int latestStacks = 0;
        statusEffects.OnStart += _ => starts++;
        statusEffects.OnStatusUpdated += (_, stacks) =>
        {
            updates++;
            latestStacks = stacks;
        };

        statusEffects.AddStatus(StatusEffectSystem.StatusType.Bleeding, 10f, 1f);
        statusEffects.AddStatus(StatusEffectSystem.StatusType.Bleeding, 3f, 0.25f);

        Assert.That(starts, Is.EqualTo(1));
        Assert.That(updates, Is.EqualTo(2));
        Assert.That(latestStacks, Is.EqualTo(2));
        Assert.That(statusEffects.GetStackCount(StatusEffectSystem.StatusType.Bleeding), Is.EqualTo(2));
        Assert.That(statusEffects.GetRemainingTime(StatusEffectSystem.StatusType.Bleeding), Is.EqualTo(3f).Within(0.0001f));

        for (int i = 0; i < 5; i++)
            statusEffects.AddStatus(StatusEffectSystem.StatusType.Bleeding, 7f, 0.1f);

        Assert.That(statusEffects.GetStackCount(StatusEffectSystem.StatusType.Bleeding), Is.EqualTo(5));
        Assert.That(statusEffects.GetRemainingTime(StatusEffectSystem.StatusType.Bleeding), Is.EqualTo(7f).Within(0.0001f));
        Assert.That(updates, Is.EqualTo(7), "Capped applications should still publish refresh updates.");
    }

    [TestCase(StatusEffectSystem.StatusType.Stun)]
    [TestCase(StatusEffectSystem.StatusType.Frozen)]
    [TestCase(StatusEffectSystem.StatusType.Fear)]
    public void HardControlStatuses_RemainAtOneStack(StatusEffectSystem.StatusType type)
    {
        statusEffects.AddStatus(type, 5f);
        statusEffects.AddStatus(type, 5f);

        Assert.That(statusEffects.GetMaxStacks(type), Is.EqualTo(1));
        Assert.That(statusEffects.GetStackCount(type), Is.EqualTo(1));
    }

    [Test]
    public void NumericMultipliers_ScaleLinearlyAndClampAtZero()
    {
        AddStacks(StatusEffectSystem.StatusType.Speed, 2);
        Assert.That(statusEffects.MovementSpeedMultiplier, Is.EqualTo(3f).Within(0.0001f));

        statusEffects.ClearAll();
        AddStacks(StatusEffectSystem.StatusType.Slow, 2);
        Assert.That(statusEffects.MovementSpeedMultiplier, Is.Zero.Within(0.0001f));

        statusEffects.ClearAll();
        AddStacks(StatusEffectSystem.StatusType.Onslaught, 2);
        Assert.That(statusEffects.AttackSpeedMultiplier, Is.EqualTo(2f).Within(0.0001f));
        Assert.That(statusEffects.MovementSpeedMultiplier, Is.EqualTo(1.2f).Within(0.0001f));

        statusEffects.ClearAll();
        AddStacks(StatusEffectSystem.StatusType.XpBoost, 2);
        Assert.That(statusEffects.CurrentXpMultiplier, Is.EqualTo(3f).Within(0.0001f));

        statusEffects.ClearAll();
        AddStacks(StatusEffectSystem.StatusType.Cursed, 2);
        Assert.That(statusEffects.HealingReceivedMultiplier, Is.Zero.Within(0.0001f));
        Assert.That(statusEffects.StatusDurationReceivedMultiplier, Is.EqualTo(2f).Within(0.0001f));

        statusEffects.AddStatus(StatusEffectSystem.StatusType.Bleeding, 4f);
        Assert.That(statusEffects.GetRemainingTime(StatusEffectSystem.StatusType.Bleeding), Is.EqualTo(8f).Within(0.0001f));

        statusEffects.ClearAll();
        AddStacks(StatusEffectSystem.StatusType.Shock, 2);
        Assert.That(statusEffects.IncomingDamageMultiplier, Is.EqualTo(3f).Within(0.0001f));
    }

    [Test]
    public void TickEffects_MultiplyDamageAndHealingByStacks()
    {
        Object.DestroyImmediate(statusEffects);
        SimpleHealth health = testObject.AddComponent<SimpleHealth>();
        statusEffects = testObject.AddComponent<StatusEffectSystem>();
        FieldInfo healthField = typeof(StatusEffectSystem).GetField("health", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(healthField, Is.Not.Null);
        healthField.SetValue(statusEffects, health);

        health.currentHealth = 100f;
        AddStacks(StatusEffectSystem.StatusType.Ignite, 3);
        AdvanceEffects(1.1f);
        Assert.That(health.CurrentHealth, Is.EqualTo(85));

        statusEffects.ClearAll();
        health.currentHealth = 50f;
        AddStacks(StatusEffectSystem.StatusType.Regeneration, 3);
        AdvanceEffects(1.1f);
        Assert.That(health.CurrentHealth, Is.EqualTo(65));
    }

    [Test]
    public void RemoveClearAndExpiry_ClearAllStacksAndEndOncePerStatus()
    {
        int ends = 0;
        statusEffects.OnEnd += _ => ends++;

        AddStacks(StatusEffectSystem.StatusType.Poison, 3);
        statusEffects.RemoveStatus(StatusEffectSystem.StatusType.Poison);
        Assert.That(statusEffects.GetStackCount(StatusEffectSystem.StatusType.Poison), Is.Zero);
        Assert.That(ends, Is.EqualTo(1));

        statusEffects.AddStatus(StatusEffectSystem.StatusType.Bleeding, 1f, 10f);
        AdvanceEffects(1.1f);
        Assert.That(statusEffects.GetStackCount(StatusEffectSystem.StatusType.Bleeding), Is.Zero);
        Assert.That(ends, Is.EqualTo(2));

        AddStacks(StatusEffectSystem.StatusType.Speed, 2);
        AddStacks(StatusEffectSystem.StatusType.Onslaught, 2);
        statusEffects.ClearAll();
        Assert.That(statusEffects.GetStackCount(StatusEffectSystem.StatusType.Speed), Is.Zero);
        Assert.That(statusEffects.GetStackCount(StatusEffectSystem.StatusType.Onslaught), Is.Zero);
        Assert.That(ends, Is.EqualTo(4));
    }

    [Test]
    public void StackLabel_HidesOneAndShowsHigherCounts()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/Status Effect Icon.prefab");
        GameObject instance = Object.Instantiate(prefab);
        Transform labelTransform = instance.transform.Find("Stack Count");
        Component label = labelTransform != null ? labelTransform.GetComponent("TextMeshProUGUI") : null;

        try
        {
            Assert.That(label, Is.Not.Null);
            MethodInfo updateStackText = typeof(StatusEffectsUI).GetMethod(
                "UpdateStackText",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(updateStackText, Is.Not.Null);

            updateStackText.Invoke(null, new object[] { label, 1 });
            Assert.That(label.gameObject.activeSelf, Is.False);

            updateStackText.Invoke(null, new object[] { label, 3 });
            Assert.That(label.gameObject.activeSelf, Is.True);
            PropertyInfo textProperty = label.GetType().GetProperty("text");
            Assert.That(textProperty, Is.Not.Null);
            Assert.That(textProperty.GetValue(label), Is.EqualTo("x3"));
        }
        finally
        {
            Object.DestroyImmediate(instance);
        }
    }

    [Test]
    public void StatusIconPrefab_ContainsInactiveStackLabel()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/Status Effect Icon.prefab");
        Assert.That(prefab, Is.Not.Null);

        Transform labelTransform = prefab.transform.Find("Stack Count");
        Assert.That(labelTransform, Is.Not.Null);
        Assert.That(labelTransform.GetComponent("TextMeshProUGUI"), Is.Not.Null);
        Assert.That(labelTransform.gameObject.activeSelf, Is.False);
    }

    private void AddStacks(StatusEffectSystem.StatusType type, int count)
    {
        for (int i = 0; i < count; i++)
            statusEffects.AddStatus(type, 10f, 1f);
    }

    private void AdvanceEffects(float deltaTime)
    {
        MethodInfo advanceEffects = typeof(StatusEffectSystem).GetMethod(
            "AdvanceEffects",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(advanceEffects, Is.Not.Null);
        advanceEffects.Invoke(statusEffects, new object[] { deltaTime });
    }
}
