using System;
using System.Collections.Generic;
using System.Text;

namespace ZTSL.Descriptors
{
    public sealed record LyricStreamDescriptor(byte StreamId, string Language = "eng", string ExtrasJson = "") : StreamDescriptor(StreamId, ExtrasJson)
    {
    }
}
