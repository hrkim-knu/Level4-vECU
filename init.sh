#!/bin/bash

CURRENT_DIR=$(cd ../../ && pwd)

echo "export RENODE_HOME=$CURRENT_DIR" >> ~/.bashrc
echo 'export PATH=$PATH:$RENODE_HOME' >> ~/.bashrc

source ~/.bashrc

echo "RENODE_HOME has been set to $RENODE_HOME & RENODE_HOME is added to PATH."

cd ..
mkdir log

echo "log Directory is created."


