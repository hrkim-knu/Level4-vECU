import os

template = """
*** Variables ***
${PLATFORM}          @hrkim/Renode/script/s32k148.repl
${ELF}               @hrkim/Renode/elf/S32K_HSP_2019_BASE_R190716_hrkim_ADC_LED.elf
@{LED_PINS}          11     12     13     14
${ADC_CHANNEL}       1
${ADC_REPEAT}        100
${DELAY}             100ms
${TIMEOUT}           10s
${POLL_INTERVAL}     1ms

*** Keywords ***
Create Machine
    Execute Command                 mach create "s32k148_AUTOSAR"
    Execute Command                 machine LoadPlatformDescription ${PLATFORM}  
    Execute Command                 sysbus LoadELF ${ELF}
    ${vector_table_offset}=         Execute Command     sysbus GetSymbolAddress "Os_ExceptionVectorTable"
    Execute Command                 sysbus.cpu VectorTableOffset ${vector_table_offset}

Feed Sample To ADC
    [Arguments]     ${value}    ${channel}  ${repeat}
    #Log to Console      Feeding sample to ADC: value=${value}, channel=${channel}
    Execute Command     sysbus.adc0 FeedSample ${value} ${channel} ${repeat}

Check If Read Is Done
    ${read_done}=       Execute Command    sysbus.adc0 IsReadDone
    Return From Keyword If    ${read_done}    True
    Fail    ADC read not done yet

Wait Until Read Is Done
    Wait Until Keyword Succeeds    ${TIMEOUT}    ${POLL_INTERVAL}    Check If Read Is Done

Reset Pin
    [Arguments]     ${pin}
    Execute Command         sysbus.portE ResetPin ${pin}

Reset GPIO
    FOR     ${pin}  IN  @{LED_PINS}
        Reset Pin   ${pin}
    END

Read LED Value
    [Arguments]     ${pin}
    ${LED_VALUE}=       Execute Command    sysbus.portE ReadPin ${pin}
    RETURN              ${LED_VALUE}
    
Calc Expected LED Values
    [Arguments]     ${adc_value}
    ${led_values}=      Evaluate    [int((int(${adc_value} / 256)) >> i) & 1 for i in range(4)]
    #Log to Console     expected LED values: ${led_values}
    RETURN              ${led_values}

*** Test Cases ***
Test Case ${ADC_value}
    Create Machine
    Start Emulation
    Reset GPIO

    Feed Sample To ADC    ${ADC_value}    ${ADC_CHANNEL}    ${ADC_REPEAT}
    ${expected_led_values}=    Calc Expected LED Values    ${ADC_value}
    Wait Until Read Is Done

    FOR    ${index}    IN RANGE    0    4
        ${led_value}=    Read LED Value    ${LED_PINS[${index}]}
        #Log To Console    LED pin=${LED_PINS[${index}]}, Read LED value=${led_value}, Expected value=${expected_led_values[${index}]}
        Should Be Equal As Numbers    ${led_value}    ${expected_led_values[${index}]}
    END
"""

output_dir = "generated_tests"

if not os.path.exists(output_dir):
    os.makedirs(output_dir)

for value in range(4096):
    test_case_content = template.replace("${ADC_value}", str(value))
    with open(os.path.join(output_dir, f"test_{value}.robot"), "w") as test_file:
        test_file.write(test_case_content)
