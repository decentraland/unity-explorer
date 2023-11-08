using System;
using System.IO;
using System.Text;

public class BSC5
{
    public struct Header
    {
        public int STAR0;
        public int STAR1;
        public int STARN;
        public int STNUM;
        public int MPROP;
        public int NMAG;
        public int NBENT;
    }

    public struct Entry
    {
        public float XNO;
        public double SRA0;
        public double SDEC0;
        public string IS;
        public short MAG;
        public float XRPM;
        public float XDPM;
    }

    public readonly Header header;
    public readonly Entry[] entries;

    private BSC5(Header header, Entry[] entries)
    {
        this.header = header;
        this.entries = entries;
    }

    public static BSC5 Parse(byte[] data)
    {
        using (var memStream = new MemoryStream(data))
        {
            using (var binaryReader = new BinaryReader(memStream))
            {
                BSC5Reader reader = new BSC5Reader(binaryReader);
                Header header = reader.ReadHeader();
                Entry[] entries = new Entry[header.STARN];
                for (int i = 0; i < entries.Length; i++)
                {
                    entries[i] = reader.ReadEntry(header.STNUM, header.MPROP);
                }
                return new BSC5(header, entries);
            }
        }
    }
}

public class BSC5Reader
{
    BinaryReader reader;

    public BSC5Reader(BinaryReader reader)
    {
        this.reader = reader;
    }

    public BSC5.Header ReadHeader()
    {
        BSC5.Header header = new BSC5.Header();
        header.STAR0 = reader.ReadInt32();
        header.STAR1 = reader.ReadInt32();
        header.STARN = Math.Abs(reader.ReadInt32());
        header.STNUM = reader.ReadInt32();
        header.MPROP = reader.ReadInt32();
        header.NMAG = reader.ReadInt32();
        header.NBENT = reader.ReadInt32();
        return header;
    }

    public BSC5.Entry ReadEntry(int STNUM, int MPROP)
    {
        BSC5.Entry entry = new BSC5.Entry();
        switch (STNUM)
        {
            case 0:
                break;
            case 1:
            case 2:
                entry.XNO = reader.ReadSingle();
                break;
            default:
                throw new System.ArgumentException("STNUM must be 0, 1, or 2");
        }
        entry.SRA0 = reader.ReadDouble();
        entry.SDEC0 = reader.ReadDouble();
        entry.IS = Encoding.ASCII.GetString(reader.ReadBytes(2));
        entry.MAG = reader.ReadInt16();
        if (MPROP == 1)
        {
            entry.XRPM = reader.ReadSingle();
            entry.XDPM = reader.ReadSingle();
        }
        return entry;
    }
}
