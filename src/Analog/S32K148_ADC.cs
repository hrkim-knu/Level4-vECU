//
// Copyright (c) 2010-2023 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Time;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.Timers;

namespace Antmicro.Renode.Peripherals.Analog
{
   // STM32 ADC has many features and only a partial subset are implemented here

   public class S32K148_ADC : BasicDoubleWordPeripheral, IKnownSize
   {
      public S32K148_ADC(IMachine machine) : base(machine)
      {
         // hrkim : 정수 생성, 정수 만큼 ADCChannel 객체 생성
         channels = Enumerable.Range(0, NumberOfChannels).Select(x => new ADCChannel(this, x)).ToArray();
        // hrkim : Each channel has own timer
        samplingTimer = new LimitTimer[NumberOfChannels];
        
        // hrkim : Init isAdcInProgress
        isAdcInProgress = new bool[NumberOfChannels];
         // hrkim : for renode-test
        //  testAdcData = new uint[NumberOfChannels];

         // Sampling time fixed
        //  samplingTimer = new LimitTimer(
        //        machine.ClockSource, 10000000, this, "samplingClock",
        //        limit: 100,
        //        eventEnabled: true,
        //        direction: Direction.Ascending,
        //        enabled: false,
        //        autoUpdate: false,
        //        workMode: WorkMode.OneShot);
        //  samplingTimer.LimitReached += OnConversionFinished;
        for (int i = 0; i < NumberOfChannels; i++)
        {
            var currentIndex = i;
            samplingTimer[currentIndex] = new LimitTimer(
                machine.ClockSource, 40000000, this, $"samplingClock_{i}",
                limit: 100,
                eventEnabled: true,
                direction: Direction.Ascending,
                enabled: false,
                autoUpdate: false,
                workMode: WorkMode.OneShot);
            samplingTimer[currentIndex].LimitReached += () => OnConversionFinished(currentIndex);
        }        

         DefineRegisters();
      }

      // hrkim : for renode-test
//       public void EnableTestMode()
//       {/
        // testMode = true;
//       }

//       public void DisableTestMode()
//       {
        // testMode = false;
//       }

      public void FeedSample(uint value, uint channelIdx, int repeat = 1)
      {
         if(IsValidChannel(channelIdx))
         {
            channels[channelIdx].FeedSample(value, repeat);
        //     this.Log(LogLevel.Info, $"Feed Sample is done in channel: {channelIdx}, value: {value}");            
         }

        // hrkim : for renode-test
        // if(testMode)
        // {
        //    testAdcData[channelIdx] = value;
        // }

        readDone = false;
      }

      public void FeedSample(string path, uint channelIdx, int repeat = 1)
      {
         if(IsValidChannel(channelIdx))
         {
            var parsedSamples = ADCChannel.ParseSamplesFile(path);
            channels[channelIdx].FeedSample(parsedSamples, repeat);
         }
      }
      // hrkim : for renode-test
//       public uint ReadADC(uint channelIdx)
//       {
//         if(testMode)
//         {
//             return testAdcData[channelIdx];
//         }
//         else
//             return channels[channelIdx].GetSample();
//       }

      // hrkim : for renode-test
      public bool IsReadDone()
      {
        this.Log(LogLevel.Debug, "IsReadDone executed, returning: {0}", readDone);

        return readDone;
      }

      private bool IsValidChannel(uint channelIdx)
      {
         if(channelIdx >= NumberOfChannels)
         {
            throw new RecoverableException("Only channels 0/1 are supported");
         }
         return true;
      }

      public override void Reset()
      {
         base.Reset();
         foreach(var c in channels)
         {
            c.Reset();
         }
      }

      public long Size => 0x1000;

      public GPIO IRQ { get; } = new GPIO();
      public GPIO DMARequest { get; } = new GPIO();

