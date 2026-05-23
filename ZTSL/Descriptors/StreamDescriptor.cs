using System;
using System.Collections.Generic;
using System.Text;

namespace ZTSL.Descriptors
{
    public abstract record StreamDescriptor(byte StreamId, string ExtrasJson = "")
    {

    }
}
