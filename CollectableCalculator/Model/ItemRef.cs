using System;

namespace CollectableCalculator.Model
{
    internal sealed class ItemRef : IEquatable<ItemRef>
    {
        public required uint Id { get; init; }
        public required string Name { get; init; }
        public required ushort IconId { get; init; }

        public bool Equals(ItemRef? other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Id == other.Id;
        }

        public override bool Equals(object? obj)
        {
            return ReferenceEquals(this, obj) || obj is ItemRef other && Equals(other);
        }

        public override int GetHashCode()
        {
            return (int)Id;
        }

        public static bool operator ==(ItemRef? left, ItemRef? right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(ItemRef? left, ItemRef? right)
        {
            return !Equals(left, right);
        }
    }
}