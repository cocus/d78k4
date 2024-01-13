// See https://aka.ms/new-console-template for more information

using System.Diagnostics;
using System.Reflection.PortableExecutable;

public class OpByteMatcher
{
    public Byte Mask { get; set; }
    public Byte Val { get; set; }
    public OpByteMatcher(Byte mask, Byte val)
    {
        Mask = mask;
        Val = val;
    }

    public bool Match(Byte against)
    {
        return (against & Mask) == Val;
    }
}

public class OpOperand
{
    public string Name { get; set; }
    public OpOperand(string name) => Name = name;
}
public class Op
{
    public List<OpByteMatcher> MaskBytes { get; set; }
    public int NumOfBytes { get; set; }
    public string Name { get; set; }
    public string OperandsString { get; set; }

    public List<OpOperand> Operands { get; set; }

    public Op(int numOfBytes, string name, string operands, OpByteMatcher b1, OpByteMatcher b2, OpByteMatcher b3, OpByteMatcher b4, OpByteMatcher b5, OpByteMatcher b6, OpByteMatcher b7)
        : this(numOfBytes, name, operands, b1, b2, b3, b4, b5, b6)
    {
        MaskBytes.Add(b7);
    }
    public Op(int numOfBytes, string name, string operands, OpByteMatcher b1, OpByteMatcher b2, OpByteMatcher b3, OpByteMatcher b4, OpByteMatcher b5, OpByteMatcher b6)
        : this(numOfBytes, name, operands, b1, b2, b3, b4, b5)
    {
        MaskBytes.Add(b6);
    }

    public Op(int numOfBytes, string name, string operands, OpByteMatcher b1, OpByteMatcher b2, OpByteMatcher b3, OpByteMatcher b4, OpByteMatcher b5)
        : this(numOfBytes, name, operands, b1, b2, b3, b4)
    {
        MaskBytes.Add(b5);
    }

    public Op(int numOfBytes, string name, string operands, OpByteMatcher b1, OpByteMatcher b2, OpByteMatcher b3, OpByteMatcher b4)
        : this(numOfBytes, name, operands, b1, b2, b3)
    {
        MaskBytes.Add(b4);
    }

    public Op(int numOfBytes, string name, string operands, OpByteMatcher b1, OpByteMatcher b2, OpByteMatcher b3)
        : this(numOfBytes, name, operands, b1, b2)
    {
        MaskBytes.Add(b3);
    }

    public Op(int numOfBytes, string name, string operands, OpByteMatcher b1, OpByteMatcher b2)
        : this(numOfBytes, name, operands, b1)
    {
        MaskBytes.Add(b2);
    }

    public Op(int numOfBytes, string name, string operands, OpByteMatcher b1)
    {
        NumOfBytes = numOfBytes;
        Name = name;
        OperandsString = operands;

        MaskBytes = [b1];
    }

    public virtual bool Process(BinaryReader r) => false;
}

public class OpLocation : Op
{
    public OpLocation()
        : base(4, 
            "LOCATION",
            "locaddr",
            new OpByteMatcher(0b11111111, 0b00001001), new OpByteMatcher(0b11111111, 0b11000001))
    {
        Operands = [new OpOperand("locaddr")];
    }

    public override bool Process(BinaryReader r)
    {

        r.ReadBytes(2);
        var LocalAddr = r.ReadUInt16();
     
        // page 205
        if (LocalAddr == 0x01fe)
        {
            LocalAddr = 0;
        }
        else if (LocalAddr == 0x00ff)
        {
            LocalAddr = 0xf;
        }
        OperandsString = "#" + String.Format("{0:X4}", LocalAddr);

        return true;
    }
}


public class OpMovG_SP_IMM24 : Op
{
    public OpMovG_SP_IMM24()
      : base(5,
          "MOVG",
          "SP, #imm24",
          new OpByteMatcher(0b11111111, 0b00001001), new OpByteMatcher(0b11111111, 0b00100000))
    {
        Operands = [new OpOperand("SP"), new OpOperand("#imm24")];
    }

    public override bool Process(BinaryReader r)
    {

        r.ReadBytes(2);
        UInt32 Imm24 = r.ReadUInt16();
        Imm24 |= ((UInt32)r.ReadByte()) << 16;
        OperandsString = "SP, #" + String.Format("{0:X6}", Imm24);

        return true;
    }

}
public static class MainClass
{
    public static List<Op> ops = new List<Op>();

