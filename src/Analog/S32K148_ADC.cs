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
   //
   // Supported:
   // * Software triggered conversion
   // * Single conversion
   // * Scan mode with regular group
   // * Continuous conversion
   // * Modes of use
   //   - Polling (read EOC status flag)
   //   - Interrupt (enable ADC interrupt for EOC)
   //   - DMA (enable DMA and configure stream for peripheral to memory transfer)
   //
   // Not Implemented:
   // * Analog watchdog
   // * Overrun detection
   // * External triggers
   // * Injected channels
   // * Sampling time (time is fixed)
   // * Discontinuous mode
   // * Multi-ADC (i.e. Dual/Triple) mode
   public class STM32_ADC : BasicDoubleWordPeripheral, IKnownSize
   {
      public STM32_ADC(IMachine machine) : base(machine)
      {
         // hrkim : 정수 생성, 정수 만큼 ADCChannel 객체 생성
         channels = Enumerable.Range(0, NumberOfChannels).Select(x => new ADCChannel(this, x)).ToArray();

         DefineRegisters();

         // Sampling time fixed
         samplingTimer = new LimitTimer(
               machine.ClockSource, 1000000, this, "samplingClock",
               limit: 100,
               eventEnabled: true,
               direction: Direction.Ascending,
               enabled: false,
               autoUpdate: false,
               workMode: WorkMode.OneShot);
         samplingTimer.LimitReached += OnConversionFinished;
      }

      public void FeedSample(uint value, uint channelIdx, int repeat = 1)
      {
         if(IsValidChannel(channelIdx))
         {
            channels[channelIdx].FeedSample(value, repeat);
         }
      }

      public void FeedSample(string path, uint channelIdx, int repeat = 1)
      {
         if(IsValidChannel(channelIdx))
         {
            var parsedSamples = ADCChannel.ParseSamplesFile(path);
            channels[channelIdx].FeedSample(parsedSamples, repeat);
         }
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
                    .WithValueField(0, 6, out selectedChannel, name: "ADCH",
                        changeCallback: (_, value) => { if(value) { EnableADC(); }}) // hrkim : 1 is used
                    .WithTaggedFlag("Interrupt Enable", 6) // hrkim : not used
                    .WithFlag(7, out coco, name: "COCO") // hkrim : 1 - Conversion is complete
                    .WithReservedBits(8, 24)
            ;
            Register.CFG1.Define(this)
                    .WithValueField(0, 2, name: "ADICLK") // hrkim : 0 
                    .WithValueField(2, 2, name: "MODE") // hrkim : 1 - 12-bit conversion
                    .WithReservedBits(4, 1)
                    .WithValueField(5, 2, name: "ADIV") // hrkim : 'divide' will be used to divide input clock
                    .WithReservedBits(7, 1)
                    .WithValueField(8, 1, name: "CLRLTRG") // hrkim : 0
                    .WithReservedBits(9, 23)
            ;
            Register.CFG2.Define(this)
                    .WithValueField(0, 8, name: "SMPLTS", writeCallback: (_, value) => // hrkim :  1 -> ADC sampling time = 2 ADC clock cycle
                    {
                        samplingClock = value + 1;
                    })
                    .WithReservedBits(8, 24) 
            ;
            Register.RA.Define(this)
                    .WithValueField(0, 12, FieldMode.Read, name: "D", valueProviderCallback: _ => adcData)
                    .WithReservedBits(12, 20)
            ;
            Register.CV1.Define(this)
                    .WithValueField(0, 16, name: "CV") // hrkim : 0
                    .WithReservedBits(16, 16)
            ;
            Register.CV2.Define(this)
                    .WithValueField(0, 16, name: "CV") // hrkim : 0
                    .WithReservedBits(16, 16)
            ;
            Register.SC2.Define(this)
                    .WithValueField(0, 2, name: "REFSEL") // hrkim : 0 - Default Voltage Ref Selection
                    .WithFlag(2, name: "DMAEN") // hrkim : 0 - DMA is disabled
                    .WithFlag(3, name: "ACREN") // hrkim : 0
                    .WithFlag(4, name: "ACFGT") // hrkim : 0
                    .WithFlag(5, name: "ACFE") // hrkim : 0 - Compare func is disabled
                    .WithFlag(6, out HwTrigger name: "ADTRG") // hrkim : 1 - HW trigger
                    .WithFlag(7, name: "ADACT") // hrkim : 0 - Conviersion not in progress
                    .WithReservedBits(8, 5);
                    .WithValueField(13, 2, name: "TRGPRNUM") // hrkim : 0 - Not supported in ADC0
                    .WithReservedBits(15, 1);
                    .WithValueField(16, 4, name: "TRGSTLAT") // hrkim : 0 - Not supported in ADC0
                    .WithReservedBits(20, 4);
                    .WithValueField(27, 4, name: "TRGSTERR") // hrkim : 0 - Not supported in ADC0
                    .WithReservedBits(28, 4);
            ;
            Register.SC3.Define(this)
                    .WithValueField(0, 2, name: "AVGS") // hrkim : 0 - 4 samples avg but we insert converged value
                    .WithFlag(2, name: "AVGE") // hrkim : 0 - HW avg function disabled
                    .WithFlag(3, name: "ADCO") // hrkim : 0 - one shot
                    .WithReservedBits(4, 3);
                    .WithFlag(7, name: "CAL") // hrkim : 0 - no calibration
                    .WithReservedBits(8, 24)
            ;
            Register.BASE_OFS.Define(this)
                    .WithValueField(0, 8, name: "BA_OFS") // hrkim : 0x40
                    .WithReservedBits(8, 24)
            ;
            Register.OFS.Define(this)
                    .WithValueField(0, 16, name: "OFS") // hrkim : 0xFFFF
                    .WithReservedBits(17, 16)
            ;
            Register.USR_OFS.Define(this)
                    .WithValueField(0, 8, name: "USR_OFS") // hrkim : 0
                    .WithReservedBits(8, 24)
            ;
            Register.XOFS.Define(this)
                    .WithValueField(0, 6, name: "XOFS") // hrkim : 0x40
                    .WithReservedBits(6, 26)
            ;
            Register.YOFS.Define(this)
                    .WithValueField(0, 8, name: "YOFS") // hrkim : 0x37
                    .WithReservedBits(8, 24)
            ;
            Register.G.Define(this)
                    .WithValueField(0, 10, name: "G") // hrkim : 0x7FF
                    .WithReservedBits(11, 21)
            ;
            Register.UG.Define(this)
                    .WithValueField(0, 10, name: "UG") // hrkim : 0x4
                    .WithResvedBits(10, 22)
            ;
            Register.CLPS.Define(this)
                    .WithValueField(0, 7, name: "CLPS")
                    .WithReservedBits(7, 25)
            ;
            Register.CLP3.Define(this)
                    .WithValueField(0, 10, name: "CLP3")
                    .WithReservedBits(10, 22)
            ;
            Register.CLP2.Define(this)
                    .WithValueField(0, 10, name: "CLP2")
                    .WithReservedBits(10, 22)
            ;
            Register.CLP1.Define(this)
                    .WithValueField(0, 9, name: "CLP1")
                    .WithReservedBits(9, 23)
            ;
            Register.CLP0.Define(this)
                    .WithValueField(0, 8, name: "CLP0")
                    .WithReservedBits(8, 23)
            ;
            Register.CLPX.Define(this)
                    .WithValueField(0, 7, name: "CLPX")
                    .WithReservedBits(7, 25)
            ;
            Register.CLP9.Define(this)
                    .WithValueField(0, 7, name: "CLP9")
                    .WithReservedBits(7, 25)
            ;
            Register.CLPS_OFS.Define(this)
                    .WithValueField(0, 4, name: "CLPS_OFS")
                    .WithReservedBits(4, 28)
            ;
            Register.CLP3_OFS.Define(this)
                    .WithValueField(0, 4, name: "CLP3_OFS")
                    .WithReservedBits(4, 28)
            ;
            Register.CLP2_OFS.Define(this)
                    .WithValueField(0, 4, name: "CLP2_OFS")
                    .WithReservedBits(4, 28)
            ;
            Register.CLP1_OFS.Define(this)
                    .WithValueField(0, 4, name: "CLP1_OFS")
                    .WithReservedBits(4, 28)
            ;
            Register.CLP0_OFS.Define(this)
                    .WithValueField(0, 4, name: "CLP0_OFS")
                    .WithReservedBits(4, 28)
            ;
            Register.CLPX_OFS.Define(this)
                    .WithValueField(0, 12, name: "CLPX_OFS")
                    .WithReservedBits(12, 20)
            ;
            Register.CLP9_OFS.Define(this)
                    .WithValueField(0, 12, name: "CLP9_OFS")
                    .WithReservedBits(12, 20)
            ;
            Registers.aSC1A.Define(this)
                    .WithValueField(0, 6, name: "ADCH") // hrkim : 4 -> 1
                    .WithFlag(6, name: "AIEN") // hrkim : not used
                    .WithFlag(7, nmae: "COCO") // hkrim : 1 - Conversion is complete
                    .WithReservedBits(8, 24)
            ;
            Register.aRA.Define(this)
                    .WithValueField(0, 12, name: "D")
                    .WithReservedBits(12, 20)            
            ;
         // Registers.Status.Define(this)
         //    .WithTaggedFlag("Analog watchdog flag", 0)
         //    .WithFlag(1, out endOfConversion, name: "Regular channel end of conversion")
         //    .WithTaggedFlag("Injected channel end of conversion", 2)
         //    .WithTaggedFlag("Injected channel start flag", 3)
         //    .WithTaggedFlag("Regular channel start flag", 4)
         //    .WithTaggedFlag("Overrun", 5)
         //    .WithReservedBits(6, 26);

         // Registers.Control1.Define(this)
         //    .WithTag("Analog watchdog channel select bits", 0, 5)
         //    .WithFlag(5, out eocInterruptEnable, name: "Interrupt enable for EOC")
         //    .WithTaggedFlag("Analog watchdog interrupt enable", 6)
         //    .WithTaggedFlag("Interrupt enable for injected channels", 7)
         //    .WithFlag(8, out scanMode, name: "Scan mode")
         //    .WithTaggedFlag("Enable the watchdog on a single channel in scan mode", 9)
         //    .WithTaggedFlag("Automatic injected group conversion", 10)
         //    .WithTaggedFlag("Discontinuous mode on regular channels", 11)
         //    .WithTaggedFlag("Discontinuous mode on injected channels", 12)
         //    .WithTag("Discontinuous mode channel count", 13, 3)
         //    .WithReservedBits(16, 6)
         //    .WithTaggedFlag("Analog watchdog enable on injected channels", 22)
         //    .WithTaggedFlag("Analog watchdog enable on regular channels", 23)
         //    .WithTag("Resolution", 24, 2)
         //    .WithTaggedFlag("Overrun interrupt enable", 26)
         //    .WithReservedBits(27, 5);

         // Registers.Control2.Define(this, name: "Control2")
         //    .WithFlag(0, out adcOn,
         //          name: "A/D Converter ON/OFF",
         //          changeCallback: (_, val) => { if(val) { EnableADC(); }})
         //    .WithFlag(1, out continuousConversion, name: "Continous conversion")
         //    .WithReservedBits(2, 6)
         //    .WithFlag(8, out dmaEnabled, name: "Direct memory access mode")
         //    .WithFlag(9, out dmaIssueRequest, name: "DMA disable selection")
         //    .WithFlag(10, out endOfConversionSelect, name: "End of conversion select")
         //    .WithTaggedFlag("Data Alignment", 11)
         //    .WithReservedBits(12, 4)
         //    .WithTag("External event select for injected group", 16, 4)
         //    .WithTag("External trigger enable for injected channels", 20, 2)
         //    .WithTaggedFlag("Start conversion of injected channels", 22)
         //    .WithReservedBits(23, 1)
         //    .WithTag("External event select for regular group", 24, 4)
         //    .WithTag("External trigger enable for regular channels", 28, 2)
         //    .WithFlag(30,
         //          name: "Start Conversion Of Regular Channels",
         //          writeCallback: (_, value) => { if(value) StartConversion(); },
         //          valueProviderCallback: _ => false)
         //    .WithReservedBits(31, 1);

         // Registers.SampleTime1.Define(this)
         //    .WithTag("Channel 10 sampling time", 0, 3)
         //    .WithTag("Channel 11 sampling time", 3, 3)
         //    .WithTag("Channel 12 sampling time", 6, 3)
         //    .WithTag("Channel 13 sampling time", 9, 3)
         //    .WithTag("Channel 14 sampling time", 12, 3)
         //    .WithTag("Channel 15 sampling time", 15, 3)
         //    .WithTag("Channel 16 sampling time", 18, 3)
         //    .WithTag("Channel 17 sampling time", 21, 3)
         //    .WithTag("Channel 18 sampling time", 24, 3)
         //    .WithReservedBits(27, 5);

         // Registers.SampleTime2.Define(this)
         //    .WithTag("Channel 0 sampling time", 0, 3)
         //    .WithTag("Channel 1 sampling time", 3, 3)
         //    .WithTag("Channel 2 sampling time", 6, 3)
         //    .WithTag("Channel 3 sampling time", 9, 3)
         //    .WithTag("Channel 4 sampling time", 12, 3)
         //    .WithTag("Channel 5 sampling time", 15, 3)
         //    .WithTag("Channel 6 sampling time", 18, 3)
         //    .WithTag("Channel 7 sampling time", 21, 3)
         //    .WithTag("Channel 8 sampling time", 24, 3)
         //    .WithTag("Channel 9 sampling time", 27, 3)
         //    .WithReservedBits(30, 2);

         // Registers.InjectedChannelDataOffset1.Define(this)
         //    .WithTag("Data offset for injected channel 1", 0, 12)
         //    .WithReservedBits(12, 20);
         // Registers.InjectedChannelDataOffset2.Define(this)
         //    .WithTag("Data offset for injected channel 2", 0, 12)
         //    .WithReservedBits(12, 20);
         // Registers.InjectedChannelDataOffset3.Define(this)
         //    .WithTag("Data offset for injected channel 3", 0, 12)
         //    .WithReservedBits(12, 20);
         // Registers.InjectedChannelDataOffset4.Define(this)
         //    .WithTag("Data offset for injected channel 4", 0, 12)
         //    .WithReservedBits(12, 20);

         // Registers.RegularSequence1.Define(this)
         //    .WithValueField(0, 5, out regularSequence[12], name: "13th conversion in regular sequence")
         //    .WithValueField(5, 5, out regularSequence[13], name: "14th conversion in regular sequence")
         //    .WithValueField(10, 5, out regularSequence[14], name: "15th conversion in regular sequence")
         //    .WithValueField(15, 5, out regularSequence[15], name: "16th conversion in regular sequence")
         //    .WithValueField(20, 4, writeCallback: (_, val) => { regularSequenceLen = (uint)val + 1; }, name: "Regular channel sequence length");

         // Registers.RegularSequence2.Define(this)
         //    .WithValueField(0, 5, out regularSequence[6], name: "7th conversion in regular sequence")
         //    .WithValueField(5, 5, out regularSequence[7], name: "8th conversion in regular sequence")
         //    .WithValueField(10, 5, out regularSequence[8], name: "9th conversion in regular sequence")
         //    .WithValueField(15, 5, out regularSequence[9], name: "10th conversion in regular sequence")
         //    .WithValueField(20, 5, out regularSequence[10], name: "11th conversion in regular sequence")
         //    .WithValueField(25, 5, out regularSequence[11], name: "12th conversion in regular sequence");

         // Registers.RegularSequence3.Define(this)
         //    .WithValueField(0, 5, out regularSequence[0], name: "1st conversion in regular sequence")
         //    .WithValueField(5, 5, out regularSequence[1], name: "2nd conversion in regular sequence")
         //    .WithValueField(10, 5, out regularSequence[2], name: "3rd conversion in regular sequence")
         //    .WithValueField(15, 5, out regularSequence[3], name: "4th conversion in regular sequence")
         //    .WithValueField(20, 5, out regularSequence[4], name: "5th conversion in regular sequence")
         //    .WithValueField(25, 5, out regularSequence[5], name: "6th conversion in regular sequence");

         // // Data register
         // Registers.RegularData.Define(this)
         //    .WithValueField(0, 32,
         //          valueProviderCallback: _ =>
         //          {
         //              this.Log(LogLevel.Debug, "Reading ADC data {0}", adcData);
         //              // Reading ADC_DR should clear EOC
         //              endOfConversion.Value = false;
         //              IRQ.Set(false);
         //              return adcData;
         //          });
      }

      private void EnableADC()
      {
         //  currentChannel = channels[regularSequence[currentChannelIdx].Value];
          currentChannel = channels[selectedChannel.Value];
          StartConversion();
      }

      private void StartConversion()
      {
         if(HwTrigger.Value)
         {
             this.Log(LogLevel.Debug, "Starting conversion time={0}",
                   machine.ElapsedVirtualTime.TimeElapsed);

             // Enable timer, which will simulate conversion being performed.
             samplingTimer.Enabled = true;
         }
         else
         {
             this.Log(LogLevel.Warning, "Trying to start conversion while ADC off");
         }
      }

      private void OnConversionFinished()
      {
         this.Log(LogLevel.Debug, "OnConversionFinished: time={0} channel={1}",
               machine.ElapsedVirtualTime.TimeElapsed,
               selectedChannel.Value);

         // Set data register and trigger DMA request
         currentChannel.PrepareSample();
         adcData = currentChannel.GetSample();
         // if(dmaEnabled.Value && dmaIssueRequest.Value)
         // {
         //    // Issue DMA peripheral request, which when mapped to DMA
         //    // controller will trigger a peripheral to memory transfer
         //    DMARequest.Set();
         //    DMARequest.Unset();
         // }

         // var scanModeActive = scanMode.Value && currentChannelIdx < regularSequenceLen - 1;
         // var scanModeFinished = scanMode.Value && currentChannelIdx == regularSequenceLen - 1;

         // Signal EOC if EOCS set with scan mode enabled and finished or we finished scanning regular group
         // endOfConversion.Value = scanModeActive ? (endOfConversionSelect.Value || scanModeFinished) : true;

         // Iterate to next channel
         // currentChannelIdx = (currentChannelIdx + 1) % regularSequenceLen;
         // currentChannel = channels[regularSequence[currentChannelIdx].Value];

         // Auto trigger next conversion if we're scanning or CONT bit set
         // samplingTimer.Enabled = scanModeActive || continuousConversion.Value;

         // Trigger EOC interrupt
         // if(endOfConversion.Value && eocInterruptEnable.Value)
         // {
         //    this.Log(LogLevel.Debug, "OnConversionFinished: Set IRQ");
         //    IRQ.Set(true);
         // }
      }

      // Control 1/2 fields
      // private IFlagRegisterField scanMode;
      // private IFlagRegisterField endOfConversion;
      // private IFlagRegisterField adcOn;
      // private IFlagRegisterField endOfConversionSelect;
      // private IFlagRegisterField eocInterruptEnable;
      // private IFlagRegisterField continuousConversion;

      // private IFlagRegisterField dmaEnabled;
      // private IFlagRegisterField dmaIssueRequest;

      // Sampling timer. Provides time-based event for driving conversion of
      // regular channel sequence.
      private readonly LimitTimer samplingTimer;

      // Data sample to be returned from data register when read.
      // private uint adcData;

      // Regular sequence settings, i.e. the channels and order of channels
      // for performing conversion
      // private uint regularSequenceLen;
      // private readonly IValueRegisterField[] regularSequence = new IValueRegisterField[19];

      // Channel objects, for managing input test data
      // private uint currentChannelIdx;
      private ADCChannel currentChannel;
      private readonly ADCChannel[] channels;
      // hrkim
      private IValueRegisterField selectedChannel;
      private IFlagRegisterField HwTrigger; // 1 : HW trigger
      private uint adcData;

      public const int NumberOfChannels = 16;

      private enum Registers
      {
         // Status = 0x0,
         // Control1 = 0x4,
         // Control2 = 0x8,
         // SampleTime1 = 0x0C,
         // SampleTime2 = 0x10,
         // InjectedChannelDataOffset1 = 0x14,
         // InjectedChannelDataOffset2 = 0x18,
         // InjectedChannelDataOffset3 = 0x1C,
         // InjectedChannelDataOffset4 = 0x20,
         // WatchdogHigherThreshold = 0x24,
         // WatchdogLowerThreshold = 0x28,
         // RegularSequence1 = 0x2C,
         // RegularSequence2 = 0x30,
         // RegularSequence3 = 0x34,
         // InjectedSequence = 0x38,
         // InjectedData1 = 0x3C,
         // InjectedData2 = 0x40,
         // InjectedData3 = 0x44,
         // InjectedData4 = 0x48,
         // RegularData = 0x4C
            SC1A = 0x0, // ADC Status and Control Register 1
            CFG1 = 0x40,
            CFG2 = 0x44,
            RA = 0x48,
            CV1 = 0x88,
            CV2 = 0x8C,
            SC2 = 0x90,
            SC3 = 0x94,
            BASE_OFS = 0x98,
            OFS = 0x9C,
            USR_OFS = 0xA0
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
            aRA = 0x188
      }
   }
}
