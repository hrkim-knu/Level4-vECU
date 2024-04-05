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
    // SIM - 
    // This is a stub providing a bare minimum of configuration options along with some reasonable default values.
    // It's not supposed to be understood as a fully-fledged Renode model.
    // Note: the documentation explicitly disallows 8- and 16-bit transfers
    public class S32K148_SIM : BasicDoubleWordPeripheral, IKnownSize
    {

        public S32K148_SIM(IMachine machine) : base(machine)
        {
            // 0x40048004 : CHIPCTL
            Registers.ChipControlregister.Define(this)
                .WithValueField(0, 4, name: "ADC_INTERLEAVE_EN")
                .WithValueField(4, 4, name: "CLKOUTSEL")
                .WithValueField(8, 3, name: "CLKOUTDIV")
                .WithFlag(11, name: "CLKOUTEN")
                .WithFlag(12, name: "TRACECLK_SEL")
                .WithFlag(13, name: "PDB_BB_SEL")
                .WithReservedBits(14, 2)
                .WithValueField(16, 3, name: "ADC_SUPPLY")
                .WithFlag(19, name: "ADC_SUPPLYEN")
                .WithFlag(20, name: "SRAMU_RETEN")
                .WithFlag(21, name: "SRAML_RETEN")
                .WithReservedBits(22, 10)
                ;

            // 0x4004800C : FTMOPT0
            Registers.FTMOptionRegister0.Define(this)
                .WithValueField(0, 3, name: "FTM0FLTxSEL")
                .WithReservedBits(3, 1)
                .WithValueField(4, 3, name: "FTM1FLTxSEL")
                .WithReservedBits(7, 1)
                .WithValueField(8, 3, name: "FTM2FLTxSEL")
                .WithReservedBits(11, 1)
                .WithValueField(12, 3, name: "FTM3FLTxSEL")
                .WithReservedBits(15, 1)
                .WithValueField(16, 2, name: "FTM4CLKSEL")
                .WithValueField(18, 2, name: "FTM5CLKSEL")
                .WithValueField(20, 2, name: "FTM6CLKSEL")
                .WithValueField(22, 2, name: "FTM7CLKSEL")
                .WithValueField(24, 2, name: "FTM0CLKSEL")
                .WithValueField(26, 2, name: "FTM1CLKSEL")
                .WithValueField(28, 2, name: "FTM2CLKSEL")
                .WithValueField(30, 2, name: "FTM3CLKSEL")
                ;   

            // 0x40048010 : LPOCLKS
            Registers.LPOClockSelectRegister.Define(this)
                .WithFlag(0, name: "LPO1KCLKEN")
                .WithFlag(1, name: "LPO32KCLKEN")
                .WithValueField(2, 2, name: "LPOCLKSEL")
                .WithValueField(4, 2, name: "RTCCLKSEL")
                .WithReservedBits(6, 26)
                ;
            // 0x40048018 : ADCOPT
            Registers.ADCOptionsRegister.Define(this)
                .WithFlag(0, name: "ADC0TRGSEL")
                .WithValueField(1, 3, name: "ADC0SWPRETRG")
                .WithValueField(4, 2, name: "ADC0PRETRGSEL")
                .WithReservedBits(6, 2)
                .WithFlag(8, name: "ADC1TRGSEL")
                .WithValueField(9, 3, name: "ADC1SWPRETRG")
                .WithValueField(12, 2, name: "ADC１PRETRGSEL")
                .WithReservedBits(14, 18)
                ;
            // 0x4004801C : FTMOPT1
            Registers.FTMOptionRegister1.Define(this)
                .WithFlag(0, name: "FTM0SYNCBIT")
                .WithFlag(1, name: "FTM1SYNCBIT")
                .WithFlag(2, name: "FTM2SYNCBIT")
                .WithFlag(3, name: "FTM3SYNCBIT")
                .WithValueField(4, 2, name: "FTM1CH0SEL")
                .WithValueField(6, 2, name: "FTM2CH0SEL")
                .WithFlag(8, name: "FTM2CH1SEL")
                .WithReservedBits(9, 2)
                .WithFlag(11, name: "FTM4SYNCBIT")
                .WithFlag(12, name: "FTM5SYNCBIT")
                .WithFlag(13, name: "FTM6SYNCBIT")
                .WithFlag(14, name: "FTM7SYNCBIT")
                .WithFlag(15, name: "FTMGLDOK")
                .WithValueField(16, 8, name: "FTM0_OUTSEL")
                .WithValueField(24, 8, name: "FTM3_OUTSEL")
                ;
            // 0x40048020 : MISCTRL0
            Registers.Miscellaneouscontrolregister0.Define(this)
                .WithReservedBits(0, 9)
                .WithFlag(9, name: "STOP1_MONITOR")
                .WithFlag(10, name: "STOP2_MONITOR")
                .WithReservedBits(11, 3)
                .WithFlag(14, name: "FTM_GTB_SPLIT_EN")
                .WithReservedBits(15, 1)
                .WithFlag(16, name: "FTM0_OBE_CTRL")
                .WithFlag(17, name: "FTM1_OBE_CTRL")
                .WithFlag(18, name: "FTM2_OBE_CTRL")
                .WithFlag(19, name: "FTM3_OBE_CTRL")
                .WithFlag(20, name: "FTM4_OBE_CTRL")
                .WithFlag(21, name: "FTM5_OBE_CTRL")
                .WithFlag(22, name: "FTM6_OBE_CTRL")
                .WithFlag(23, name: "FTM7_OBE_CTRL")
                .WithFlag(24, name: "RMII_CLK_OBE")
                .WithFlag(25, name: "RMII_CLK_SEL")
                .WithFlag(26, name: "QSPI_CLK_SEL")
                .WithReservedBits(27, 5)
                ;
            // 0x40048040 : PLATCGC
            Registers.PlatformClockGatingControl.Define(this)
                .WithFlag(0, name: "CGCMSCM")
                .WithFlag(1, name: "CGCMPU")
                .WithFlag(2, name: "CGCDMA")
                .WithFlag(3, name: "CGCERM")
                .WithFlag(4, name: "CGCEIM")
                .WithReservedBits(5, 27)
                ;
            // 0x4004804C : FCFG1
            Registers.FlashConfigurationRegister1.Define(this)
                .WithReservedBits(0, 12)
                .WithValueField(12, 4, name: "DEPART")
                .WithValueField(16, 4, name: "EEERAMSIZE")
                .WithReservedBits(20, 12)
                ;
            // 0x4004806C : MISCTRL1
            Registers.MiscellaneousControlregister1.Define(this)
                .WithFlag(0, name: "SW_TRG")
                .WithReservedBits(1, 31)
                ;            
            // 0x40048068 : CLKDIV4
            Registers.SystemClockDividerRegister4.Define(this)
                .WithFlag(0, name: "TRACEFRAC")
                .WithValueField(1, 3, name: "TRACEDIV")
                .WithReservedBits(4, 24)
                .WithFlag(28, name: "TRACEDIVEN")
                .WithReservedBits(29, 3)
                ;                 
        }

        public long Size => 0x1000;

        private enum Registers
        {
            ChipControlregister = 0x4,
            FTMOptionRegister0 = 0xC,
            LPOClockSelectRegister = 0x10,
            ADCOptionsRegister = 0x18,
            FTMOptionRegister1 = 0x1C,
            Miscellaneouscontrolregister0 = 0x20,
            SystemDeviceIdentificationRegister = 0x24,
            PlatformClockGatingControl = 0x40,
            FlashConfigurationRegister1 = 0x4C,
            UniqueIdentificationRegisterHigh = 0x54,
            UniqueIdentificationRegisterMidHigh = 0x58,
            UniqueIdentificationRegisterMidLow = 0x5C,
            UniqueIdentificationRegisterLow = 0x60,
            SystemClockDividerRegister4 = 0x68,
            MiscellaneousControlregister1 = 0x6C
        }
    }
}
