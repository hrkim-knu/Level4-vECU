//
// Copyright (c) 2010-2023 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Time;

namespace Antmicro.Renode.Peripherals.Timers
{
    public class S32K148_FTM : BasicDoubleWordPeripheral, IKnownSize
    {
        public S32K148_FTM(IMachine machine, long frequency) : base(machine)
        {
            // divider : SC->PS에서 읽어와야할듯
            // compare : MOD에서 읽어와야할듯
            innerTimer = new ComparingTimer(machine.ClockSource, frequency, this, "ftm", limit: 0xFFFF, direction: Direction.Ascending,
                enabled: false, eventEnabled: true, workMode: WorkMode.Periodic, compare: 0xFFFF, divider: 1);

            innerTimer.CompareReached += CompareReached;

            IRQ = new GPIO();

            DefineRegisters();
            // this.Log(LogLevel.Info, $"S32K_FTM constructor called - Frequency: {frequency}, Compare: { innerTimer.Compare}, Divider: {innerTimer.Divider}");
        }

        public override void Reset()
        {
            base.Reset();
            innerTimer.Reset();
            // prescaleValue = 0;
            // prescalerBypass = false;
            // compare = 0;
            // latchedTimerValue = 0;
            // hrkim
            // UpdateCount(); 
            this.Log(LogLevel.Info, $"Reset called");
            UpdateInterrupt();
        }

        public GPIO IRQ { get; }

        public long Size => 0x1000;

        private void UpdateInterrupt()
        {
            updateInterruptCallCount++; // for debug

            var value = true;
            value &= overflowFlag.Value;
            value &= interruptEnable.Value;
            this.Log(LogLevel.Info, $"UpdateInterrupt called - value: {value}, overflowFlag: {overflowFlag.Value}, interruptEnable: {interruptEnable.Value}");
            this.Log(LogLevel.Info, $"updateInterruptCallCount : {updateInterruptCallCount}");
            IRQ.Set(value);
        }

        private void CompareReached()
        {
            if(innerTimer.Compare != 0)
            {
                overflowFlag.Value = true;
                this.Log(LogLevel.Info, $"CompareReached called - overflowFlag: {overflowFlag.Value}, innerTimer.Value set to: {innerTimer.Value}");
                // innerTimer.Value = initialCount;
                UpdateInterrupt();
            }
            else
                this.Log(LogLevel.Info, $" Why CompareReached called? - overflowFlag: {overflowFlag.Value}, innerTimer.Value set to: {innerTimer.Value}");
        }

        private void UpdateDivider()
        {
            innerTimer.Divider = (uint)System.Math.Pow(2, prescaleValue);
            // innerTimer.Divider = 1;
            this.Log(LogLevel.Info, $"UpdateDivder called - innerTimer.Divider: {innerTimer.Divider}");

        }
        
        private uint LogAndReturnCNT()
        {
            uint value = (uint)innerTimer.Value;
            this.Log(LogLevel.Info, $"CNT Read - innerTimer.Value: {value}");
            return value;
        }

        private uint LogAndReturnMOD()
        {
            uint value = (uint)innerTimer.Compare;
            this.Log(LogLevel.Info, $"MOD Read - innerTimer.Compare: {value}");
            return value;
        }

        private uint LogAndReturnCNTIN()
        {
            uint value = (uint)initialCount;
            this.Log(LogLevel.Info, $"CNTIN Read - initialCount: {value}");
            return value;
        }

        private bool LogAndReturnTOF()
        {
            bool value = overflowFlag.Value;
            this.Log(LogLevel.Info, $"TOF Read - overflowFlag.Value: {value}");
            this.Log(LogLevel.Info, $"CNT value in TOF Read - innerTimer.Value: {innerTimer.Value}");
            this.Log(LogLevel.Info, $"MOD value in TOF Read - innerTimer.Compare: {innerTimer.Compare}");            
            return value;
        }

