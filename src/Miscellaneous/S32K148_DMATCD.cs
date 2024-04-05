//
// Copyright (c) 2010-2022 Antmicro
// Copyright (c) 2022 ION Mobility
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    // DMA TCD Control and Status
    // This is a stub providing a bare minimum of configuration options along with some reasonable default values.
    // It's not supposed to be understood as a fully-fledged Renode model.
    // Note: the documentation explicitly disallows 8- and 16-bit transfers
    public class S32K148_DMATCD : BasicDoubleWordPeripheral, IKnownSize
    {

        public S32K148_DMATCD(IMachine machine) : base(machine)
        {
            DefineRegisters();
        }

        private void DefineRegisters()
        {
            // 0x4000901C : TCD0_CSR
            Registers.TCD0_CSR.Define(this)
                .WithFlag(0, name: "START")
                .WithFlag(1, name: "INTMAJOR")
                .WithFlag(2, name: "INTHALF")
                .WithFlag(3, name: "DREQ")
                .WithFlag(4, name: "ESG")
                .WithFlag(5, name: "MAJORELINK")
                .WithFlag(6, name: "ACTIVE")
                .WithFlag(7, name: "DONE")
                .WithValueField(8, 4, name: "MAJORLINKCH")
                .WithReservedBits(12, 2)
                .WithValueField(14, 2, name: "BWC")
                ;             
            // 0x4000901C : TCD1_CSR
            Registers.TCD1_CSR.Define(this)
                .WithFlag(0, name: "START")
                .WithFlag(1, name: "INTMAJOR")
                .WithFlag(2, name: "INTHALF")
                .WithFlag(3, name: "DREQ")
                .WithFlag(4, name: "ESG")
                .WithFlag(5, name: "MAJORELINK")
                .WithFlag(6, name: "ACTIVE")
                .WithFlag(7, name: "DONE")
                .WithValueField(8, 4, name: "MAJORLINKCH")
                .WithReservedBits(12, 2)
                .WithValueField(14, 2, name: "BWC")
                ;             
        }

        public override void WriteDoubleWord(long offset, uint value)
        {
            switch(offset)
            {
                case 0x1C:
                case 0x3C:
                    WriteWord(offset, (ushort)(value & 0xFFFF));
                    break;
                default:
                    break;

            }
        }

        public override uint ReadDoubleWord(long offset)
        {
            switch(offset)
            {
                case 0x1C:
                case 0x3C:
                    return ReadWord(offset);
                default:
                    return 0;
            }
        }
        
        public ushort ReadWord(long offset)
        {
            return (ushort)ReadDoubleWord(offset);
        }

        public void WriteWord(long offset, ushort value)
        {
            WriteDoubleWord(offset, (uint)value);
        }

        public long Size => 0x1000;


        private enum Registers
        {
            TCD0_CSR = 0x1C,
            TCD1_CSR = 0x3C
        }

    }
}
