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
    // PCC - Perpheral Clock Control
    // This is a stub providing a bare minimum of configuration options along with some reasonable default values.
    // It's not supposed to be understood as a fully-fledged Renode model.
    // Note: the documentation explicitly disallows 8- and 16-bit transfers
    public class S32K148_PCC : BasicDoubleWordPeripheral, IKnownSize
    {
        public S32K148_PCC(IMachine machine) : base(machine)
        {
            // 0xE0
            Registers.PCCFTM0Register.Define(this)
                .WithReservedBits(0, 24)
                .WithValueField(24, 3, name: "PCS")
                .WithReservedBits(27, 3)
                .WithFlag(30, name: "CGC")
                .WithFlag(31, name: "PR")
            ;
        }

        public long Size => 0x1000;

        // private readonly IValueRegisterField systemClockSource;
        // private readonly IValueRegisterField coreClockRatio;
        // private readonly IValueRegisterField busClockRatio;
        // private readonly IValueRegisterField slowClockRatio;
        // private readonly IFlagRegisterField slowIRCEnable;

        private enum Registers
        {
            PCCFTM0Register = 0xE0
        }
    }
}