        private void DefineRegisters()
        {

            // hrkim : SC (offset : 0x00)
            Registers.StatusAndControl.Define(this)
                .WithValueField(0, 3, name: "PS", changeCallback: (_, value) =>
                {
                    prescaleValue = (uint)value;
                }, valueProviderCallback: _ => prescaleValue) // hrkim : write 110 -> Divde by 64
                // hrkim : Not define detail operation
                .WithValueField(3, 2, name: "CLKS", changeCallback: (_, value) =>
                {
                    if(value == 0)
                        innerTimer.Enabled = false;
                    else if(value == 1)
                        innerTimer.Enabled = true;
                }) // hrkim : write 01 -> FTM input clock
                .WithTaggedFlag("CPWLS", 5)
                .WithTaggedFlag("RIE", 6)
                .WithFlag(7, out reloadFlag, FieldMode.Read | FieldMode.WriteZeroToClear, name: "RF")
                .WithFlag(8, out interruptEnable, name: "TOIE")
                // .WithFlag(9, out overflowFlag, FieldMode.Read | FieldMode.WriteZeroToClear, name: "TOF")
                .WithFlag(9, out overflowFlag,  name: "TOF", writeCallback: (_, value) =>
                {
                    if (value == false)
                    {
                        overflowFlag.Value = false;
                        this.Log(LogLevel.Info, $"TOF Write : {overflowFlag.Value}");
                        UpdateInterrupt();
                        // IRQ.Unset();
                    }
                },
                valueProviderCallback: _ => LogAndReturnTOF())
                .WithReservedBits(10, 6)
                .WithTag("PWMENn", 16, 8)
                .WithTag("FLTPS", 24, 4)
                .WithReservedBits(28, 4)
                .WithWriteCallback((_, __) => UpdateDivider())
            ;

            // hrkim : CNT (offset : 0x04)
            Registers.Counter.Define(this)
                .WithValueField(0, 16, name: "CNT", writeCallback: (_, value) =>
                {                   
                    innerTimer.Value = initialCount;
                    this.Log(LogLevel.Info, $"CNT write - innerTimer.Value: {innerTimer.Value}");
                // }, valueProviderCallback: _ => innerTimer.Value)
                }, valueProviderCallback: _ => LogAndReturnCNT())
                .WithReservedBits(16, 16)
            ;


            // hrkim : MOD (offset : 0x08)
            Registers.Modulo.Define(this)
                .WithValueField(0, 16, name: "MOD", changeCallback: (_, value) =>
                {
                    if(((uint)value > (uint)0xFFFF))
                    {
                        this.Log(LogLevel.Warning, $"MOD is 16 bits register (write value : {value})");
                    //     return;
                    }
                    innerTimer.Compare = (uint)value;
                    this.Log(LogLevel.Info, $"MOD write - innerTimer.Compare: {innerTimer.Compare}");
                // }, valueProviderCallback: _ => innerTimer.Compare)
                }, valueProviderCallback: _ => LogAndReturnMOD())
                .WithReservedBits(16, 16)
            ;

            // hrkim : CNTIN (offset : 0x4C)
            Registers.CounterInitialValue.Define(this)
                .WithValueField(0, 16, name: "INIT", changeCallback: (_, value) =>
                {
                    initialCount = (uint)value;
                    // innerTimer.Value = initialCount;
                    this.Log(LogLevel.Info, $"CNTIN write - initialCount: {initialCount}");
                // }, valueProviderCallback: _ => initialCount)
                }, valueProviderCallback: _ => LogAndReturnCNTIN())
                // .WithWriteCallback((_, __) => UpdateCount())
                .WithReservedBits(16, 16)
            ;

            // hrkim : MODE (offset : 0x54)
            Registers.FeaturesModeSelection.Define(this)
                .WithFlag(0, out enabled, name: "FTMEN", changeCallback: (_, value) =>
                    {
                        innerTimer.Enabled = value;
                        if(!value)
                        {
                            innerTimer.Value = 0;
                            // compareFlag.Value = false;
                        }
                        this.Log(LogLevel.Info, $"FTMEN write - innerTimer.Enabled: {innerTimer.Enabled}");
                    })
                .WithTaggedFlag("INIT", 1)
                // .WithFlag(1, FieldMode.Write, name: "INIT", writeCallback: (_, value) =>
                // {
                //     if(value)
                //     {
                //         // OUTINIT 레지스터의 상태에 따라 채널 출력을 초기화하는 로직 구현
                //         // InitializeChannelsOutput();
                //     }   
                // }) 
                // WPEN 명령도 구현해야함
                .WithFlag(2, out wpDisable, name: "WPDIS", writeCallback: (_, value) =>
                {
                    if(wpEnable.Value)
                    {
                        wpDisable.Value = value;
                        wpEnable.Value = false;
                    }
                    
                })
                .WithTaggedFlag("PWMSYNC", 3)
                .WithTaggedFlag("CAPTEST", 4)
                .WithTag("FAULTM", 5, 2)
                .WithTaggedFlag("FAULTE", 7)
                .WithReservedBits(8, 24)
            ;

            // hrkim : FMS
            Registers.FaultModeStatus.Define(this)
            .WithTaggedFlag("FAULTF0", 0)
            .WithTaggedFlag("FAULTF1", 1)
            .WithTaggedFlag("FAULTF2", 2)
            .WithTaggedFlag("FAULTF3", 3)
            .WithReservedBits(4, 1)
            .WithTaggedFlag("FAULTIN", 5)
            .WithFlag(6, out wpEnable, name: "WPEN", writeCallback: (_, value) =>
            {
                if(value)
                {
                    wpEnable.Value = value;
                    wpDisable.Value = false;
                }

            })
            ;

        }
        // hrkim : for debug
        public byte ReadByte(long offset)
        {
            var doubleWordValue = ReadDoubleWord(offset & ~0x3);
            var shiftAmount = (int)(offset % 4) * 8;

            return (byte)((doubleWordValue >> shiftAmount) & 0xFF);
        }


