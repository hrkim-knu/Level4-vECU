#!/bin/bash

CURRENT_DIR=$(cd ../../ && pwd)

echo "export RENODE_HOME=$CURRENT_DIR" >> ~/.bashrc

source ~/.bashrc

echo "RENODE_HOME has been set to $RENODE_HOME"
