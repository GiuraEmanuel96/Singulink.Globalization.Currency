﻿using System.Collections;
using System.Text;

namespace Singulink.Globalization;

/// <summary>
/// Represents a set of money that can contain values in multiple currencies.
/// </summary>
public class SortedMoneySet : IReadOnlyMoneySet, IFormattable
{
    private readonly CurrencyRegistry _registry;
    private readonly SortedDictionary<Currency, decimal> _amountLookup = new(CurrencyByCodeComparer.Default);

    /// <summary>
    /// Initializes a new instance of the <see cref="SortedMoneySet"/> class with the <see cref="CurrencyRegistry.Default"/> currency registry.
    /// </summary>
    public SortedMoneySet() : this(CurrencyRegistry.Default)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SortedMoneySet"/> class with the specified currency registry.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// Attempted to add a value with a currency that is not available in the currency registry.
    /// </exception>
    public SortedMoneySet(CurrencyRegistry registry)
    {
        _registry = registry;
    }

    /// <inheritdoc cref="ImmutableSortedMoneySet(Money[])"/>
    public SortedMoneySet(IEnumerable<Money> values) : this(CurrencyRegistry.Default, values)
    {
    }

    /// <inheritdoc cref="ImmutableSortedMoneySet(CurrencyRegistry?, Money[])"/>
    public SortedMoneySet(CurrencyRegistry registry, IEnumerable<Money> values) : this(registry, values, values is not IReadOnlyMoneySet s || s.Registry != registry)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SortedMoneySet"/> class. Trusted internal constructor.
    /// </summary>
    internal SortedMoneySet(CurrencyRegistry registry, IEnumerable<Money> values, bool ensureValuesInRegistry) : this(registry)
    {
        AddRangeInternal(values, ensureValuesInRegistry);
    }

    /// <inheritdoc cref="IReadOnlyMoneySet.Registry"/>
    public CurrencyRegistry Registry => _registry;

    /// <inheritdoc cref="IReadOnlyMoneySet.Count"/>
    public int Count => _amountLookup.Count;

    /// <inheritdoc cref="IReadOnlyMoneySet.Currencies"/>
    public IReadOnlyCollection<Currency> Currencies => _amountLookup.Keys;

    /// <inheritdoc cref="IReadOnlyMoneySet.this[string]"/>
    public Money this[string currencyCode]
    {
        get {
            var currency = _registry[currencyCode];

            if (_amountLookup.TryGetValue(currency, out decimal amount))
                return new Money(amount, currency);

            return default;
        }
    }

    /// <inheritdoc cref="IReadOnlyMoneySet.this[Currency]"/>
    public Money this[Currency currency]
    {
        get {
            EnsureCurrencyAllowed(currency, nameof(currency));

            if (_amountLookup.TryGetValue(currency, out decimal amount))
                return new Money(amount, currency);

            return default;
        }
    }

    /// <summary>
    /// Copies the values in this set to a new immutable set that uses the same registry as this set.
    /// </summary>
    public ImmutableSortedMoneySet ToImmutableSet() => new ImmutableSortedMoneySet(_registry, this, false);

    /// <inheritdoc cref="IReadOnlyMoneySet.TryGetAmount(Currency, out decimal)"/>
    public bool TryGetAmount(Currency currency, out decimal amount)
    {
        return _amountLookup.TryGetValue(currency, out amount);
    }

    /// <inheritdoc cref="IReadOnlyMoneySet.TryGetValue(Currency, out Money)"/>
    public bool TryGetValue(Currency currency, out Money value)
    {
        EnsureCurrencyAllowed(currency, nameof(currency));

        if (_amountLookup.TryGetValue(currency, out decimal amount))
        {
            value = new Money(amount, currency);
            return true;
        }

        value = default;
        return false;
    }

    /// <inheritdoc cref="IReadOnlyMoneySet.TryGetValue(string, out Money)"/>
    public bool TryGetValue(string currencyCode, out Money value)
    {
        var currency = _registry[currencyCode];
        return TryGetValue(currency, out value);
    }

    private void AddRangeInternal(IEnumerable<Money> values, bool ensureCurrenciesInRegistry)
    {
        foreach (var value in values)
        {
            var currency = value.CurrencyOrDefault;

            if (currency == null)
                continue;

            if (ensureCurrenciesInRegistry)
                EnsureCurrencyAllowed(currency, nameof(values));

            if (_amountLookup.TryGetValue(currency, out decimal existingAmount))
                _amountLookup[currency] = existingAmount + value.Amount;
            else
                _amountLookup.Add(currency, value.Amount);
        }
    }

    private void EnsureCurrencyAllowed(Currency currency, string paramName)
    {
        if (!_registry.Contains(currency))
            Throw(currency, paramName);

        static void Throw(Currency currency, string paramName)
        {
            throw new ArgumentException($"The currency '{currency}' is not present in the set's currency registry.", paramName);
        }
    }

    public bool TryGetAmount(string currencyCode, out decimal amount)
    {
        var currency = _registry[currencyCode];
        return _amountLookup.TryGetValue(currency, out amount);
    }

    public IEnumerator<Money> GetEnumerator()
    {
        // TODO: Implement this
        throw new NotImplementedException();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        // TODO: Implement this
        throw new NotImplementedException();
    }

    /// <summary>
    /// Returns a string representation of the money values this set contains.
    /// </summary>
    /// <param name="format">The format to use for each money value. See <see cref="Money.ToString(string?, IFormatProvider?)"/> for valid money formats.
    /// Prepend the format with the <c>"!"</c> character to ignore zero amount values.</param>
    /// <param name="formatProvider">The format provider that will be used to obtain number format information. This should be a <see cref="CultureInfo"/>
    /// instance for formats that depend on the culture, otherwise the current culture is used.</param>
    public string ToString(string? format, IFormatProvider? formatProvider = null)
    {
        bool ignoreZeroAmounts;
        int count;

        if (format != null && format.StartsWith('!'))
        {
            format = format[1..];
            ignoreZeroAmounts = true;
            count = GetNonZeroCount();
        }
        else
        {
            ignoreZeroAmounts = false;
            count = Count;
        }

        if (count == 0)
            return string.Empty;

        var sb = new StringBuilder(count * 8);
        bool first = true;

        foreach (var value in this)
        {
            if (ignoreZeroAmounts && value.Amount == 0)
                continue;

            if (first)
                first = false;
            else
                sb.Append(", ");

            sb.Append(value.ToString(format, formatProvider));
        }

        return sb.ToString();

        int GetNonZeroCount()
        {
            int count = 0;

            foreach (var amount in _amountLookup.Values)
            {
                if (amount != 0)
                    count++;
            }

            return count;
        }
    }

    #region Explicit Interface Implementations

    /// <inheritdoc cref="IReadOnlyMoneySet.Currencies"/>
    IEnumerable<Currency> IReadOnlyMoneySet.Currencies => Currencies;

    #endregion
}