      private void DefineRegisters()
      {
            Registers.SC1A.Define(this)
                //     .WithValueField(0, 6, out selectedChannel, name: "ADCH") // hrkim : 1 is used
                    .WithValueField(0, 6, out SC1AselectedChannel, name: "ADCH", writeCallback: (oldValue, newValue) =>
                    {
                        // this.Log(LogLevel.Info, "Writing to ADCH. Old value: {0}, New value: {1}", oldValue, newValue);
                        if(newValue != 0x3F)
                        {
                                EnableADC(newValue);
                        }
                    })
                    .WithTaggedFlag("Interrupt Enable", 6) // hrkim : not used
                    .WithFlag(7, out SC1AconversionComplete, FieldMode.Read, name: "COCO") // hkrim : 1 - Conversion is complete
                    .WithReservedBits(8, 24)
            ;
        //     Registers.SC1D.Define(this)
        //         //     .WithValueField(0, 6, out selectedChannel, name: "ADCH") // hrkim : 1 is used
        //             .WithValueField(0, 6, out SC1DselectedChannel, name: "ADCH", writeCallback: (oldValue, newValue) =>
        //             {
        //                 this.Log(LogLevel.Info, "Writing to ADCH. Old value: {0}, New value: {1}", oldValue, newValue);
        //                 if(newValue != 0x3F)
        //                 {
        //                         EnableADC(newValue);
        //                 }
        //             })
        //             .WithTaggedFlag("Interrupt Enable", 6) // hrkim : not used
        //             .WithFlag(7, out SC1DconversionComplete, FieldMode.Read, name: "COCO") // hkrim : 1 - Conversion is complete
        //             .WithReservedBits(8, 24)
        //     ;            
            Registers.CFG1.Define(this)
                    .WithValueField(0, 2, name: "ADICLK") // hrkim : 0 
                    .WithValueField(2, 2, name: "MODE") // hrkim : 1 - 12-bit conversion
                    .WithReservedBits(4, 1)
                    .WithValueField(5, 2, name: "ADIV") // hrkim : 'divide' will be used to divide input clock
                    .WithReservedBits(7, 1)
                    .WithValueField(8, 1, name: "CLRLTRG") // hrkim : 0
                    .WithReservedBits(9, 23)
            ;
            Registers.CFG2.Define(this)
                    .WithValueField(0, 8, name: "SMPLTS")// hrkim :  1 -> ADC sampling time = 2 ADC clock cycle
                    .WithReservedBits(8, 24) 
            ;
            Registers.RA.Define(this)
                    .WithValueField(0, 12, FieldMode.Read, name: "D", valueProviderCallback: _ =>
                    {
                        // this.Log(LogLevel.Info, "Reading ADC data {0}", adcData);
                        return adcData;
                        })
                    .WithReservedBits(12, 20)
            ;
            Registers.CV1.Define(this)
                    .WithValueField(0, 16, name: "CV") // hrkim : 0
                    .WithReservedBits(16, 16)
            ;
            Registers.CV2.Define(this)
                    .WithValueField(0, 16, name: "CV") // hrkim : 0
                    .WithReservedBits(16, 16)
            ;
            Registers.SC2.Define(this)
                    .WithValueField(0, 2, name: "REFSEL") // hrkim : 0 - Default Voltage Ref Selection
                    .WithFlag(2, name: "DMAEN") // hrkim : 0 - DMA is disabled
                    .WithFlag(3, name: "ACREN") // hrkim : 0
                    .WithFlag(4, name: "ACFGT") // hrkim : 0
                    .WithFlag(5, name: "ACFE") // hrkim : 0 - Compare func is disabled
                    .WithFlag(6, out HwTrigger, name: "ADTRG", // hrkim : 1 - HW trigger
                        writeCallback: (_, value) =>
                        {
                                this.Log(LogLevel.Info, "HwTrigger value written: {0:X}", value);
                                if(value)
                                {
                                        // EnableADC();
                                }
                        })                    
                    .WithFlag(7, name: "ADACT") // hrkim : 0 - Conviersion not in progress
                    .WithReservedBits(8, 5)
                    .WithValueField(13, 2, name: "TRGPRNUM") // hrkim : 0 - Not supported in ADC0
                    .WithReservedBits(15, 1)
                    .WithValueField(16, 4, name: "TRGSTLAT") // hrkim : 0 - Not supported in ADC0
                    .WithReservedBits(20, 4)
                    .WithValueField(24, 4, name: "TRGSTERR") // hrkim : 0 - Not supported in ADC0
                    .WithReservedBits(28, 4)
            ;        
            Registers.SC3.Define(this)
                    .WithValueField(0, 2, name: "AVGS") // hrkim : 0 - 4 samples avg but we insert converged value
                    .WithFlag(2, name: "AVGE") // hrkim : 0 - HW avg function disabled
                    .WithFlag(3, name: "ADCO") // hrkim : 0 - one shot
                    .WithReservedBits(4, 3)
                    .WithFlag(7, name: "CAL") // hrkim : 0 - no calibration
                    .WithReservedBits(8, 24)
            ;
            Registers.BASE_OFS.Define(this)
                    .WithValueField(0, 8, name: "BA_OFS") // hrkim : 0x40
                    .WithReservedBits(8, 24)
            ;
            Registers.OFS.Define(this)
                    .WithValueField(0, 16, name: "OFS") // hrkim : 0xFFFF
                    .WithReservedBits(16, 16)
            ;
            Registers.USR_OFS.Define(this)
                    .WithValueField(0, 8, name: "USR_OFS") // hrkim : 0
                    .WithReservedBits(8, 24)
            ;
            Registers.XOFS.Define(this)
                    .WithValueField(0, 6, name: "XOFS") // hrkim : 0x40
                    .WithReservedBits(6, 26)
            ;
            Registers.YOFS.Define(this)
                    .WithValueField(0, 8, name: "YOFS") // hrkim : 0x37
                    .WithReservedBits(8, 24)
            ;
            Registers.G.Define(this)
                    .WithValueField(0, 11, name: "G") // hrkim : 0x7FF
                    .WithReservedBits(11, 21)
            ;
            Registers.UG.Define(this)
                    .WithValueField(0, 10, name: "UG") // hrkim : 0x4
                    .WithReservedBits(10, 22)
            ;
            Registers.CLPS.Define(this)
                    .WithValueField(0, 7, name: "CLPS")
                    .WithReservedBits(7, 25)
            ;
            Registers.CLP3.Define(this)
                    .WithValueField(0, 10, name: "CLP3")
                    .WithReservedBits(10, 22)
            ;
            Registers.CLP2.Define(this)
                    .WithValueField(0, 10, name: "CLP2")
                    .WithReservedBits(10, 22)
            ;
            Registers.CLP1.Define(this)
                    .WithValueField(0, 9, name: "CLP1")
                    .WithReservedBits(9, 23)
            ;
            Registers.CLP0.Define(this)
                    .WithValueField(0, 8, name: "CLP0")
                    .WithReservedBits(8, 23)
            ;
            Registers.CLPX.Define(this)
                    .WithValueField(0, 7, name: "CLPX")
                    .WithReservedBits(7, 25)
            ;
            Registers.CLP9.Define(this)
                    .WithValueField(0, 7, name: "CLP9")
                    .WithReservedBits(7, 25)
            ;
            Registers.CLPS_OFS.Define(this)
                    .WithValueField(0, 4, name: "CLPS_OFS")
                    .WithReservedBits(4, 28)
            ;
            Registers.CLP3_OFS.Define(this)
                    .WithValueField(0, 4, name: "CLP3_OFS")
                    .WithReservedBits(4, 28)
            ;
            Registers.CLP2_OFS.Define(this)
                    .WithValueField(0, 4, name: "CLP2_OFS")
                    .WithReservedBits(4, 28)
            ;
            Registers.CLP1_OFS.Define(this)
                    .WithValueField(0, 4, name: "CLP1_OFS")
                    .WithReservedBits(4, 28)
            ;
            Registers.CLP0_OFS.Define(this)
                    .WithValueField(0, 4, name: "CLP0_OFS")
                    .WithReservedBits(4, 28)
            ;
            Registers.CLPX_OFS.Define(this)
                    .WithValueField(0, 12, name: "CLPX_OFS")
                    .WithReservedBits(12, 20)
            ;
            Registers.CLP9_OFS.Define(this)
                    .WithValueField(0, 12, name: "CLP9_OFS")
                    .WithReservedBits(12, 20)
            ;
        //     Registers.aSC1A.Define(this)
        //             .WithValueField(0, 6, out selectedChannel, name: "ADCH", writeCallback: (oldValue, newValue) =>
        //             {
        //                 this.Log(LogLevel.Info, "Writing to ADCH. Old value: {0}, New value: {1}", oldValue, newValue);
        //             })
        //             .WithTaggedFlag("Interrupt Enable", 6) // hrkim : not used
        //             .WithFlag(7, out conversionComplete, FieldMode.Read, name: "COCO") // hkrim : 1 - Conversion is complete
        //             .WithReservedBits(8, 24)
        //     ;
            Registers.aSC1A.Define(this)
                        .WithValueField(0, 6, out SC1AselectedChannel, name: "ADCH", writeCallback: (oldValue, newValue) =>
                        {
                                this.Log(LogLevel.Debug, "Writing to ADCH(aSC1A). Old value: {0}, New value: {1}", oldValue, newValue);
                                if(newValue != 0x3F)
                                {
                                        readDone = false;
                                        EnableADC(newValue);
                                }
                        }, valueProviderCallback: _ =>
                        {
                                var value = SC1AselectedChannel.Value;
                                this.Log(LogLevel.Debug, "Reading ADCH(aSC1A). Value: {0}", value);
                                return value;
                        })
                        .WithTaggedFlag("Interrupt Enable", 6)
                        .WithFlag(7, out SC1AconversionComplete, FieldMode.Read, name: "COCO", valueProviderCallback: _ =>
                        {
                                var value = SC1AconversionComplete.Value;
                                this.Log(LogLevel.Debug, "Reading COCO(aSC1A). Value: {0}", value);
                                if(value == true)
                                        readDone = true;
                                return value;
                        })
                        .WithReservedBits(8, 24)
                ;
        //     Registers.aSC1D.Define(this)
        //                 .WithValueField(0, 6, out SC1DselectedChannel, name: "ADCH", writeCallback: (oldValue, newValue) =>
        //                 {
        //                         this.Log(LogLevel.Info, "Writing to ADCH(aSC1D). Old value: {0}, New value: {1}", oldValue, newValue);
        //                         if(newValue != 0x3F)
        //                         {
        //                                 EnableADC(newValue);
        //                         }
        //                 }, valueProviderCallback: _ =>
        //                 {
        //                         var value = SC1DselectedChannel.Value;
        //                         this.Log(LogLevel.Info, "Reading ADCH(aSC1D). Value: {0}", value);
        //                         return value;
        //                 })
        //                 .WithTaggedFlag("Interrupt Enable", 6)
        //                 .WithFlag(7, out SC1DconversionComplete, FieldMode.Read, name: "COCO", valueProviderCallback: _ =>
        //                 {
        //                         var value = SC1DconversionComplete.Value;
        //                         this.Log(LogLevel.Info, "Reading COCO(aSC1D). Value: {0}", value);
        //                         // readDone = true;
        //                         return value;
        //                 })
        //                 .WithReservedBits(8, 24)
        //         ;        
            Registers.aRA.Define(this)
                    .WithValueField(0, 12, FieldMode.Read, name: "D", valueProviderCallback: _ =>
                    {
                        this.Log(LogLevel.Info, "Reading ADC data(aRA) {0}", adcData);
                        var returnData = adcData;
                        adcData = 0;
                        return returnData;
                        })
                    .WithReservedBits(12, 20)
            ;
      }

