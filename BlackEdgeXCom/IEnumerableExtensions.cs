using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections;

namespace BlackEdgeCommon.Utils.Extensions
{
    public static class IEnumerableExtensions
    {
        public static IEnumerable<T> AsEnumerable<T>(this T item)
        {
            yield return item;
        }

        public static bool SafeContains<T>(this HashSet<T> hashSet, T value)
            => hashSet?.Contains(value) ?? false;

        public static bool UnsortedSequencesEqual<T>(this IEnumerable<T> first, IEnumerable<T> second)
        {
            return UnsortedSequencesEqual(first, second, null);
        }

        public static IEnumerable<T> RemoveFurthestFromMean<T>(this IEnumerable<T> source,
            Func<T, double> numericalFunction, double fractionToRemove)
        {
            IEnumerable<double> numericalObservations = source.Select(x => numericalFunction.Invoke(x));
            int countOfObservations = numericalObservations.Count();
            //Take mean
            double rawMean = numericalObservations.Sum() / countOfObservations;

            //Remove furthest fraction
            int furthestObsCountToRemove = (int)(countOfObservations * fractionToRemove);

            if (furthestObsCountToRemove > 0)
            {
                IEnumerable<Tuple<double, T>> distanceFromMeanAndObservation =
                    source.Select(x => new Tuple<double, T>(Math.Abs(numericalFunction.Invoke(x) - rawMean), x))
                    .OrderByDescending(y => y.Item1);
                double distanceThreshold = distanceFromMeanAndObservation.Skip(furthestObsCountToRemove).First().Item1;
                return distanceFromMeanAndObservation
                    .Where(x => x.Item1 <= distanceThreshold).Select(x => x.Item2);
            }
            else
            {
                return source;
            }
        }

        public static IEnumerable<U> SelectArbitraryElementUniqueBy<T, U, IStructuralEquatable>(this IEnumerable<T> source, Func<T, U> selectionFunction,
            Func<T, IStructuralEquatable> sourceToTupleFunc)
        {
            return source
                .GroupBy(x => sourceToTupleFunc.Invoke(x))
                .Select(y => selectionFunction.Invoke(y.First()));
        }

        public static IEnumerable<U> SelectUniqueBy<T, U>(this IEnumerable<T> source, Func<T, U> selectionFunction,
            Func<T, IComparable> sourceToTupleFunc)
        {
            return source
                .GroupBy(x => sourceToTupleFunc.Invoke(x))
                .Select(y => selectionFunction.Invoke(y.First()));
        }

        public static bool UnsortedSequencesEqual<T>(this IEnumerable<T> first, IEnumerable<T> second, IEqualityComparer<T> comparer)
        {
            if (first == null)
                throw new ArgumentNullException("first");

            if (second == null)
                throw new ArgumentNullException("second");

            var counts = new Dictionary<T, int>(comparer);

            foreach (var i in first)
            {
                int c;
                if (counts.TryGetValue(i, out c))
                    counts[i] = c + 1;
                else
                    counts[i] = 1;
            }

            foreach (var i in second)
            {
                int c;
                if (!counts.TryGetValue(i, out c))
                    return false;

                if (c == 1)
                    counts.Remove(i);
                else
                    counts[i] = c - 1;
            }

            return counts.Count == 0;
        }

        public static bool TryGetOnly<T>(this IEnumerable<T> source, out T result)
            => TryGetOnly(source, value => true, out result);

        public static bool TryGetOnly<T>(this IEnumerable<T> source, Func<T, bool> predicate, out T result)
        {
            result = default;

            if (source == null)
                return false;

            bool valueFound = false;

            foreach (T value in source.Where(predicate))
            {
                if (valueFound)
                    return false;

                valueFound = true;
                result = value;
            }

            return valueFound;
        }

        public static T OnlyOrDefault<T>(this IEnumerable<T> source)
            => OnlyOrDefault(source, element => true);

        public static T OnlyOrDefault<T>(this IEnumerable<T> source, Func<T, bool> predicate)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            T onlyValue = default;
            bool valueFound = false;

            foreach (T value in source.Where(predicate))
            {
                if (valueFound)
                    return default;

                valueFound = true;
                onlyValue = value;
            }

            return onlyValue;
        }

        public static T OnlyOrFallback<T>(this IEnumerable<T> source, T fallbackValue)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            T onlyValue = fallbackValue;
            bool valueFound = false;

            foreach (T value in source)
            {
                if (valueFound)
                    return fallbackValue;

                valueFound = true;
                onlyValue = value;
            }

            return onlyValue;
        }
    }
}
