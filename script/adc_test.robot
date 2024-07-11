*** Variables ***
${PLATFORM}          @hrkim/Renode/script/s32k148.repl
${ADC_CHANNEL}       1
${ADC_REPEAT}        1
${DELAY}             1ms
${TIMEOUT}           500
${POLL_INTERVAL}     1ms

*** Keywords ***
Prepare Machine
    Execute Command     mach create "s32k148_AUTOSAR"
    Execute Command     machine LoadPlatformDescription ${PLATFORM}
    
    Execute Command     sysbus LoadELF @hrkim/Renode/elf/S32K_HSP_2019_BASE_R190716_hrkim_ADC.elf
    ${vector_table_offset}=     Execute Command     sysbus GetSymbolAddress "Os_ExceptionVectorTable"
    Execute Command     sysbus.cpu VectorTableOffset ${vector_table_offset}

Feed Sample To ADC
    [Arguments]     ${value}    ${channel}  ${repeat}
    #Log to Console     Feeding sample to ADC: value=${value}, channel=${channel}
    Execute Command     sysbus.adc0 FeedSample ${value} ${channel} ${repeat}
    Execute Command     sysbus.adc0 EnableTestMode

Read ADC Value
    [Arguments]     ${channel}
    ${ADC_VALUE}=   Execute Command     sysbus.adc0 ReadADC ${channel}
    #Log To Console     Read ADC value: channel=${channel}, value=${ADC_VALUE}
    RETURN    ${ADC_VALUE}


*** Test Cases ***
ADC Value Check TC
    Prepare Machine
    Start Emulation

    FOR     ${value}    IN RANGE    0   4096
        Feed Sample To ADC  ${value}    ${ADC_CHANNEL}  ${ADC_REPEAT}
        
        #Sleep       ${DELAY}
        ${adc_value}=   Read ADC Value  ${ADC_CHANNEL}
        #Log to Console     Checking ADC value : expected=${value}, actual=${adc_value}
        Should Be Equal As Numbers      ${adc_value}    ${value}
    END
    
