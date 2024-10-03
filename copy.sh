
#!/bin/bash

# Copy all files from Timers
cp -a ./src/Timers/. ${RENODE_HOME}/src/Infrastructure/src/Emulator/Peripherals/Peripherals/Timers/

# Copy all files from Timers
cp -a ./src/Time/. ${RENODE_HOME}/src/Infrastructure/src/Emulator/Main/Time/

# Copy all files from Miscellaneous
cp -a ./src/Miscellaneous/. ${RENODE_HOME}/src/Infrastructure/src/Emulator/Peripherals/Peripherals/Miscellaneous/

# Copy all files from Analog
cp -a ./src/Analog/. ${RENODE_HOME}/src/Infrastructure/src/Emulator/Peripherals/Peripherals/Analog/

# Copy all files from CAN1
cp -a ./src/CAN/S32K1XX_FlexCAN* ${RENODE_HOME}/src/Infrastructure/src/Emulator/Peripherals/Peripherals/CAN/S32K1XX_FlexCAN/

# Copy all files from CAN2
cp -a ./src/CAN/S32K3XX_FlexCAN* ${RENODE_HOME}/src/Infrastructure/src/Emulator/Peripherals/Peripherals/CAN/S32K3XX_FlexCAN/

# Copy all files from Commands
cp -a ./src/Commands/RunForCommand.cs ${RENODE_HOME}/src/Infrastructure/src/Emulator/Extensions/UserInterface/Commands/

# Copy all files from UI
cp -a ./src/UI/. ${RENODE_HOME}/src/Infrastructure/src/UI/

# Copy all files from Extensions
cp -a ./src/Extensions/. ${RENODE_HOME}/src/Infrastructure/src/Emulator/Extensions/
