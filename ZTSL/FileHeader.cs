using ZTSL.Descriptors;

namespace ZTSL
{
    public sealed record FileHeader(List<StreamDescriptor> StreamDescriptors, string MetadataJson)
    {
        public bool Equals(FileHeader? other)
        {
            return other is not null
                && MetadataJson == other.MetadataJson
                && StreamDescriptors.SequenceEqual(other.StreamDescriptors);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(MetadataJson, StreamDescriptors.Count);
        }
    }
}
