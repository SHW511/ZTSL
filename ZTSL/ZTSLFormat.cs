using System;
using System.Collections.Generic;
using System.Text;

namespace ZTSL
{
    public static class ZTSLFormat
    {
        public static readonly byte[] Magic = [0x5A, 0x54, 0x53, 0x4C];
        public const byte VersionMajor = 1;
        public const byte VersionMinor = 0;
        public const byte StreamTypeAudio = 0x01;
        public const byte StreamTypeLyric = 0x02;
        public const byte CodecIdOpus = 0x01;
        public const uint SampleRate48k = 48000;
    }
}