        // hrkim
        private IFlagRegisterField reloadFlag;
        private IFlagRegisterField overflowFlag;
        private IFlagRegisterField interruptEnable;
        private IFlagRegisterField wpEnable;
        private IFlagRegisterField wpDisable;
        private IFlagRegisterField enabled;
        private uint prescaleValue;
        // private uint moduloValue;
        private uint initialCount;
        private static int updateInterruptCallCount = 0; // for debug

        // private uint compare;
        // private uint latchedTimerValue;
        // private bool prescalerBypass;
        // private uint prescaleValue;

        private readonly ComparingTimer innerTimer;

        private enum Registers
        {
            StatusAndControl = 0x0,
            Counter = 0x4,
            Modulo = 0x8,
            Channel0StatusAndControl = 0xC,
            Channel0Value = 0x10,
            Channel1StatusAndControl = 0x14,
            Channel1Value = 0x18,
            Channel2StatusAndControl = 0x1C,
            Channel2Value = 0x20,
            Channel3StatusAndControl = 0x24,
            Channel3Value = 0x28,
            Channel4StatusAndControl = 0x2C,
            Channel4Value = 0x30,
            Channel5StatusAndControl = 0x34,
            Channel5Value = 0x38,
            Channel6StatusAndControl = 0x3C,
            Channel6Value = 0x40,
            Channel7StatusAndControl = 0x44,
            Channel7Value = 0x48,
            CounterInitialValue = 0x4C,
            CaptureAndCompareStatus = 0x50,
            FeaturesModeSelection = 0x54,
            Synchronization = 0x58,
            InitialStateForChannelsOutput = 0x5C,
            OutputMask = 0x60,
            FunctionForLinkedChannels = 0x64,
            DeadtimeConfiguration = 0x68,
            FTMExternalTrigger = 0x6C,
            ChannelPolarity = 0x70,
            FaultModeStatus = 0x74,
            InputCaptureFilterControl = 0x78,
            FaultControl = 0x7C,
            QuadratureDecoderControlAndStatus = 0x80,
            Configuration = 0x84,
            FTMFaultInputPolarity = 0x88,
            SynchronizationConfiguration = 0x8C,
            FTMInvertingControl = 0x90,
            FTMSoftwareOutputContorl = 0x94,
            FTMPWMLoad = 0x98,
            HalfCycleRegister = 0x9C,
            Pair0DeadtimeConfiguration = 0xA0,
            Pair1DeadtimeConfiguration = 0xA8,
            Pair2DeadtimeConfiguration = 0xB0,
            Pair3DeadtimeConfiguration = 0xB8,
            MirrorofModuloValue = 0x200,
            MirrorofChannel0MatchValue = 0x204,
            MirrorofChannel1MatchValue = 0x208,
            MirrorofChannel2MatchValue = 0x20C,
            MirrorofChannel3MatchValue = 0x210,
            MirrorofChannel4MatchValue = 0x214,
            MirrorofChannel5MatchValue = 0x218,
            MirrorofChannel6MatchValue = 0x21C,
            MirrorofChannel7MatchValue = 0x220,

        }
    }
}
