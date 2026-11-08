using System;
using BepInEx.Configuration;
using UnityEngine;

namespace PlanetbaseTrainer.Core
{
    /// <summary>A persisted, user-tweakable parameter rendered by the UI (slider, checkbox, selector...).</summary>
    internal abstract class Option
    {
        public readonly string LabelKey;

        protected Option(string labelKey)
        {
            LabelKey = labelKey;
        }

        public string Label => Loc.T(LabelKey);

        /// <summary>Raised after the value changed (already persisted).</summary>
        public event Action Changed;

        protected void RaiseChanged()
        {
            Settings.MarkDirty();
            try
            {
                Changed?.Invoke();
            }
            catch (Exception ex)
            {
                Log.ErrorOnce("option." + LabelKey, ex);
            }
        }
    }

    internal sealed class FloatOption : Option
    {
        private readonly ConfigEntry<float> _entry;
        private readonly Func<float, string> _format;
        public readonly float Min;
        public readonly float Max;
        public readonly float Step;

        public FloatOption(ConfigFile cfg, string section, string key, string labelKey, float defaultValue,
            float min, float max, float step, Func<float, string> format, string description = null)
            : base(labelKey)
        {
            Min = min;
            Max = max;
            Step = step;
            _format = format ?? (v => v.ToString("0.##"));
            _entry = cfg.Bind(section, key, defaultValue,
                new ConfigDescription(description ?? key, new AcceptableValueRange<float>(min, max)));
            Value = Mathf.Clamp(_entry.Value, min, max);
        }

        public float Value { get; private set; }

        public string Display => _format(Value);

        public void Set(float value)
        {
            if (Step > 0f)
                value = Min + Mathf.Round((value - Min) / Step) * Step;
            value = Mathf.Clamp(value, Min, Max);
            if (Mathf.Abs(value - Value) < 0.0001f)
                return;
            Value = value;
            _entry.Value = value;
            RaiseChanged();
        }
    }

    internal sealed class BoolOption : Option
    {
        private readonly ConfigEntry<bool> _entry;

        public BoolOption(ConfigFile cfg, string section, string key, string labelKey, bool defaultValue, string description = null)
            : base(labelKey)
        {
            _entry = cfg.Bind(section, key, defaultValue, description ?? key);
            Value = _entry.Value;
        }

        public bool Value { get; private set; }

        public void Set(bool value)
        {
            if (value == Value)
                return;
            Value = value;
            _entry.Value = value;
            RaiseChanged();
        }
    }

    /// <summary>Selector over a fixed list of entries (rendered as ‹ value ›).</summary>
    internal sealed class ChoiceOption : Option
    {
        private readonly ConfigEntry<int> _entry;
        private readonly Func<int, string> _label;
        public readonly int Count;

        public ChoiceOption(ConfigFile cfg, string section, string key, string labelKey, int defaultIndex, int count,
            Func<int, string> label, string description = null)
            : base(labelKey)
        {
            Count = count;
            _label = label;
            _entry = cfg.Bind(section, key, defaultIndex,
                new ConfigDescription(description ?? key, new AcceptableValueRange<int>(0, count - 1)));
            Value = Mathf.Clamp(_entry.Value, 0, count - 1);
        }

        public int Value { get; private set; }

        public string Display => _label(Value);

        public void Cycle(int direction) => Set((Value + direction + Count) % Count);

        public void Set(int index)
        {
            index = Mathf.Clamp(index, 0, Count - 1);
            if (index == Value)
                return;
            Value = index;
            _entry.Value = index;
            RaiseChanged();
        }
    }

    /// <summary>Selector over an enum (persisted by name, so the config file stays readable).</summary>
    internal sealed class EnumOption<T> : Option where T : struct, Enum
    {
        private static readonly T[] Values = (T[])Enum.GetValues(typeof(T));
        private readonly ConfigEntry<T> _entry;
        private readonly Func<T, string> _label;

        public EnumOption(ConfigFile cfg, string section, string key, string labelKey, T defaultValue,
            Func<T, string> label, string description = null)
            : base(labelKey)
        {
            _label = label;
            _entry = cfg.Bind(section, key, defaultValue, description ?? key);
            Value = _entry.Value;
        }

        public T Value { get; private set; }

        public string Display => _label(Value);

        public void Cycle(int direction)
        {
            int index = Array.IndexOf(Values, Value);
            Set(Values[(index + direction + Values.Length) % Values.Length]);
        }

        public void Set(T value)
        {
            if (value.Equals(Value))
                return;
            Value = value;
            _entry.Value = value;
            RaiseChanged();
        }
    }

    /// <summary>A labelled set of checkboxes rendered as toggleable chips.</summary>
    internal sealed class BoolGroupOption : Option
    {
        public readonly BoolOption[] Items;

        public BoolGroupOption(string labelKey, BoolOption[] items) : base(labelKey)
        {
            Items = items;
        }
    }
}
