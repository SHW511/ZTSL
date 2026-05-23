using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;

namespace ZTSL
{
    public readonly record struct Packet(byte StreamId, uint Timestamp, ImmutableArray<byte> Payload)
    {
        public bool Equals(Packet other)
        {
            return StreamId == other.StreamId 
                && Timestamp == other.Timestamp
                && Payload.AsSpan()
                .SequenceEqual(other.Payload.AsSpan());
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(StreamId, Timestamp, Payload.Length);
        }
    }
}
