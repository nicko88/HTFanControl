#!/bin/bash

wget https://github.com/nicko88/HTFanControl/releases/latest/download/HTFanControl_RasPi.zip
rm HTFanControl
unzip HTFanControl_RasPi.zip
rm HTFanControl_RasPi.zip
chmod +x HTFanControl
nohup ./HTFanControl &>/dev/null &
