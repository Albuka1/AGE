namespace Age.Core;

/// <summary>
/// Sorts lists by an integer key while preserving the relative order of equal keys.
/// </summary>
public sealed class SpriteSorter
{
    /// <summary>
    /// Performs a stable in-place sort. Allocates two temporary buffers.
    /// </summary>
    public void Sort<T>(List<T> items, Func<T, int> zSelector)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(zSelector);

        int count = items.Count;
        if (count < 2)
        {
            return;
        }

        T[] source = new T[count];
        T[] buffer = new T[count];
        items.CopyTo(source);

        for (int width = 1; width < count; width *= 2)
        {
            for (int start = 0; start < count; start += 2 * width)
            {
                int middle = Math.Min(start + width, count);
                int end = Math.Min(start + (2 * width), count);
                Merge(source, buffer, start, middle, end, zSelector);
            }

            (source, buffer) = (buffer, source);
        }

        for (int index = 0; index < count; index++)
        {
            items[index] = source[index];
        }
    }

    private static void Merge<T>(T[] source, T[] destination, int start, int middle, int end, Func<T, int> zSelector)
    {
        int left = start;
        int right = middle;

        for (int index = start; index < end; index++)
        {
            if (left < middle && (right >= end || zSelector(source[left]) <= zSelector(source[right])))
            {
                destination[index] = source[left++];
            }
            else
            {
                destination[index] = source[right++];
            }
        }
    }
}
