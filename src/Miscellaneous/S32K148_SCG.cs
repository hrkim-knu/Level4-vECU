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
    // SCG - System Clock Generator
    // This is a stub providing a bare minimum of configuration options along with some reasonable default values.
    // It's not supposed to be understood as a fully-fledged Renode model.
    // Note: the documentation explicitly disallows 8- and 16-bit transfers
    public class S32K148_SCG : BasicDoubleWordPeripheral, IKnownSize
    {
        public S32K148_SCG(IMachine machine) : base(machine)
        {
            // 0x10
            Registers.ClockStatus.Define(this)
                .WithValueField(0, 4, FieldMode.Read, valueProviderCallback: _ => slowClockRatio.Value, name: "DIVSLOW")
                .WithValueField(4, 4, FieldMode.Read, valueProviderCallback: _ => busClockRatio.Value, name: "DIVBUS")
                .WithReservedBits(8, 8)
                .WithValueField(16, 4, FieldMode.Read, valueProviderCallback: _ => coreClockRatio.Value, name: "DIVCORE")
                .WithReservedBits(20, 4)
                .WithValueField(24, 4, FieldMode.Read, valueProviderCallback: _ => systemClockSource.Value, name: "SCS")
                .WithReservedBits(28, 4)
            ;
            // 0x14 : Setteted in Mcu_SCG_SystemClockInit()
            Registers.RunClockControl.Define(this)
                // binding
                .WithValueField(0, 4, out slowClockRatio, name: "DIVSLOW") // hrkim : write 3 -> Divide by 4
                .WithValueField(4, 4, out busClockRatio, name: "DIVBUS") // hrkim : write 1 -> Divide by 2
                .WithReservedBits(8, 8)
                .WithValueField(16, 4, out coreClockRatio, name: "DIVCORE") // hrkim : write 1 -> Divide by 2
                .WithReservedBits(20, 4)
                .WithValueField(24, 4, out systemClockSource, name: "SCS") // hrkim : write 6 -> System PLL
                .WithReservedBits(28, 4)
            ;
            // 0x18 : editted by hrkim
            Registers.VLPRClockControl.Define(this)
                .WithValueField(0, 4, name: "DIVSLOW") // hrkim : write 3 
                .WithValueField(4, 4, name: "DIVBUS") // hrkim : write 0
                .WithReservedBits(8, 8)
                .WithValueField(16, 4, name: "DIVCORE") //hrkim : write 1
                .WithReservedBits(20, 4)
                .WithValueField(24, 4, name: "SCS") // hrkim : write 2 -> Slow IRC(SIRC_CLK)
                .WithReservedBits(28, 4)
            ;
            // 0x1C
            Registers.HSRUNClockControl.Define(this)
                .WithValueField(0, 4, name: "DIVSLOW") // hrkim : write 3
                .WithValueField(4, 4, name: "DIVBUS") // hrkim : write 1
                .WithReservedBits(8, 8)
                .WithValueField(16, 4, name: "DIVCORE") // hrkim : write 1
                .WithReservedBits(20, 4)
                .WithValueField(24, 4, name: "SCS") // hrkim : write 2 -> Reserved
                .WithReservedBits(28, 4)
            ;
            // 0x20 : editted by hrkim
            Registers.CLKOUTConfiguration.Define(this)
                .WithReservedBits(0, 24)
                .WithValueField(24, 4, name: "CLKOUTSEL") // hrkim : write 0110 -> Selects the SCG system clock : System PLL (SPLL_CLK)
                .WithReservedBits(28, 4)
            ;

            // 0x100 : editted by hrkim
            Registers.OscillatorControlStatus.Define(this)
                .WithFlag(0, name: "SOSCEN") // hrkim : write 1--> System OSC is enabled
                .WithReservedBits(1, 15)
                .WithFlag(16, valueProviderCallback: _ => false, name: "SOSCCM")
                .WithFlag(17, valueProviderCallback: _ => false, name: "SOSCCMRE")
                .WithReservedBits(18, 5)
                .WithFlag(23, valueProviderCallback: _ => false, name: "LK")
                .WithFlag(24, FieldMode.Read, valueProviderCallback: _ => true, name: "SOSCVLD")
                .WithFlag(25, FieldMode.Read, valueProviderCallback: _ => false, name: "SOSCSEL")
                .WithFlag(26, FieldMode.WriteOneToClear, name: "SOSCERR")
                .WithReservedBits(27, 5)
            ;

            // 0x104 : editted by hrkim
            Registers.OscillatorDivide.Define(this)
                .WithValueField(0, 3, name: "SOSCDIV1") // hrkim : write 1 -> System OSC Clock Divide 1 by 1
                .WithReservedBits(3, 5)
                .WithValueField(8, 3, name: "SOSCDIV2") // hrkim : write 1 -> System OSC Clock Divide 2 by 1
                .WithReservedBits(11, 21)
            ;

            // 0x108 : editted by hrkim
            Registers.OscillatorConfiguration.Define(this)
                .WithReservedBits(0, 2)
                .WithFlag(2, name: "EREFS") // hrkim : write 1 -> Internal crystal oscillator of OSC selected
                .WithFlag(3, name: "HGO") // hrkim : write 0 -> Configure crystal oscillator for low-gain operation
                .WithValueField(4, 2, name: "RANGE") // hrkim : write 0x11 -> High freq range selected for the crystal oscillator
                .WithReservedBits(8, 24)
            ;                


            // 0x200
            Registers.SlowIRCControlStatus.Define(this, 0x3)
                .WithFlag(0, out slowIRCEnable, name: "SIRCEN") // hrkim : write 1 -> Slow IRC is enable
                .WithFlag(1, name: "SIRCSTEN")
                .WithFlag(2, name: "SIRCLPEN")
                .WithReservedBits(3, 20)
                .WithFlag(23, name: "LK")
                .WithFlag(24, FieldMode.Read, valueProviderCallback: _ => slowIRCEnable.Value, name: "SIRCVLD")
                .WithFlag(25, FieldMode.Read, valueProviderCallback: _ => false, name: "SIRCSEL")
                .WithReservedBits(26, 6)
            ;
            // 0x204
            Registers.SlowIRCDivide.Define(this, 0x101)
                .WithValueField(0, 3, name: "SIRCDIV1") // hrkim : write 1 -> Slow IRC Clock Divide 1 by 1
                .WithReservedBits(3, 5)
                .WithValueField(8, 3, name: "SIRCDIV2") // hrkim : write 1 -> Slow IRC Clock Divide 2 by 1
                .WithReservedBits(11, 21)
            ;
            // 0x208
            Registers.SlowIRCConfiguration.Define(this)
                .WithFlag(0, name: "RANGE") // hrkim : write 1 -> Slow IRC high range clock
                .WithReservedBits(1, 30)
            ;
            // 0x300
            Registers.FastIRCControlStatus.Define(this)
                .WithFlag(0, name: "FIRCEN") // hrkim : write 1 -> Fast IRC is enable
                .WithReservedBits(1, 2)
                .WithFlag(3,  name: "FIRCREGOFF") // hrkim : write 1 -> Fast IRC Regulator is disabled
                .WithReservedBits(4, 19)
                .WithFlag(23, name: "LK")
                .WithFlag(24, FieldMode.Read, valueProviderCallback: _ => true, name: "FIRCVLD") // hrkim : read only
                .WithFlag(25, FieldMode.Read, valueProviderCallback: _ => true, name: "FIRCSEL") // hrkim : read only
                .WithFlag(26, valueProviderCallback: _ => false, name: "FIRCERR") // hrkim : read only
                .WithReservedBits(27, 5)
            ;
            // 0x304 : hrkim
            Registers.FastIRCDivide.Define(this)
                .WithValueField(0, 3, name: "FIRCDIV1") // hrkim : write 1 -> Fast IRC Clock Divde 1 by 1
                .WithReservedBits(3, 5)
                .WithValueField(8, 3, name: "FIRCDIV2") // hrkim : write 1 -> Fast IRC Clock Divde 2 by 2
                .WithReservedBits(11, 21)
            ;

            // 0x308 : hrkim
            Registers.FastIRCConfiguration.Define(this)
                .WithValueField(0, 2, name: "RANGE") // hrkim : write 0 -> Frequency Range - Fast IRC is trimmed to 48MHz
                .WithReservedBits(3, 29)
            ;
                
            // 0x600 : hrkim
            Registers.SystemPLLControlStatus.Define(this)
                .WithFlag(0, name: "SPLLEN") // hrkim : write 1 -> System PLL is enabled
                .WithReservedBits(1, 15)
                .WithFlag(16, name: "SPLLCM") // hrkim : write 1 -> system PLL Clock Monitor is enabled
                .WithFlag(17, valueProviderCallback: _ => false, name: "SPLLCMRE")
                .WithReservedBits(18, 5)
                .WithFlag(23, valueProviderCallback: _ => false, name: "LK")
                .WithFlag(24, valueProviderCallback: _ => true, name: "SPLLVLD")
                .WithFlag(25, valueProviderCallback: _ => false, name: "SPLLSEL")
                .WithFlag(26, valueProviderCallback: _ => false, name: "SPLLERR")
                .WithReservedBits(27, 4)
            ;

            // 0x604 : hrkim
            Registers.SystemPLLDivide.Define(this)
                .WithValueField(0, 3, name: "SPLLDIV1") // hrkim : write 011 -> System PLL Clock Divde 1 by 4
                .WithReservedBits(3, 5)
                .WithValueField(8, 3, name: "SPLLDIV2") // hrkim : write 100 -> System PLL Clock Divide by 8
                .WithReservedBits(11, 21)
            ;
            // 0x608 : hrkim
            Registers.SystemPLLConfiguration.Define(this)
                .WithReservedBits(0, 8)
                .WithValueField(8, 3, name: "PREDIV") // hrkim : write 000 -> PLL reference Clock Divder 1
                .WithReservedBits(11, 5)
                .WithValueField(16, 5, name: "MULT") // hrkim : write 11000 -> Multiply Factor for the System PLL 40
                .WithReservedBits(21, 11)
            ;
        }

        public long Size => 0x1000;

        private readonly IValueRegisterField systemClockSource;
        private readonly IValueRegisterField coreClockRatio;
        private readonly IValueRegisterField busClockRatio;
        private readonly IValueRegisterField slowClockRatio;
        private readonly IFlagRegisterField slowIRCEnable;

        private enum Registers
        {
            VersionId = 0x0,
            Parameter = 0x4,
            ClockStatus = 0x10,
            RunClockControl = 0x14,
            VLPRClockControl = 0x18,
            HSRUNClockControl = 0x1C,
            CLKOUTConfiguration = 0x20,
            OscillatorControlStatus = 0x100,
            OscillatorDivide = 0x104,
            OscillatorConfiguration = 0x108,

            SlowIRCControlStatus = 0x200,
            SlowIRCDivide = 0x204,
            SlowIRCConfiguration = 0x208,

            FastIRCControlStatus = 0x300,
            FastIRCDivide = 0x304,
            FastIRCConfiguration = 0x308,
            SystemPLLControlStatus = 0x600,
            SystemPLLDivide = 0x604,
            SystemPLLConfiguration = 0x608
        }
    }
}
