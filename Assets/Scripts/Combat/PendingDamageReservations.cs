using System.Collections.Generic;
using UnityEngine;
using TD.Enemies;

namespace TD.Combat
{
    /// <summary>
    /// Tracks damage that is already travelling toward a target. Towers use this
    /// to avoid launching more projectiles once the pending hits are lethal.
    /// </summary>
    internal static class PendingDamageReservations
    {
        internal readonly struct Reservation
        {
            internal readonly ReservationGroup Owner;
            internal readonly int Id;

            internal Reservation(ReservationGroup owner, int id)
            {
                Owner = owner;
                Id = id;
            }
        }

        internal sealed class ReservationGroup
        {
            internal readonly IDamageable Target;
            internal readonly List<ReservationEntry> Entries = new(4);
            internal readonly List<float> OrderedDamages = new(4);
            internal readonly List<float> TimelyDamages = new(4);
            internal bool IsActive = true;
            internal int NextId;

            internal ReservationGroup(IDamageable target)
            {
                Target = target;
            }
        }

        internal readonly struct ReservationEntry
        {
            internal readonly int Id;
            internal readonly double EstimatedImpactTime;

            internal ReservationEntry(int id, double estimatedImpactTime)
            {
                Id = id;
                EstimatedImpactTime = estimatedImpactTime;
            }
        }

        private static readonly Dictionary<IDamageable, ReservationGroup> reservations = new();

        internal static Reservation Reserve(IDamageable target, float damage, float estimatedFlightTime = 0f)
        {
            if (IsUnavailable(target) || damage <= 0f)
                return default;

            if (!reservations.TryGetValue(target, out ReservationGroup group))
            {
                group = new ReservationGroup(target);
                reservations.Add(target, group);
            }

            int id = group.NextId++;
            double estimatedImpactTime = Time.timeAsDouble + Mathf.Max(0f, estimatedFlightTime);
            int insertionIndex = group.Entries.Count;
            while (insertionIndex > 0
                && group.Entries[insertionIndex - 1].EstimatedImpactTime > estimatedImpactTime)
            {
                insertionIndex--;
            }

            group.Entries.Insert(insertionIndex, new ReservationEntry(id, estimatedImpactTime));
            group.OrderedDamages.Insert(insertionIndex, damage);
            return new Reservation(group, id);
        }

        internal static void Release(Reservation reservation)
        {
            ReservationGroup group = reservation.Owner;
            if (group == null || !group.IsActive)
                return;

            for (int index = 0; index < group.Entries.Count; index++)
            {
                if (group.Entries[index].Id != reservation.Id)
                    continue;

                group.Entries.RemoveAt(index);
                group.OrderedDamages.RemoveAt(index);
                break;
            }

            if (group.Entries.Count == 0
                && reservations.TryGetValue(group.Target, out ReservationGroup currentGroup)
                && ReferenceEquals(currentGroup, group))
            {
                group.IsActive = false;
                reservations.Remove(group.Target);
            }
        }

        internal static void ReleaseAll(IDamageable target)
        {
            if (target == null || !reservations.TryGetValue(target, out ReservationGroup group))
                return;

            group.IsActive = false;
            reservations.Remove(target);
        }

        internal static bool IsLethallyCovered(IDamageable target)
        {
            if (IsUnavailable(target))
                return true;

            if (!reservations.TryGetValue(target, out ReservationGroup group)
                || !group.IsActive
                || group.OrderedDamages.Count == 0)
                return false;

            int timelyDamageCount = GetTimelyDamageCount(target, group);
            if (timelyDamageCount == 0)
                return false;

            if (timelyDamageCount == group.OrderedDamages.Count)
                return target.WouldBeDestroyedBy(group.OrderedDamages);

            group.TimelyDamages.Clear();
            for (int index = 0; index < timelyDamageCount; index++)
                group.TimelyDamages.Add(group.OrderedDamages[index]);

            return target.WouldBeDestroyedBy(group.TimelyDamages);
        }

        private static int GetTimelyDamageCount(IDamageable target, ReservationGroup group)
        {
            if (!(target is Enemy enemy))
                return group.Entries.Count;

            float secondsToGoal = enemy.EstimatedSecondsToGoal;
            if (float.IsPositiveInfinity(secondsToGoal))
                return group.Entries.Count;

            double goalTime = Time.timeAsDouble + Mathf.Max(0f, secondsToGoal);
            int count = 0;
            while (count < group.Entries.Count
                && group.Entries[count].EstimatedImpactTime < goalTime)
            {
                count++;
            }

            return count;
        }

        internal static void Clear()
        {
            foreach (ReservationGroup group in reservations.Values)
                group.IsActive = false;

            reservations.Clear();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            Clear();
        }

        private static bool IsUnavailable(IDamageable target)
        {
            if (target == null)
                return true;

            if (target is Component component
                && (component == null || !component.gameObject.activeInHierarchy))
                return true;

            return target.IsDead;
        }
    }
}
