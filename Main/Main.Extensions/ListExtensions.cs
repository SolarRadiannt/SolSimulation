namespace  SolSimulation.Main.Extensions;

using System;
using System.Collections.Generic;

public static class ListExtensions
{
    /// <summary>
    /// Removes the element at <paramref name="index"/> in O(1) by moving the last
    /// element into its slot. The order of remaining elements is NOT preserved.
    /// </summary>
    /// <exception cref="ArgumentNullException">list is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">index is out of range.</exception>
    public static void SwapRemoveAt<T>(this List<T> list, int index)
    {
        ArgumentNullException.ThrowIfNull(list);                    // .NET 6+
        if ((uint)index >= (uint)list.Count)                        // unsigned trick: one compare
            throw new ArgumentOutOfRangeException(nameof(index));

        int last = list.Count - 1;
        if (index != last)
            list[index] = list[last];   // move last into the hole

        list.RemoveAt(last);            // O(1): only shrinks & clears the slot
    }

    /// <summary>Non-throwing variant. Returns false if index is out of range.</summary>
    public static bool TrySwapRemoveAt<T>(this List<T> list, int index)
    {
        ArgumentNullException.ThrowIfNull(list);
        if ((uint)index >= (uint)list.Count) return false;

        int last = list.Count - 1;
        if (index != last)
            list[index] = list[last];

        list.RemoveAt(last);
        return true;
    }

    /// <summary>Removes the first occurrence of <paramref name="item"/> via swap-remove.</summary>
    public static bool SwapRemove<T>(this List<T> list, T item)
    {
        ArgumentNullException.ThrowIfNull(list);
        int index = list.IndexOf(item);
        if (index < 0) return false;

        list.SwapRemoveAt(index);
        return true;
    }

    /// <summary>Removes all items matching <paramref name="match"/> in O(n) total.</summary>
    public static int SwapRemoveAll<T>(this List<T> list, Predicate<T> match)
    {
        ArgumentNullException.ThrowIfNull(list);
        ArgumentNullException.ThrowIfNull(match);

        int removed = 0;
        for (int i = list.Count - 1; i >= 0; i--)
        {
            if (match(list[i]))
            {
                list.SwapRemoveAt(i);
                removed++;
            }
        }
        return removed;
    }
}