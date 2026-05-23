using System;
using System.Collections.Generic;
using System.Text;

namespace ZTSL.Descriptors
{
    public sealed record AudioStreamDescriptor(byte StreamId, byte Channels, ushort Preskip, string ExtrasJson = "") : StreamDescriptor(StreamId, ExtrasJson)
    {

    }
}
