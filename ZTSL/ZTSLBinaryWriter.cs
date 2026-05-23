using System;
using System.Collections.Generic;
using System.Text;
using ZTSL.Descriptors;

namespace ZTSL
{
    public class ZTSLBinaryWriter
    {
        private readonly BinaryWriter bw;

        public ZTSLBinaryWriter(Stream output)
        {
            bw = new BinaryWriter(output, Encoding.UTF8, leaveOpen: true);
        }

        public void WritePacket(Packet p)
        {
            bw.Write(p.StreamId);
            bw.Write(p.Timestamp);
            bw.Write((ushort)p.Payload.Length);
            bw.Write(p.Payload.AsSpan());
        }

        public void WriteDescriptor(StreamDescriptor d)
        {
            var (streamType, typeSpecificSize) = d switch
            {
                AudioStreamDescriptor => (ZTSLFormat.StreamTypeAudio, 8),
                LyricStreamDescriptor => (ZTSLFormat.StreamTypeLyric, 3),
                _ => throw new ArgumentException("Unknown StreamDescriptor type")
            };

            byte[] extrasBytes = Encoding.UTF8.GetBytes(d.ExtrasJson);

            var descriptorLength = 1 + 1 + typeSpecificSize + extrasBytes.Length;

            if (descriptorLength > 255)
            {
                throw new ArgumentException("Descriptor length exceeds maximum allowed size. Strip JSON extras if possible.");
            }

            bw.Write((byte)descriptorLength);
            bw.Write(d.StreamId);
            bw.Write(streamType);

            // Write type-specific fields

            if (d is AudioStreamDescriptor audio)
            {

                //Audio bloc
                bw.Write(ZTSLFormat.CodecIdOpus); // CodecId (1 byte)
                bw.Write(ZTSLFormat.SampleRate48k); // SampleRate (4 byte)
                bw.Write(audio.Channels); // Channels (1 byte)
                bw.Write(audio.Preskip); // Preskip (2 bytes)
            }
            else if (d is LyricStreamDescriptor lyric)
            {
                //Lyric bloc
                var langBytes = Encoding.ASCII.GetBytes(lyric.Language);
                if (langBytes.Length != 3)
                {
                    throw new InvalidOperationException($"Language code must be exactly 3 ASCII characters; following ISO 639-3. Got: {lyric.Language}");
                }

                bw.Write(langBytes); // Language (3 bytes (ASCII))
            }

            bw.Write(extrasBytes); // ExtrasJson (variable length)
        }

        public void WriteHeader(FileHeader header)
        {
            if (header.StreamDescriptors.Count == 0 || header.StreamDescriptors.Count > 255)
            {
                throw new InvalidOperationException("Stream count must be between 1 and 255.");
            }

            byte[] metadataBytes = Encoding.UTF8.GetBytes(header.MetadataJson);

            var headerLength = 4 //(magic)
                + 1 //(version major)
                + 1 //(version minor)
                + 4 //(header length of field itself)
                + 1 //stream count
                + header.StreamDescriptors.Sum(DescriptorOnDiskSize) //stream descriptors
                + 4 //(metadata length field)
                + metadataBytes.Length;

            bw.Write(ZTSLFormat.Magic); // Magic (4 bytes)
            bw.Write(ZTSLFormat.VersionMajor); // Version Major (1 byte)
            bw.Write(ZTSLFormat.VersionMinor); // Version Minor (1 byte)
            bw.Write((uint)headerLength); // Header Length (4 bytes)
            bw.Write((byte)header.StreamDescriptors.Count); // Stream Count (1 byte)

            foreach (var descriptor in header.StreamDescriptors)
            {
                WriteDescriptor(descriptor);
            }

            bw.Write((uint)metadataBytes.Length); // Metadata Length (4 bytes)
            bw.Write(metadataBytes); // Metadata (variable length)
        }

        private static int DescriptorOnDiskSize(StreamDescriptor d)
        {
            int typeSpecificSize = d switch
            {
                AudioStreamDescriptor => 8,
                LyricStreamDescriptor => 3,
                _ => throw new ArgumentException("Unknown StreamDescriptor type")
            };

            int extrasLen = Encoding.UTF8.GetByteCount(d.ExtrasJson);
            return 1 + 1 + 1 + typeSpecificSize + extrasLen;
        }
    }
}
