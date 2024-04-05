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
    // FTFC - Flash Memory Module
    // This is a stub providing a bare minimum of configuration options along with some reasonable default values.
    // It's not supposed to be understood as a fully-fledged Renode model.
    // Note: the documentation explicitly disallows 8- and 16-bit transfers
    public class S32K148_FTFC : BasicBytePeripheral, IKnownSize
    {
        public S32K148_FTFC(IMachine machine) : base(machine)
        {
            DefineRegisters();
        }

        public long Size => 0x1000;

        protected override void DefineRegisters()
        {
            // 0x17
            Registers.DataFlashProtectionRegister.Define(this)
                // .WithValueField(0, 8, valueProviderCallback: _ => 1, name: "DPROT"
                .WithValueField(0, 8, name: "DPROT",
                    writeCallback: (_, value) => {
                        // write : Update internal status with input value
                        dataFlashProtectionStatus = (byte)value;
                    },
                    valueProviderCallback: _ => {
                        // read : Returning current protection status 
                        return dataFlashProtectionStatus;
                    }
                );
        }

        private byte dataFlashProtectionStatus;

        private enum Registers
        {
            DataFlashProtectionRegister = 0x17
        }
    }
}