      private void EnableADC(ulong selectedChannel)
      {
        int channel = (int)selectedChannel;

        // this.Log(LogLevel.Info, "EnableADC executed for channel {0}", channel);
            
            if (isAdcInProgress[channel])
            {
                this.Log(LogLevel.Warning, "ADC is already in progress for channel {0}", channel);
                return;
            }        
          
        isAdcInProgress[channel] = true;
          
        SC1AconversionComplete.Value = false;
         
        
        StartConversion(channel);
      }

      private void StartConversion(int channel)
      {
         if(HwTrigger.Value)
         {
             //this.Log(LogLevel.Debug, "Starting conversion time={0}",
        //      this.Log(LogLevel.Info, "Starting conversion time={0} on channel {1}",
                //    machine.ElapsedVirtualTime.TimeElapsed, channel);

             // Enable timer, which will simulate conversion being performed.
             samplingTimer[channel].Enabled = true;
         }
         else
         {
             this.Log(LogLevel.Warning, "Trying to start conversion while ADC off");
         }
      }

      private void OnConversionFinished(int channel)
      {
        
        //  this.Log(LogLevel.Debug, "OnConversionFinished: time={0} channel={1}",
        // hrkim
        //   this.Log(LogLevel.Info, "OnConversionFinished: time={0} channel={1}",
        //        machine.ElapsedVirtualTime.TimeElapsed,
        //        channel);

         channels[channel].PrepareSample();
         adcData = channels[channel].GetSample();
        //  this.Log(LogLevel.Info, "adcData: {0}, {1}",
        //        adcData, channels[channel].GetSample());

        SC1AconversionComplete.Value = true;
        
         isAdcInProgress[channel] = false;
      }

