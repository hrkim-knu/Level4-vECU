
#!/bin/bash

# Copy all files from Timers
cp -a ./src/Timers/. ${RENODE_HOME}/src/Infrastructure/src/Emulator/Peripherals/Peripherals/Timers/

# Copy all files from Miscellaneous
cp -a ./src/Miscellaneous/. ${RENODE_HOME}/src/Infrastructure/src/Emulator/Peripherals/Peripherals/Miscellaneous/

# Copy Peripherals.csproj file
#cp ./src/Peripherals.csproj ${RENODE_HOME}/src/Infrastructure/src/Emulator/Peripherals/