    public static Op? GetOp(BinaryReader reader)
    {
        var PrePos = reader.BaseStream.Position;
        byte StartByte = reader.ReadByte();
        //System.Console.WriteLine($"At {reader.BaseStream.Position} = {String.Format("{0:X2}", StartByte)}");

        foreach (var item in ops)
        {
            reader.BaseStream.Seek(PrePos, SeekOrigin.Begin);
            byte CurrentByte = reader.ReadByte();
            bool FailToMatch = false;

            // validate all bytes of ops
            foreach (var ByteToMatch in item.MaskBytes)
            {
                if (!ByteToMatch.Match(CurrentByte))
                {
                    FailToMatch = true;
                    break;
                }
                CurrentByte = reader.ReadByte();
            }

            // try next op
            if (FailToMatch)
            {
                continue;
            }

            reader.BaseStream.Seek(PrePos, SeekOrigin.Begin);

            if (!item.Process(reader))
            {
                // seek the other bytes (should let the Op read these)
                reader.BaseStream.Seek(PrePos + item.NumOfBytes, SeekOrigin.Begin);
            }

            return item;
        }

        reader.BaseStream.Seek(PrePos, SeekOrigin.Begin);
        return null;
    }
    public static void Main()
    {
        Console.WriteLine("Hello, World!");

        ops.Add(new Op(2, "MOV", "r1, #byte", new OpByteMatcher(0b11111000, 0b10111000)));
        ops.Add(new Op(3, "MOV", "r2, #byte", new OpByteMatcher(0b11111111, 0b00111100), new OpByteMatcher(0b11111000, 0b10111000)));
        ops.Add(new Op(3, "MOV", "saddr2, #byte", new OpByteMatcher(0b11111111, 0b00111010)));
        ops.Add(new Op(4, "MOV", "saddr1, #byte", new OpByteMatcher(0b11111111, 0b00111100), new OpByteMatcher(0b11111111, 0b00111010)));
        ops.Add(new Op(3, "MOV", "sfr, #byte", new OpByteMatcher(0b11111111, 0b00101011)));
        ops.Add(new Op(5, "MOV", "!addr16, #byte", new OpByteMatcher(0b11111111, 0b00001001), new OpByteMatcher(0b11111111, 0b01000000)));
        ops.Add(new Op(6, "MOV", "!addr24, #byte", new OpByteMatcher(0b11111111, 0b00001001), new OpByteMatcher(0b11111111, 0b01010000)));
        ops.Add(new Op(2, "MOV", "r, r1", new OpByteMatcher(0b11111111, 0b00100100), new OpByteMatcher(0b00001000, 0b00000000)));
        ops.Add(new Op(3, "MOV", "r, r2", new OpByteMatcher(0b11111111, 0b00111100), new OpByteMatcher(0b11111111, 0b00100100), new OpByteMatcher(0b00001000, 0b00000000)));
        ops.Add(new Op(1, "MOV", "A, r1", new OpByteMatcher(0b11111000, 0b11010000)));
        ops.Add(new Op(2, "MOV", "A, r2", new OpByteMatcher(0b11111111, 0b00111100), new OpByteMatcher(0b11111000, 0b11010000)));
        ops.Add(new Op(2, "MOV", "A, saddr2", new OpByteMatcher(0b11111111, 0b00100000)));
        ops.Add(new Op(3, "MOV", "r, saddr2", new OpByteMatcher(0b11111111, 0b00111000), new OpByteMatcher(0b00001111, 0b00000000)));
        ops.Add(new Op(3, "MOV", "r, saddr1", new OpByteMatcher(0b11111111, 0b00111000), new OpByteMatcher(0b00001111, 0b00000001)));
        ops.Add(new Op(2, "MOV", "saddr2, A", new OpByteMatcher(0b11111111, 0b00100010)));
        ops.Add(new Op(3, "MOV", "saddr2, r", new OpByteMatcher(0b11111111, 0b00111000), new OpByteMatcher(0b00001111, 0b00000100)));
        ops.Add(new Op(3, "MOV", "saddr1, r", new OpByteMatcher(0b11111111, 0b00111000), new OpByteMatcher(0b00001111, 0b00000101)));
        ops.Add(new Op(2, "MOV", "A, sfr", new OpByteMatcher(0b11111111, 0b00010000)));
        ops.Add(new Op(3, "MOV", "r, sfr", new OpByteMatcher(0b11111111, 0b00111000), new OpByteMatcher(0b00001111, 0b00000010)));
        ops.Add(new Op(2, "MOV", "sfr, A", new OpByteMatcher(0b11111111, 0b00010010)));
        ops.Add(new Op(3, "MOV", "sfr, r", new OpByteMatcher(0b11111111, 0b00111000), new OpByteMatcher(0b00001111, 0b00000110)));
        ops.Add(new Op(4, "MOV", "saddr2, saddr2'", new OpByteMatcher(0b11111111, 0b00101010), new OpByteMatcher(0b11111111, 0b00000000)));
        ops.Add(new Op(4, "MOV", "saddr2, saddr1", new OpByteMatcher(0b11111111, 0b00101010), new OpByteMatcher(0b11111111, 0b00010000)));
        ops.Add(new Op(4, "MOV", "saddr2, saddr2", new OpByteMatcher(0b11111111, 0b00101010), new OpByteMatcher(0b11111111, 0b00100000)));

        ops.Add(new Op(4, "MOV", "saddr1, saddr1'", new OpByteMatcher(0b11111111, 0b00101010), new OpByteMatcher(0b11111111, 0b00110000)));
        ops.Add(new Op(4, "MOV", "r, !addr16", new OpByteMatcher(0b11111111, 0b00111110), new OpByteMatcher(0b00001111, 0b00000000)));
        ops.Add(new Op(4, "MOV", "!addr16, r", new OpByteMatcher(0b11111111, 0b00111110), new OpByteMatcher(0b00001111, 0b00000001)));
        ops.Add(new Op(5, "MOV", "r, !!addr24", new OpByteMatcher(0b11111111, 0b00111110), new OpByteMatcher(0b00001111, 0b00000010)));
        ops.Add(new Op(5, "MOV", "!!addr24, r", new OpByteMatcher(0b11111111, 0b00111110), new OpByteMatcher(0b00001111, 0b00000011)));
        ops.Add(new Op(2, "MOV", "A, [saddrp2]", new OpByteMatcher(0b11111111, 0b00011000)));
        ops.Add(new Op(3, "MOV", "A, [saddrp1]", new OpByteMatcher(0b11111111, 0b00111000), new OpByteMatcher(0b11111111, 0b00011000)));
        ops.Add(new Op(3, "MOV", "A, [%saddrg2]", new OpByteMatcher(0b11111111, 0b00000111), new OpByteMatcher(0b11111111, 0b00110000)));
        ops.Add(new Op(4, "MOV", "A, [%saddrg1]", new OpByteMatcher(0b11111111, 0b00111100), new OpByteMatcher(0b11111111, 0b00000111), new OpByteMatcher(0b11111111, 0b00110000)));
        ops.Add(new Op(1, "MOV", "A, [TDE +]", new OpByteMatcher(0b11111111, 0b01011000)));
        ops.Add(new Op(1, "MOV", "A, [WHL +]", new OpByteMatcher(0b11111111, 0b01011001)));
        ops.Add(new Op(1, "MOV", "A, [TDE -]", new OpByteMatcher(0b11111111, 0b01011010)));
        ops.Add(new Op(1, "MOV", "A, [WHL -]", new OpByteMatcher(0b11111111, 0b01011011)));
        ops.Add(new Op(1, "MOV", "A, [TDE]", new OpByteMatcher(0b11111111, 0b01011100)));
        ops.Add(new Op(1, "MOV", "A, [WHL]", new OpByteMatcher(0b11111111, 0b01011101)));
        ops.Add(new Op(2, "MOV", "A, [VVP]", new OpByteMatcher(0b11111111, 0b00010110), new OpByteMatcher(0b11111111, 0b01100000)));
        ops.Add(new Op(2, "MOV", "A, [UUP]", new OpByteMatcher(0b11111111, 0b00010110), new OpByteMatcher(0b11111111, 0b01110000)));
        ops.Add(new Op(3, "MOV", "A, [TDE + byte]", new OpByteMatcher(0b11111111, 0b00000110), new OpByteMatcher(0b11111111, 0b00000000)));
        ops.Add(new Op(3, "MOV", "A, [SP + byte]", new OpByteMatcher(0b11111111, 0b00000110), new OpByteMatcher(0b11111111, 0b00010000)));
        ops.Add(new Op(3, "MOV", "A, [WHL + byte]", new OpByteMatcher(0b11111111, 0b00000110), new OpByteMatcher(0b11111111, 0b00100000)));
        ops.Add(new Op(3, "MOV", "A, [UUP + byte]", new OpByteMatcher(0b11111111, 0b00000110), new OpByteMatcher(0b11111111, 0b00110000)));
        ops.Add(new Op(3, "MOV", "A, [VVP + byte]", new OpByteMatcher(0b11111111, 0b00000110), new OpByteMatcher(0b11111111, 0b01000000)));
        ops.Add(new Op(5, "MOV", "A, imm24 [DE]", new OpByteMatcher(0b11111111, 0b00001010), new OpByteMatcher(0b11111111, 0b00000000)));
        ops.Add(new Op(5, "MOV", "A, imm24 [A]", new OpByteMatcher(0b11111111, 0b00001010), new OpByteMatcher(0b11111111, 0b00010000)));
        ops.Add(new Op(5, "MOV", "A, imm24 [HL]", new OpByteMatcher(0b11111111, 0b00001010), new OpByteMatcher(0b11111111, 0b00100000)));

        ops.Add(new Op(5, "MOV", "A, imm24 [B]", new OpByteMatcher(0b11111111, 0b00001010), new OpByteMatcher(0b11111111, 0b00110000)));
        ops.Add(new Op(2, "MOV", "A, [TDE + A]", new OpByteMatcher(0b11111111, 0b00010111), new OpByteMatcher(0b11111111, 0b00000000)));
        ops.Add(new Op(2, "MOV", "A, [WHL + A]", new OpByteMatcher(0b11111111, 0b00010111), new OpByteMatcher(0b11111111, 0b00010000)));
        ops.Add(new Op(2, "MOV", "A, [TDE + B]", new OpByteMatcher(0b11111111, 0b00010111), new OpByteMatcher(0b11111111, 0b00100000)));
        ops.Add(new Op(2, "MOV", "A, [WHL + B]", new OpByteMatcher(0b11111111, 0b00010111), new OpByteMatcher(0b11111111, 0b00110000)));
        ops.Add(new Op(2, "MOV", "A, [VVP + DE]", new OpByteMatcher(0b11111111, 0b00010111), new OpByteMatcher(0b11111111, 0b01000000)));
        ops.Add(new Op(2, "MOV", "A, [VVP + HL]", new OpByteMatcher(0b11111111, 0b00010111), new OpByteMatcher(0b11111111, 0b01010000)));
        ops.Add(new Op(2, "MOV", "A, [TDE + C]", new OpByteMatcher(0b11111111, 0b00010111), new OpByteMatcher(0b11111111, 0b01100000)));
        ops.Add(new Op(2, "MOV", "A, [WHL + C]", new OpByteMatcher(0b11111111, 0b00010111), new OpByteMatcher(0b11111111, 0b01110000)));
        ops.Add(new Op(2, "MOV", "[saddrp2], A", new OpByteMatcher(0b11111111, 0b00011001)));
        ops.Add(new Op(3, "MOV", "[saddrp1], A", new OpByteMatcher(0b11111111, 0b00111100), new OpByteMatcher(0b11111111, 0b00011001)));
        ops.Add(new Op(3, "MOV", "[%saddrg2], A", new OpByteMatcher(0b11111111, 0b00000111), new OpByteMatcher(0b11111111, 0b10110000)));
        ops.Add(new Op(4, "MOV", "[%saddrg1], A", new OpByteMatcher(0b11111111, 0b00111100), new OpByteMatcher(0b11111111, 0b00000111), new OpByteMatcher(0b11111111, 0b10110000)));
        ops.Add(new Op(1, "MOV", "[TDE +], A", new OpByteMatcher(0b11111111, 0b01010000)));
        ops.Add(new Op(1, "MOV", "[WHL +], A", new OpByteMatcher(0b11111111, 0b01010001)));
        ops.Add(new Op(1, "MOV", "[TDE -], A", new OpByteMatcher(0b11111111, 0b01010010)));
        ops.Add(new Op(1, "MOV", "[WHL -], A", new OpByteMatcher(0b11111111, 0b01010011)));
        ops.Add(new Op(1, "MOV", "[TDE], A", new OpByteMatcher(0b11111111, 0b01010100)));
        ops.Add(new Op(1, "MOV", "[WHL], A", new OpByteMatcher(0b11111111, 0b01010101)));
        ops.Add(new Op(2, "MOV", "[VVP], A", new OpByteMatcher(0b11111111, 0b00010110), new OpByteMatcher(0b11111111, 0b11100000)));
        ops.Add(new Op(2, "MOV", "[UUP], A", new OpByteMatcher(0b11111111, 0b00010110), new OpByteMatcher(0b11111111, 0b11110000)));
        ops.Add(new Op(3, "MOV", "[TDE + byte], A", new OpByteMatcher(0b11111111, 0b00000110), new OpByteMatcher(0b11111111, 0b10000000)));
        ops.Add(new Op(3, "MOV", "[SP + byte], A", new OpByteMatcher(0b11111111, 0b00000110), new OpByteMatcher(0b11111111, 0b10010000)));
        ops.Add(new Op(3, "MOV", "[WHL + byte], A", new OpByteMatcher(0b11111111, 0b00000110), new OpByteMatcher(0b11111111, 0b10100000)));
        ops.Add(new Op(3, "MOV", "[UUP + byte], A", new OpByteMatcher(0b11111111, 0b00000110), new OpByteMatcher(0b11111111, 0b10110000)));
        ops.Add(new Op(3, "MOV", "[VVP + byte], A", new OpByteMatcher(0b11111111, 0b00000110), new OpByteMatcher(0b11111111, 0b11000000)));
        ops.Add(new Op(5, "MOV", "imm24 [DE], A", new OpByteMatcher(0b11111111, 0b00001010), new OpByteMatcher(0b11111111, 0b10000000)));
        ops.Add(new Op(5, "MOV", "imm24 [A], A", new OpByteMatcher(0b11111111, 0b00001010), new OpByteMatcher(0b11111111, 0b10010000)));
        ops.Add(new Op(5, "MOV", "imm24 [HL], A", new OpByteMatcher(0b11111111, 0b00001010), new OpByteMatcher(0b11111111, 0b10100000)));

        ops.Add(new Op(5, "MOV", "imm24 [B], A", new OpByteMatcher(0b11111111, 0b00001010), new OpByteMatcher(0b11111111, 0b10110000)));
        ops.Add(new Op(2, "MOV", "[TDE + A], A", new OpByteMatcher(0b11111111, 0b00010111), new OpByteMatcher(0b11111111, 0b10000000)));
        ops.Add(new Op(2, "MOV", "[WHL + A], A", new OpByteMatcher(0b11111111, 0b00010111), new OpByteMatcher(0b11111111, 0b10010000)));
        ops.Add(new Op(2, "MOV", "[TDE + B], A", new OpByteMatcher(0b11111111, 0b00010111), new OpByteMatcher(0b11111111, 0b10100000)));
        ops.Add(new Op(2, "MOV", "[WHL + B], A", new OpByteMatcher(0b11111111, 0b00010111), new OpByteMatcher(0b11111111, 0b10110000)));
        ops.Add(new Op(2, "MOV", "[VVP + DE], A", new OpByteMatcher(0b11111111, 0b00010111), new OpByteMatcher(0b11111111, 0b11000000)));
        ops.Add(new Op(2, "MOV", "[VVP + HL], A", new OpByteMatcher(0b11111111, 0b00010111), new OpByteMatcher(0b11111111, 0b11010000)));
        ops.Add(new Op(2, "MOV", "[TDE + C], A", new OpByteMatcher(0b11111111, 0b00010111), new OpByteMatcher(0b11111111, 0b11100000)));
        ops.Add(new Op(2, "MOV", "[WHL + C], A", new OpByteMatcher(0b11111111, 0b00010111), new OpByteMatcher(0b11111111, 0b11110000)));
        ops.Add(new Op(3, "MOV", "PSWL, #byte", new OpByteMatcher(0b11111111, 0b00101011), new OpByteMatcher(0b11111111, 0b11111110)));
        ops.Add(new Op(3, "MOV", "PSWH, #byte", new OpByteMatcher(0b11111111, 0b00101011), new OpByteMatcher(0b11111111, 0b11111111)));
        ops.Add(new Op(2, "MOV", "PSWL, A", new OpByteMatcher(0b11111111, 0b00010010), new OpByteMatcher(0b11111111, 0b11111110)));
        ops.Add(new Op(2, "MOV", "PSWH, A", new OpByteMatcher(0b11111111, 0b00010010), new OpByteMatcher(0b11111111, 0b11111111)));
        ops.Add(new Op(2, "MOV", "A, PSWL", new OpByteMatcher(0b11111111, 0b00010000), new OpByteMatcher(0b11111111, 0b11111110)));
        ops.Add(new Op(2, "MOV", "A, PSWH", new OpByteMatcher(0b11111111, 0b00010000), new OpByteMatcher(0b11111111, 0b11111111)));
        ops.Add(new Op(3, "MOV", "V, #byte", new OpByteMatcher(0b11111111, 0b00000111), new OpByteMatcher(0b11111111, 0b01100001)));
        ops.Add(new Op(3, "MOV", "U, #byte", new OpByteMatcher(0b11111111, 0b00000111), new OpByteMatcher(0b11111111, 0b01100011)));
        ops.Add(new Op(3, "MOV", "T, #byte", new OpByteMatcher(0b11111111, 0b00000111), new OpByteMatcher(0b11111111, 0b01100101)));
        ops.Add(new Op(3, "MOV", "W, #byte", new OpByteMatcher(0b11111111, 0b00000111), new OpByteMatcher(0b11111111, 0b01100111)));
        ops.Add(new Op(2, "MOV", "A, V", new OpByteMatcher(0b11111111, 0b00000101), new OpByteMatcher(0b11111111, 0b11000001)));
        ops.Add(new Op(2, "MOV", "A, U", new OpByteMatcher(0b11111111, 0b00000101), new OpByteMatcher(0b11111111, 0b11000011)));
        ops.Add(new Op(2, "MOV", "A, T", new OpByteMatcher(0b11111111, 0b00000101), new OpByteMatcher(0b11111111, 0b11000101)));
        ops.Add(new Op(2, "MOV", "A, W", new OpByteMatcher(0b11111111, 0b00000101), new OpByteMatcher(0b11111111, 0b11000111)));
        ops.Add(new Op(2, "MOV", "V, A", new OpByteMatcher(0b11111111, 0b00000101), new OpByteMatcher(0b11111111, 0b11001001)));
        ops.Add(new Op(2, "MOV", "U, A", new OpByteMatcher(0b11111111, 0b00000101), new OpByteMatcher(0b11111111, 0b11001011)));
        ops.Add(new Op(2, "MOV", "T, A", new OpByteMatcher(0b11111111, 0b00000101), new OpByteMatcher(0b11111111, 0b11001101)));
        ops.Add(new Op(2, "MOV", "W, A", new OpByteMatcher(0b11111111, 0b00000101), new OpByteMatcher(0b11111111, 0b11001111)));




        // page 261
        ops.Add(new OpLocation());
        
        // page 256
        ops.Add(new OpMovG_SP_IMM24());



        using (var f = System.IO.File.Open("27C512.bin", FileMode.Open))
        using (var reader = new BinaryReader(f))
        {

            var VectorReset = reader.ReadUInt16();
            System.Console.WriteLine($"Vector Reset = {String.Format("{0:X4}", VectorReset)}");
            f.Seek(VectorReset, SeekOrigin.Begin);



            // while start here
            while (reader.BaseStream.CanRead)
            {
                var OpPos = reader.BaseStream.Position;
                var op = GetOp(reader);
                if (op != null)
                {
                    System.Console.WriteLine($"{String.Format("{0:X4}", OpPos)}:  {op.Name} {op.OperandsString}");
                }
                else
                {
                    System.Console.WriteLine($"{String.Format("{0:X4}", OpPos)}:  Unknown {String.Format("{0:X2}", reader.ReadByte())}");
                    return;
                }
            }
        }

    }
}