      // Sampling timer. Provides time-based event for driving conversion of
      // regular channel sequence.
      private readonly LimitTimer[] samplingTimer;

      // private uint currentChannelIdx;
      //private ADCChannel currentChannel;
      private readonly ADCChannel[] channels;
      // hrkim
      private IValueRegisterField SC1AselectedChannel;
//       private IValueRegisterField SC1DselectedChannel;
      private IFlagRegisterField HwTrigger; // 1 : HW trigger
      private IFlagRegisterField SC1AconversionComplete;
//       private IFlagRegisterField SC1DconversionComplete;
      // Data sample to be returned from data register when read.
      private uint adcData;
//       private bool testMode = false;
//       private uint[] testAdcData;
      private bool readDone = false;
      public const int NumberOfChannels = 16;
      private bool[] isAdcInProgress;

      private enum Registers
      {
            SC1A = 0x0, // ADC Status and Control Register 1
        //     SC1D = 0x10,
            CFG1 = 0x40,
            CFG2 = 0x44,
            RA = 0x48,
            CV1 = 0x88,
            CV2 = 0x8C,
            SC2 = 0x90,
            SC3 = 0x94,
            BASE_OFS = 0x98,
            OFS = 0x9C,
            USR_OFS = 0xA0,
            XOFS = 0xA4,
            YOFS = 0xA8,
            G = 0xAC,
            UG = 0xB0,
            CLPS = 0xB4,
            CLP3 = 0xB8,
            CLP2 = 0xBC,
            CLP1 = 0xC0,
            CLP0 = 0xC4,
            CLPX = 0xC8,
            CLP9 = 0xCC,
            CLPS_OFS = 0xD0,
            CLP3_OFS = 0xD4,
            CLP2_OFS = 0xD8,
            CLP1_OFS = 0xDC,
            CLP0_OFS = 0xE0,
            CLPX_OFS = 0xE4,
            CLP9_OFS = 0xE8,
            aSC1A = 0x108,
        //     aSC1D = 0x118,
            aRA = 0x188
      }
   }
}
