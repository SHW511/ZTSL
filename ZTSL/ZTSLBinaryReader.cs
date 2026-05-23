using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using ZTSL.Descriptors;

namespace ZTSL
{
    public class ZTSLBinaryReader
    {
        private readonly BinaryReader br;

        public ZTSLBinaryReader(Stream input)
        {
            br = new BinaryReader(input, Encoding.UTF8, leaveOpen: true);
        }

        public Packet ReadPacket()
        {
            byte streamId = br.ReadByte();
            uint timestamp = br.ReadUInt32();
            ushort payloadLength = br.ReadUInt16();
            var payload = br.ReadBytes(payloadLength);

            var packet = new Packet(streamId, timestamp, payload.ToImmutableArray());

            return packet;
        }

        public StreamDescriptor ReadDescriptor()
        {
            byte descriptorLength = br.ReadByte();
            byte streamId = br.ReadByte();
            byte streamType = br.ReadByte();

            StreamDescriptor descriptor;

            if (streamType == ZTSLFormat.StreamTypeAudio)
            {
                //audio

                var extrasLength = descriptorLength - 1 - 1 - 8;

                var codec = br.ReadByte(); //codec id (1 byte)

                if(codec != ZTSLFormat.CodecIdOpus)
                {
                    throw new InvalidOperationException($"Unsupported codec id: {codec}");
                }

                var sampleRate = br.ReadUInt32(); //sample rate (4 bytes)

                if(sampleRate != ZTSLFormat.SampleRate48k)
                {
                    throw new InvalidOperationException($"Unsupported sample rate: {sampleRate}");
                }

                var channelCount = br.ReadByte(); //channel count (1 byte)
                var preskip = br.ReadUInt16(); //Preskip (2 bytes)

                var extrasBytes = br.ReadBytes(extrasLength);
                var extrasString = Encoding.UTF8.GetString(extrasBytes);

                descriptor = new AudioStreamDescriptor(streamId, channelCount, preskip, extrasString);

            }
            else if (streamType == ZTSLFormat.StreamTypeLyric)
            {
                //Lyric

                var extrasLength = descriptorLength - 1 - 1 - 3;

                var languageCodeBytes = br.ReadBytes(3); //Language code (3 bytes)
                string languageCode = Encoding.ASCII.GetString(languageCodeBytes);

                var extras = br.ReadBytes(extrasLength); //Extras (remaining bytes)

                descriptor = new LyricStreamDescriptor(streamId, languageCode, Encoding.UTF8.GetString(extras));
            }
            else
            {
                throw new InvalidOperationException($"Unknown stream type: {streamType}");
            }

            return descriptor;
        }

        public FileHeader ReadHeader()
        {
            var magicBytes = br.ReadBytes(4);
            string magic = Encoding.UTF8.GetString(magicBytes);
            
            if (!magicBytes.SequenceEqual(ZTSLFormat.Magic))
            {
                throw new InvalidOperationException($"Invalid magic number. Expected: {ZTSLFormat.Magic}, got: {magic}");
            }
            
            byte versionMajor = br.ReadByte();
            byte versionMinor = br.ReadByte();
            
            if (versionMajor != ZTSLFormat.VersionMajor || versionMinor != ZTSLFormat.VersionMinor)
            {
                throw new InvalidOperationException($"Unsupported version. Expected: {ZTSLFormat.VersionMajor}, {ZTSLFormat.VersionMinor}, got: {versionMajor}.{versionMinor}");
            }

            var headerLength = br.ReadUInt32(); //currently unused, reserved for future use

            var streamCount = br.ReadByte();

            List<StreamDescriptor> streamDescriptors = new List<StreamDescriptor>();
            for (var i = 0; i < streamCount; i++)
            {
                streamDescriptors.Add(ReadDescriptor());
            }

            var metadataLength = br.ReadUInt32();
            var metadataBytes = br.ReadBytes((int)metadataLength);

            var metadata = Encoding.UTF8.GetString(metadataBytes);

            return new FileHeader(streamDescriptors, metadata);
        }
    }
}
