using DOL.Network;

namespace DOL.GS.PacketHandler
{
    /// <summary>
    /// An outgoing TCP packet
    /// </summary>
    public class GSTCPPacketOut : PacketOut
    {
        public GSTCPPacketOut(byte packetCode)
        {
            PacketCode = packetCode;
            WriteShort(0x00); // Reserved for size.
            base.WriteByte(packetCode);
        }

        public GSTCPPacketOut(byte packetCode, int startingSize) : base(startingSize + 3)
        {
            PacketCode = packetCode;
            WriteShort(0x00); //reserved for size
            base.WriteByte(packetCode);
        }

        public override void WritePacketLength()
        {
            base.WritePacketLength();
            WriteShort((ushort) (Length - 3));
        }

        public override string ToString()
        {
            return $"{base.ToString()}: Size={Length - 5} ID=0x{PacketCode:X2}";
        }

        public override void Close()
        {
            // Called by Dispose and normally invalidates the stream.
            // But this is both pointless (`MemoryStream` doesn't have any unmanaged resource) and undesirable (we always want the buffer to remain accessible)
        }
    }
}
