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
    // RPC - 
    // This is a stub providing a bare minimum of configuration options along with some reasonable default values.
    // It's not supposed to be understood as a fully-fledged Renode model.
    // Note: the documentation explicitly disallows 8- and 16-bit transfers
    public class S32K148_RPC : BasicDoubleWordPeripheral, IKnownSize
    {

        public S32K148_RPC(IMachine machine) : base(machine)
        {
            // 0x4007F000 : RCM_VERID
            Registers.VersionIDRegister.Define(this)
                .WithValueField(0, 15, name: "FEATURE")
                .WithValueField(16, 8, name: "MINOR")
                .WithValueField(24, 8, name: "MAJOR")
                ;
            // 0x4007F00C : RCM_RPC
            Registers.ResetPinControlRegister.Define(this)
                .WithValueField(0, 2, name: "RSTFLTSRW")
                .WithFlag(2, name: "RSTFLTSS")
                .WithReservedBits(3, 5)
                .WithValueField(8, 5, name: "RSTFLTSEL")
                .WithReservedBits(13, 19)
                ;
            // 0x4007F018 : RCM_SSRS
            Registers.StickySystemResetStatusRegister.Define(this)
                .WithReservedBits(0, 1)
                .WithFlag(1, name: "SLVD")
                .WithFlag(2, name: "SLOC")
                .WithFlag(3, name: "SLOL")
                .WithFlag(4, name: "SCMU_LOC")
                .WithFlag(5, name: "SWDOG")
                .WithFlag(6, name: "SPIN")
                .WithFlag(7, name: "SPOR")
                .WithFlag(8, name: "SJTAG")
                .WithFlag(9, name: "SLOCKUP")
                .WithFlag(10, name: "SSW")
                .WithFlag(11, name: "SMDM_AP")
                .WithReservedBits(12, 1)
                .WithFlag(13, name: "SSACKERR")
                .WithReservedBits(14, 18)
                ;
            // 0x4007F018 : RCM_SRIE
            Registers.SystemResetInterruptEnableRegister.Define(this)
                .WithValueField(0, 2, name: "DELAY")
                .WithFlag(2, name: "LOC")
                .WithFlag(3, name: "LOL")
                .WithFlag(4, name: "CMU_LOC")
                .WithFlag(5, name: "WDOG")
                .WithFlag(6, name: "PIN")
                .WithFlag(7, name: "GIE")
                .WithFlag(8, name: "JTAG")
                .WithFlag(9, name: "LOCKUP")
                .WithFlag(10, name: "SW")
                .WithFlag(11, name: "MDM_AP")
                .WithReservedBits(12, 1)
                .WithFlag(13, name: "SACKERR")
                .WithReservedBits(14, 18)
                ;

             
        }

        public long Size => 0x1000;

        private enum Registers
        {
            VersionIDRegister = 0x0,
            ParameterRegister = 0x4,
            SystemResetStatusRegister = 0x8,
            ResetPinControlRegister = 0xC,
            StickySystemResetStatusRegister = 0x18,
            SystemResetInterruptEnableRegister = 0x1C,
        }
    }
}
